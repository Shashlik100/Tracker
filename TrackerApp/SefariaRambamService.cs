using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TrackerApp;

internal sealed class SefariaRambamService : IDisposable
{
    private const string BaseUrl = "https://www.sefaria.org/";
    private static readonly TimeSpan IndexCacheDuration = TimeSpan.FromDays(7);
    private static readonly TimeSpan ShapeCacheDuration = TimeSpan.FromDays(30);
    private const string RootCategoryKey = "Mishneh Torah";

    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly List<SefariaRequestLogEntry> _requestLog = [];
    private IReadOnlyList<RambamSeferInfo>? _sefarim;
    private Dictionary<string, IReadOnlyList<RambamHalakhotInfo>>? _halakhotBySefer;
    private readonly Dictionary<string, RambamHalakhotShape> _shapes = new(StringComparer.OrdinalIgnoreCase);

    public SefariaRambamService(string cacheDirectory)
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

    public IReadOnlyList<RambamSeferInfo> GetSefarim()
    {
        EnsureIndexLoaded();
        return _sefarim!;
    }

    public IReadOnlyList<RambamHalakhotInfo> GetHalakhotForSefer(string englishSefer)
    {
        EnsureIndexLoaded();
        return _halakhotBySefer!.TryGetValue(englishSefer, out var halakhot)
            ? halakhot
            : [];
    }

    public RambamHalakhotShape GetHalakhotShape(string englishHalakhotTitle)
    {
        if (_shapes.TryGetValue(englishHalakhotTitle, out var cachedShape))
        {
            return cachedShape;
        }

        EnsureIndexLoaded();
        using var document = GetCachedJsonDocument(
            $"rambam-shape-{SanitizeCacheKey(englishHalakhotTitle)}",
            $"api/shape/{Uri.EscapeDataString(englishHalakhotTitle)}",
            ShapeCacheDuration);

        var chapterCounts = ExtractChapterCounts(document.RootElement);
        if (chapterCounts.Count == 0)
        {
            throw new InvalidOperationException($"לא ניתן היה לפענח את מבנה ההלכות {englishHalakhotTitle} מ-Sefaria.");
        }

        var hebrewTitle = GetPreferredHalakhotHebrewTitle(englishHalakhotTitle);
        var shape = new RambamHalakhotShape(englishHalakhotTitle, hebrewTitle, chapterCounts);
        _shapes[englishHalakhotTitle] = shape;
        return shape;
    }

    public string GetPreferredSeferHebrewTitle(string englishSefer)
    {
        EnsureIndexLoaded();
        return _sefarim!
            .FirstOrDefault(sefer => string.Equals(sefer.EnglishCategory, englishSefer, StringComparison.Ordinal))
            ?.HebrewTitle
            ?? englishSefer;
    }

    public string GetPreferredHalakhotHebrewTitle(string englishHalakhotTitle)
    {
        EnsureIndexLoaded();
        foreach (var halakhotGroup in _halakhotBySefer!.Values)
        {
            var match = halakhotGroup.FirstOrDefault(item =>
                string.Equals(item.EnglishTitle, englishHalakhotTitle, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match.HebrewTitle;
            }
        }

        return CleanRambamHebrewTitle(englishHalakhotTitle);
    }

    public IReadOnlyList<SefariaRequestLogEntry> GetRequestLog() => _requestLog.ToList();

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private void EnsureIndexLoaded()
    {
        if (_sefarim is not null && _halakhotBySefer is not null)
        {
            return;
        }

        using var document = GetCachedJsonDocument("rambam-index", "api/index", IndexCacheDuration);
        ParseRambamIndex(document.RootElement, out var sefarim, out var halakhotBySefer);
        if (sefarim.Count == 0)
        {
            throw new InvalidOperationException("לא התקבל מבנה רמב״ם תקין מ-Sefaria.");
        }

        _sefarim = sefarim;
        _halakhotBySefer = halakhotBySefer;
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

            throw new InvalidOperationException("לא ניתן לטעון כעת את נתוני הרמב״ם מ-Sefaria.", fetchException);
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

    private void ParseRambamIndex(
        JsonElement root,
        out IReadOnlyList<RambamSeferInfo> sefarim,
        out Dictionary<string, IReadOnlyList<RambamHalakhotInfo>> halakhotBySefer)
    {
        var seferInfos = new List<RambamSeferInfo>();
        halakhotBySefer = new Dictionary<string, IReadOnlyList<RambamHalakhotInfo>>(StringComparer.Ordinal);
        CollectRambamSefarim(root, seferInfos, halakhotBySefer);

        sefarim = seferInfos
            .GroupBy(sefer => sefer.EnglishCategory, StringComparer.Ordinal)
            .Select(group => group.OrderBy(sefer => sefer.Order).First())
            .OrderBy(sefer => sefer.Order)
            .ToArray();
    }

    private void CollectRambamSefarim(
        JsonElement element,
        IList<RambamSeferInfo> sefarim,
        IDictionary<string, IReadOnlyList<RambamHalakhotInfo>> halakhotBySefer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectRambamSefarim(item, sefarim, halakhotBySefer);
                }

                break;
            case JsonValueKind.Object:
                if (TryParseSefer(element, out var seferInfo, out var halakhot))
                {
                    if (!halakhotBySefer.ContainsKey(seferInfo.EnglishCategory))
                    {
                        sefarim.Add(seferInfo);
                        halakhotBySefer[seferInfo.EnglishCategory] = halakhot;
                    }
                }

                foreach (var property in element.EnumerateObject())
                {
                    CollectRambamSefarim(property.Value, sefarim, halakhotBySefer);
                }

                break;
        }
    }

