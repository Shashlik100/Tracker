using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TrackerApp;

internal sealed class SefariaTalmudService : IDisposable
{
    private const string BaseUrl = "https://www.sefaria.org/";
    private const string BavliCollectionKey = "Bavli";
    private const string YerushalmiCollectionKey = "Yerushalmi";
    private static readonly TimeSpan IndexCacheDuration = TimeSpan.FromDays(7);
    private static readonly TimeSpan ShapeCacheDuration = TimeSpan.FromDays(30);

    private static readonly IReadOnlyDictionary<string, string> CollectionHebrewMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [BavliCollectionKey] = "בבלי",
        [YerushalmiCollectionKey] = "ירושלמי"
    };

    private static readonly IReadOnlyDictionary<string, string> SederHebrewFallbackMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Seder Zeraim"] = "סדר זרעים",
        ["Seder Moed"] = "סדר מועד",
        ["Seder Nashim"] = "סדר נשים",
        ["Seder Nezikin"] = "סדר נזיקין",
        ["Seder Kodashim"] = "סדר קדשים",
        ["Seder Tahorot"] = "סדר טהרות"
    };

    private static readonly IReadOnlyDictionary<string, int> SederOrderMap = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["Seder Zeraim"] = 1,
        ["Seder Moed"] = 2,
        ["Seder Nashim"] = 3,
        ["Seder Nezikin"] = 4,
        ["Seder Kodashim"] = 5,
        ["Seder Tahorot"] = 6
    };

    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly List<SefariaRequestLogEntry> _requestLog = [];
    private Dictionary<string, IReadOnlyList<TalmudSederInfo>>? _sedarimByCollection;
    private Dictionary<string, IReadOnlyList<TalmudTractateInfo>>? _tractatesByCollectionAndSeder;
    private readonly Dictionary<string, YerushalmiTractateShape> _yerushalmiShapes = new(StringComparer.OrdinalIgnoreCase);

    public SefariaTalmudService(string cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
        Directory.CreateDirectory(_cacheDirectory);

        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(BaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(20)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TrackerApp", "1.0"));
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("he, en;q=0.8");
    }

    public IReadOnlyList<TalmudCollectionInfo> GetCollections()
    {
        return
        [
            new TalmudCollectionInfo(BavliCollectionKey, CollectionHebrewMap[BavliCollectionKey], 1),
            new TalmudCollectionInfo(YerushalmiCollectionKey, CollectionHebrewMap[YerushalmiCollectionKey], 2)
        ];
    }

    public IReadOnlyList<TalmudSederInfo> GetSedarim(string collectionKey)
    {
        EnsureIndexLoaded();
        return _sedarimByCollection!.TryGetValue(collectionKey, out var sedarim)
            ? sedarim
            : [];
    }

    public IReadOnlyList<TalmudTractateInfo> GetTractates(string collectionKey, string englishSeder)
    {
        EnsureIndexLoaded();
        return _tractatesByCollectionAndSeder!.TryGetValue(BuildSederLookupKey(collectionKey, englishSeder), out var tractates)
            ? tractates
            : [];
    }

    public string GetPreferredCollectionHebrewTitle(string collectionKey)
    {
        return CollectionHebrewMap.GetValueOrDefault(collectionKey, collectionKey);
    }

    public string GetPreferredSederHebrewTitle(string englishSeder)
    {
        return SederHebrewFallbackMap.GetValueOrDefault(englishSeder, englishSeder);
    }

    public string GetPreferredTractateHebrewTitle(string collectionKey, string englishTractateTitle)
    {
        EnsureIndexLoaded();
        foreach (var tractates in _tractatesByCollectionAndSeder!.Values)
        {
            var match = tractates.FirstOrDefault(tractate =>
                string.Equals(tractate.CollectionKey, collectionKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(tractate.EnglishTitle, englishTractateTitle, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match.HebrewTitle;
            }
        }

        return CleanTalmudHebrewTitle(collectionKey, englishTractateTitle);
    }

    public YerushalmiTractateShape GetYerushalmiShape(string englishTractateTitle)
    {
        if (_yerushalmiShapes.TryGetValue(englishTractateTitle, out var cachedShape))
        {
            return cachedShape;
        }

        EnsureIndexLoaded();
        using var document = GetCachedJsonDocument(
            $"talmud-yerushalmi-shape-{SanitizeCacheKey(englishTractateTitle)}",
            $"api/shape/{Uri.EscapeDataString(englishTractateTitle)}",
            ShapeCacheDuration);

        var halakhahCounts = ExtractYerushalmiHalakhahCounts(document.RootElement);
        if (halakhahCounts.Count == 0)
        {
            throw new InvalidOperationException($"לא ניתן היה לפענח את מבנה המסכת הירושלמית {englishTractateTitle} מ-Sefaria.");
        }

        var hebrewTitle = GetPreferredTractateHebrewTitle(YerushalmiCollectionKey, englishTractateTitle);
        var shape = new YerushalmiTractateShape(englishTractateTitle, hebrewTitle, halakhahCounts);
        _yerushalmiShapes[englishTractateTitle] = shape;
        return shape;
    }

    public IReadOnlyList<SefariaRequestLogEntry> GetRequestLog() => _requestLog.ToList();

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private void EnsureIndexLoaded()
    {
        if (_sedarimByCollection is not null && _tractatesByCollectionAndSeder is not null)
        {
            return;
        }

        using var document = GetCachedJsonDocument("talmud-index", "api/index", IndexCacheDuration);
        ParseIndex(document.RootElement, out var sedarimByCollection, out var tractatesByCollectionAndSeder);

        if (sedarimByCollection.Count == 0)
        {
            throw new InvalidOperationException("לא התקבל מבנה תלמוד תקין מ-Sefaria.");
        }

        _sedarimByCollection = sedarimByCollection;
        _tractatesByCollectionAndSeder = tractatesByCollectionAndSeder;
    }

    private JsonDocument GetCachedJsonDocument(string cacheKey, string relativeUrl, TimeSpan duration)
    {
        var cachePath = Path.Combine(_cacheDirectory, $"{SanitizeCacheKey(cacheKey)}.json");
        if (TryReadFreshCache(cachePath, duration, out var freshDocument))
        {
            RecordRequest(relativeUrl, true, "cache", "cache hit");
            return freshDocument;
        }

        try
        {
            var json = TryFetchJson(relativeUrl);
            File.WriteAllText(cachePath, json, Encoding.UTF8);
            return JsonDocument.Parse(json);
        }
        catch (Exception fetchException)
        {
            RecordRequest(relativeUrl, false, "network", fetchException.Message);
            if (File.Exists(cachePath))
            {
                RecordRequest(relativeUrl, true, "cache", "stale cache fallback");
                return JsonDocument.Parse(File.ReadAllText(cachePath, Encoding.UTF8));
            }

            throw new InvalidOperationException("לא ניתן לטעון כעת את נתוני התלמוד מ-Sefaria.", fetchException);
        }
    }

    private string TryFetchJson(string relativeUrl)
    {
        try
        {
            var json = FetchWithNode(relativeUrl);
            RecordRequest(relativeUrl, true, "node", "ok");
            return json;
        }
        catch (Exception nodeException)
        {
            RecordRequest(relativeUrl, false, "node", nodeException.Message);
            var json = _httpClient.GetStringAsync(relativeUrl).GetAwaiter().GetResult();
            RecordRequest(relativeUrl, true, "httpclient", "ok");
            return json;
        }
    }

    private static bool TryReadFreshCache(string cachePath, TimeSpan duration, out JsonDocument document)
    {
        document = default!;
        if (!File.Exists(cachePath))
        {
            return false;
        }

        var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath);
        if (age > duration)
        {
            return false;
        }

        document = JsonDocument.Parse(File.ReadAllText(cachePath, Encoding.UTF8));
        return true;
    }

    private static string FetchWithNode(string relativeUrl)
    {
        const string script = """
            const https = require('https');
            const url = process.argv[1] || process.argv[2];
            https.get(url, { headers: { 'User-Agent': 'TrackerApp/1.0' } }, (res) => {
              if (res.statusCode && res.statusCode >= 400) {
                console.error(`HTTP ${res.statusCode}`);
                process.exit(1);
                return;
              }
              let data = '';
              res.setEncoding('utf8');
              res.on('data', chunk => data += chunk);
              res.on('end', () => process.stdout.write(data));
            }).on('error', (err) => {
              console.error(err.message);
              process.exit(1);
            });
            """;

        var startInfo = new ProcessStartInfo
        {
            FileName = "node",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add(script);
        startInfo.ArgumentList.Add(new Uri(new Uri(BaseUrl, UriKind.Absolute), relativeUrl).ToString());

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("לא ניתן היה להפעיל את node לצורך גישה ל-Sefaria.");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "התקבלה תגובה ריקה מ-Sefaria." : error.Trim());
        }

        return output;
    }

    private void ParseIndex(
        JsonElement root,
        out Dictionary<string, IReadOnlyList<TalmudSederInfo>> sedarimByCollection,
        out Dictionary<string, IReadOnlyList<TalmudTractateInfo>> tractatesByCollectionAndSeder)
    {
        var tractates = new List<TalmudTractateInfo>();
        CollectTractates(root, tractates);

        sedarimByCollection = tractates
            .GroupBy(tractate => tractate.CollectionKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                collectionGroup => collectionGroup.Key,
                collectionGroup => (IReadOnlyList<TalmudSederInfo>)collectionGroup
                    .GroupBy(tractate => tractate.EnglishSeder, StringComparer.Ordinal)
                    .Select(group => new TalmudSederInfo(
                        collectionGroup.Key,
                        group.Key,
                        group.First().HebrewSeder,
                        SederOrderMap.GetValueOrDefault(group.Key, int.MaxValue)))
                    .OrderBy(seder => seder.Order)
                    .ThenBy(seder => seder.HebrewTitle, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        tractatesByCollectionAndSeder = tractates
            .GroupBy(tractate => BuildSederLookupKey(tractate.CollectionKey, tractate.EnglishSeder), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TalmudTractateInfo>)group
                    .OrderBy(tractate => tractate.Order)
                    .ThenBy(tractate => tractate.HebrewTitle, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    private void CollectTractates(JsonElement element, IList<TalmudTractateInfo> tractates)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectTractates(item, tractates);
                }

                break;
            case JsonValueKind.Object:
                if (TryParseBaseTalmudTractate(element, out var tractate))
                {
                    tractates.Add(tractate);
                }

                foreach (var property in element.EnumerateObject())
                {
                    CollectTractates(property.Value, tractates);
                }

                break;
        }
    }

    private static bool TryParseBaseTalmudTractate(JsonElement element, out TalmudTractateInfo tractate)
    {
        tractate = default!;

        if (TryGetPropertyIgnoreCase(element, "dependence", out var dependenceElement) &&
            dependenceElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(dependenceElement.GetString()))
        {
            return false;
        }

        if (!TryGetPropertyIgnoreCase(element, "primary_category", out var primaryCategoryElement) ||
            primaryCategoryElement.ValueKind != JsonValueKind.String ||
            !string.Equals(primaryCategoryElement.GetString(), "Talmud", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryGetPropertyIgnoreCase(element, "categories", out var categoriesElement) ||
            categoriesElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var categories = categoriesElement
            .EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString() ?? string.Empty)
            .ToArray();

        if (categories.Length != 3 ||
            !string.Equals(categories[0], "Talmud", StringComparison.Ordinal) ||
            !categories[2].StartsWith("Seder ", StringComparison.Ordinal))
        {
            return false;
        }

        var collectionKey = categories[1] switch
        {
            BavliCollectionKey => BavliCollectionKey,
            YerushalmiCollectionKey => YerushalmiCollectionKey,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(collectionKey))
        {
            return false;
        }

        var englishTitle = TryGetPropertyIgnoreCase(element, "title", out var titleElement) &&
                           titleElement.ValueKind == JsonValueKind.String
            ? titleElement.GetString() ?? string.Empty
            : string.Empty;
        if (string.IsNullOrWhiteSpace(englishTitle))
        {
            return false;
        }

        var rawHebrewTitle = TryGetPropertyIgnoreCase(element, "heTitle", out var hebrewTitleElement) &&
                             hebrewTitleElement.ValueKind == JsonValueKind.String
            ? hebrewTitleElement.GetString()
            : string.Empty;

        var order = TryGetPropertyIgnoreCase(element, "order", out var orderElement) &&
                    orderElement.ValueKind == JsonValueKind.Number &&
                    orderElement.TryGetInt32(out var parsedOrder)
            ? parsedOrder
            : int.MaxValue;

        var englishSeder = categories[2];
        var hebrewSeder = SederHebrewFallbackMap.GetValueOrDefault(englishSeder, englishSeder);
        tractate = new TalmudTractateInfo(
            collectionKey,
            englishTitle,
            CleanTalmudHebrewTitle(collectionKey, rawHebrewTitle, englishTitle),
            englishSeder,
            hebrewSeder,
            order);
        return true;
    }

    private static IReadOnlyList<int> ExtractYerushalmiHalakhahCounts(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in root.EnumerateArray())
            {
                var counts = ExtractYerushalmiHalakhahCounts(entry);
                if (counts.Count > 0)
                {
                    return counts;
                }
            }

            return [];
        }

        if (root.ValueKind == JsonValueKind.Object &&
            TryGetPropertyIgnoreCase(root, "chapters", out var chaptersElement) &&
            chaptersElement.ValueKind == JsonValueKind.Array)
        {
            var counts = new List<int>();
            foreach (var chapter in chaptersElement.EnumerateArray())
            {
                if (chapter.ValueKind == JsonValueKind.Array)
                {
                    counts.Add(chapter.EnumerateArray().Count());
                }
                else if (chapter.ValueKind == JsonValueKind.Number && chapter.TryGetInt32(out var count))
                {
                    counts.Add(count);
                }
            }

            return counts;
        }

        return [];
    }

    private static string CleanTalmudHebrewTitle(string collectionKey, string? rawTitle, string? englishFallback = null)
    {
        if (!string.IsNullOrWhiteSpace(rawTitle))
        {
            var title = rawTitle.Trim();
            if (string.Equals(collectionKey, YerushalmiCollectionKey, StringComparison.OrdinalIgnoreCase) &&
                title.StartsWith("תלמוד ירושלמי ", StringComparison.Ordinal))
            {
                return title["תלמוד ירושלמי ".Length..].Trim();
            }

            if (string.Equals(collectionKey, BavliCollectionKey, StringComparison.OrdinalIgnoreCase) &&
                title.StartsWith("תלמוד בבלי ", StringComparison.Ordinal))
            {
                return title["תלמוד בבלי ".Length..].Trim();
            }

            return title;
        }

        return englishFallback ?? string.Empty;
    }

    private void RecordRequest(string endpoint, bool success, string transport, string message)
    {
        _requestLog.Add(new SefariaRequestLogEntry(endpoint, success, transport, message));
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string SanitizeCacheKey(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalidCharacters.Contains(character) ? '_' : character);
        }

        return builder.ToString();
    }

    private static string BuildSederLookupKey(string collectionKey, string englishSeder)
    {
        return $"{collectionKey}|{englishSeder}";
    }
}
