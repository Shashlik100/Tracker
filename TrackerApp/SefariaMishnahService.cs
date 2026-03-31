using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TrackerApp;

internal sealed class SefariaMishnahService : IDisposable
{
    private const string BaseUrl = "https://www.sefaria.org/";
    private static readonly TimeSpan IndexCacheDuration = TimeSpan.FromDays(7);
    private static readonly TimeSpan ShapeCacheDuration = TimeSpan.FromDays(30);

    private static readonly IReadOnlyDictionary<string, string> SederHebrewFallbackMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Seder Zeraim"] = "סדר זרעים",
        ["Seder Moed"] = "סדר מועד",
        ["Seder Nashim"] = "סדר נשים",
        ["Seder Nezikin"] = "סדר נזיקין",
        ["Seder Kodashim"] = "סדר קדשים",
        ["Seder Tahorot"] = "סדר טהרות"
    };

    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly List<SefariaRequestLogEntry> _requestLog = [];
    private IReadOnlyList<MishnahSederInfo>? _sedarim;
    private Dictionary<string, IReadOnlyList<MishnahTractateInfo>>? _tractatesBySeder;
    private readonly Dictionary<string, MishnahTractateShape> _shapes = new(StringComparer.OrdinalIgnoreCase);

    public SefariaMishnahService(string cacheDirectory)
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

    public IReadOnlyList<MishnahSederInfo> GetSedarim()
    {
        EnsureIndexLoaded();
        return _sedarim!;
    }

    public IReadOnlyList<MishnahTractateInfo> GetTractatesForSeder(string englishSeder)
    {
        EnsureIndexLoaded();
        return _tractatesBySeder!.TryGetValue(englishSeder, out var tractates)
            ? tractates
            : [];
    }

    public MishnahTractateShape GetTractateShape(string englishTractateTitle)
    {
        if (_shapes.TryGetValue(englishTractateTitle, out var cachedShape))
        {
            return cachedShape;
        }

        EnsureIndexLoaded();
        using var document = GetCachedJsonDocument(
            $"mishnah-shape-{SanitizeCacheKey(englishTractateTitle)}",
            $"api/shape/{Uri.EscapeDataString(englishTractateTitle)}",
            ShapeCacheDuration);

        var chapterCounts = ExtractChapterCounts(document.RootElement);
        if (chapterCounts.Count == 0)
        {
            throw new InvalidOperationException($"לא ניתן היה לפענח את מבנה המסכת {englishTractateTitle} מ-Sefaria.");
        }

        var hebrewTitle = GetPreferredTractateHebrewTitle(englishTractateTitle);
        var shape = new MishnahTractateShape(englishTractateTitle, hebrewTitle, chapterCounts);
        _shapes[englishTractateTitle] = shape;
        return shape;
    }

    public string GetPreferredTractateHebrewTitle(string englishTractateTitle)
    {
        EnsureIndexLoaded();
        foreach (var tractates in _tractatesBySeder!.Values)
        {
            var match = tractates.FirstOrDefault(tractate =>
                string.Equals(tractate.EnglishTitle, englishTractateTitle, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match.HebrewTitle;
            }
        }

        return CleanMishnahHebrewTitle(englishTractateTitle);
    }

    public IReadOnlyList<SefariaRequestLogEntry> GetRequestLog() => _requestLog.ToList();

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private void EnsureIndexLoaded()
    {
        if (_sedarim is not null && _tractatesBySeder is not null)
        {
            return;
        }

        using var document = GetCachedJsonDocument("mishnah-index", "api/index", IndexCacheDuration);
        ParseMishnahIndex(document.RootElement, out var sedarim, out var tractatesBySeder);
        if (sedarim.Count == 0)
        {
            throw new InvalidOperationException("לא התקבל מבנה משנה תקין מ-Sefaria.");
        }

        _sedarim = sedarim;
        _tractatesBySeder = tractatesBySeder;
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

            throw new InvalidOperationException("לא ניתן לטעון כעת את נתוני המשנה מ-Sefaria.", fetchException);
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

    private void ParseMishnahIndex(
        JsonElement root,
        out IReadOnlyList<MishnahSederInfo> sedarim,
        out Dictionary<string, IReadOnlyList<MishnahTractateInfo>> tractatesBySeder)
    {
        var sederInfos = new List<MishnahSederInfo>();
        tractatesBySeder = new Dictionary<string, IReadOnlyList<MishnahTractateInfo>>(StringComparer.Ordinal);
        CollectMishnahSedarim(root, sederInfos, tractatesBySeder);

        sedarim = sederInfos
            .GroupBy(seder => seder.EnglishCategory, StringComparer.Ordinal)
            .Select(group => group.OrderBy(seder => seder.Order).First())
            .OrderBy(seder => seder.Order)
            .ToArray();
    }

    private void CollectMishnahSedarim(
        JsonElement element,
        IList<MishnahSederInfo> sedarim,
        IDictionary<string, IReadOnlyList<MishnahTractateInfo>> tractatesBySeder)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectMishnahSedarim(item, sedarim, tractatesBySeder);
                }

                break;
            case JsonValueKind.Object:
                if (TryParseSeder(element, out var sederInfo, out var tractates))
                {
                    if (!tractatesBySeder.ContainsKey(sederInfo.EnglishCategory))
                    {
                        sedarim.Add(sederInfo);
                        tractatesBySeder[sederInfo.EnglishCategory] = tractates;
                    }
                }

                foreach (var property in element.EnumerateObject())
                {
                    CollectMishnahSedarim(property.Value, sedarim, tractatesBySeder);
                }

                break;
        }
    }

    private static bool TryParseSeder(
        JsonElement element,
        out MishnahSederInfo sederInfo,
        out IReadOnlyList<MishnahTractateInfo> tractates)
    {
        sederInfo = default!;
        tractates = [];

        if (!TryGetPropertyIgnoreCase(element, "category", out var categoryElement) ||
            categoryElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var englishCategory = categoryElement.GetString() ?? string.Empty;
        if (!englishCategory.StartsWith("Seder ", StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryGetPropertyIgnoreCase(element, "contents", out var contentsElement) ||
            contentsElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var parsedTractates = new List<MishnahTractateInfo>();
        foreach (var entry in contentsElement.EnumerateArray())
        {
            if (!TryParseBaseMishnahTractate(entry, englishCategory, out var tractate))
            {
                continue;
            }

            parsedTractates.Add(tractate);
        }

        if (parsedTractates.Count == 0)
        {
            return false;
        }

        var hebrewCategory = TryGetPropertyIgnoreCase(element, "heCategory", out var hebrewCategoryElement) &&
                             hebrewCategoryElement.ValueKind == JsonValueKind.String
            ? hebrewCategoryElement.GetString() ?? string.Empty
            : SederHebrewFallbackMap.GetValueOrDefault(englishCategory, englishCategory);

        var order = TryGetPropertyIgnoreCase(element, "order", out var orderElement) &&
                    orderElement.ValueKind == JsonValueKind.Number &&
                    orderElement.TryGetInt32(out var parsedOrder)
            ? parsedOrder
            : int.MaxValue;

        sederInfo = new MishnahSederInfo(englishCategory, hebrewCategory, order);
        tractates = parsedTractates
            .OrderBy(tractate => tractate.Order)
            .ThenBy(tractate => tractate.HebrewTitle, StringComparer.Ordinal)
            .ToArray();
        return true;
    }

    private static bool TryParseBaseMishnahTractate(
        JsonElement element,
        string englishSeder,
        out MishnahTractateInfo tractate)
    {
        tractate = default!;

        if (TryGetPropertyIgnoreCase(element, "dependence", out var dependenceElement) &&
            dependenceElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(dependenceElement.GetString()))
        {
            return false;
        }

        var primaryCategory = TryGetPropertyIgnoreCase(element, "primary_category", out var categoryElement) &&
                              categoryElement.ValueKind == JsonValueKind.String
            ? categoryElement.GetString()
            : null;

        var corpus = TryGetPropertyIgnoreCase(element, "corpus", out var corpusElement) &&
                     corpusElement.ValueKind == JsonValueKind.String
            ? corpusElement.GetString()
            : null;

        if (!string.Equals(primaryCategory, "Mishnah", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(corpus, "Mishnah", StringComparison.OrdinalIgnoreCase))
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

        var hebrewTitle = TryGetPropertyIgnoreCase(element, "heTitle", out var hebrewTitleElement) &&
                          hebrewTitleElement.ValueKind == JsonValueKind.String
            ? CleanMishnahHebrewTitle(hebrewTitleElement.GetString())
            : CleanMishnahHebrewTitle(englishTitle);

        var hebrewSeder = SederHebrewFallbackMap.GetValueOrDefault(englishSeder, englishSeder);
        var order = TryGetPropertyIgnoreCase(element, "order", out var orderElement) &&
                    orderElement.ValueKind == JsonValueKind.Number &&
                    orderElement.TryGetInt32(out var parsedOrder)
            ? parsedOrder
            : int.MaxValue;

        tractate = new MishnahTractateInfo(
            englishTitle,
            hebrewTitle,
            englishSeder,
            hebrewSeder,
            order);
        return true;
    }

    private static IReadOnlyList<int> ExtractChapterCounts(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in root.EnumerateArray())
            {
                if (TryGetPropertyIgnoreCase(entry, "chapters", out var chapters) &&
                    chapters.ValueKind == JsonValueKind.Array)
                {
                    var counts = chapters
                        .EnumerateArray()
                        .Where(value => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _))
                        .Select(value => value.GetInt32())
                        .ToArray();
                    if (counts.Length > 0)
                    {
                        return counts;
                    }
                }
            }
        }

        if (root.ValueKind == JsonValueKind.Object &&
            TryGetPropertyIgnoreCase(root, "chapters", out var chapterArray) &&
            chapterArray.ValueKind == JsonValueKind.Array)
        {
            return chapterArray
                .EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _))
                .Select(value => value.GetInt32())
                .ToArray();
        }

        return [];
    }

    private static string CleanMishnahHebrewTitle(string? rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
        {
            return string.Empty;
        }

        var title = rawTitle.Trim();
        return title.StartsWith("משנה ", StringComparison.Ordinal)
            ? title["משנה ".Length..].Trim()
            : title;
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
}