    private static bool TryParseSefer(
        JsonElement element,
        out RambamSeferInfo seferInfo,
        out IReadOnlyList<RambamHalakhotInfo> halakhot)
    {
        seferInfo = default!;
        halakhot = [];

        if (!TryGetPropertyIgnoreCase(element, "category", out var categoryElement) ||
            categoryElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var englishCategory = categoryElement.GetString() ?? string.Empty;
        if (!englishCategory.StartsWith("Sefer ", StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryGetPropertyIgnoreCase(element, "contents", out var contentsElement) ||
            contentsElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var parsedHalakhot = new List<RambamHalakhotInfo>();
        foreach (var entry in contentsElement.EnumerateArray())
        {
            if (!TryParseHalakhotEntry(entry, englishCategory, out var halakhah))
            {
                continue;
            }

            parsedHalakhot.Add(halakhah);
        }

        if (parsedHalakhot.Count == 0)
        {
            return false;
        }

        var hebrewCategory = TryGetPropertyIgnoreCase(element, "heCategory", out var hebrewCategoryElement) &&
                             hebrewCategoryElement.ValueKind == JsonValueKind.String
            ? hebrewCategoryElement.GetString() ?? englishCategory
            : englishCategory;

        var order = TryGetPropertyIgnoreCase(element, "order", out var orderElement) &&
                    orderElement.ValueKind == JsonValueKind.Number &&
                    orderElement.TryGetInt32(out var parsedOrder)
            ? parsedOrder
            : int.MaxValue;

        seferInfo = new RambamSeferInfo(englishCategory, hebrewCategory, order);
        halakhot = parsedHalakhot
            .OrderBy(item => item.Order)
            .ThenBy(item => item.HebrewTitle, StringComparer.Ordinal)
            .ToArray();
        return true;
    }

    private static bool TryParseHalakhotEntry(
        JsonElement element,
        string englishSefer,
        out RambamHalakhotInfo halakhotInfo)
    {
        halakhotInfo = default!;

        if (TryGetPropertyIgnoreCase(element, "dependence", out var dependenceElement) &&
            dependenceElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(dependenceElement.GetString()))
        {
            return false;
        }

        var categories = TryGetPropertyIgnoreCase(element, "categories", out var categoriesElement) &&
                         categoriesElement.ValueKind == JsonValueKind.Array
            ? categoriesElement.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString() ?? string.Empty)
                .ToArray()
            : [];

        if (categories.Length < 3 ||
            !string.Equals(categories[1], RootCategoryKey, StringComparison.Ordinal) ||
            !string.Equals(categories[2], englishSefer, StringComparison.Ordinal))
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
            ? CleanRambamHebrewTitle(hebrewTitleElement.GetString())
            : CleanRambamHebrewTitle(englishTitle);

        var hebrewSefer = string.Empty;
        if (TryGetPropertyIgnoreCase(element, "categories", out _) && categories.Length >= 3)
        {
            hebrewSefer = englishSefer;
        }

        var order = TryGetPropertyIgnoreCase(element, "order", out var orderElement) &&
                    orderElement.ValueKind == JsonValueKind.Number &&
                    orderElement.TryGetInt32(out var parsedOrder)
            ? parsedOrder
            : int.MaxValue;

        halakhotInfo = new RambamHalakhotInfo(
            englishTitle,
            hebrewTitle,
            englishSefer,
            hebrewSefer,
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

    private static string CleanRambamHebrewTitle(string? rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
        {
            return string.Empty;
        }

        var title = rawTitle.Trim();
        const string prefix = "משנה תורה, ";
        return title.StartsWith(prefix, StringComparison.Ordinal)
            ? title[prefix.Length..].Trim()
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
