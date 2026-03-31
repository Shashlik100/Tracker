using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TrackerApp;

internal sealed class SefariaTurService : IDisposable
{
    private const string BaseUrl = "https://www.sefaria.org/";
    private static readonly TimeSpan ShapeCacheDuration = TimeSpan.FromDays(30);

    private static readonly IReadOnlyList<HalakhicSectionInfo> Sections =
    [
        new("Tur, Orach Chayim", "אורח חיים", 1),
        new("Tur, Yoreh De'ah", "יורה דעה", 2),
        new("Tur, Even HaEzer", "אבן העזר", 3),
        new("Tur, Choshen Mishpat", "חושן משפט", 4)
    ];

    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly List<SefariaRequestLogEntry> _requestLog = [];
    private readonly Dictionary<string, HalakhicSectionShape> _shapes = new(StringComparer.OrdinalIgnoreCase);

    public SefariaTurService(string cacheDirectory)
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

    public IReadOnlyList<HalakhicSectionInfo> GetSections() => Sections;

    public HalakhicSectionShape GetSectionShape(string englishTitle)
    {
        if (_shapes.TryGetValue(englishTitle, out var cached))
        {
            return cached;
        }

        using var document = GetCachedJsonDocument(
            $"tur-shape-{SanitizeCacheKey(englishTitle)}",
            $"api/shape/{Uri.EscapeDataString(englishTitle)}",
            ShapeCacheDuration);

        if (!TryExtractSectionShape(document.RootElement, englishTitle, out var shape))
        {
            throw new InvalidOperationException($"לא ניתן היה לפענח את מבנה הטור {englishTitle} מ-Sefaria.");
        }

        _shapes[englishTitle] = shape;
        return shape;
    }

    public string GetPreferredSectionHebrewTitle(string englishTitle)
    {
        return Sections.FirstOrDefault(section =>
                string.Equals(section.EnglishTitle, englishTitle, StringComparison.OrdinalIgnoreCase))
            ?.HebrewTitle
            ?? englishTitle.Replace("Tur, ", string.Empty, StringComparison.Ordinal);
    }

    public IReadOnlyList<SefariaRequestLogEntry> GetRequestLog() => _requestLog.ToList();

    public void Dispose()
    {
        _httpClient.Dispose();
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

            throw new InvalidOperationException("לא ניתן לטעון כעת את נתוני הטור מ-Sefaria.", fetchException);
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

    private static bool TryExtractSectionShape(JsonElement root, string englishTitle, out HalakhicSectionShape shape)
    {
        shape = default!;
        var sectionElement = root;
        if (root.ValueKind == JsonValueKind.Array)
        {
            sectionElement = root.EnumerateArray().FirstOrDefault();
        }

        if (sectionElement.ValueKind != JsonValueKind.Object ||
            !TryGetPropertyIgnoreCase(sectionElement, "chapters", out var chaptersElement) ||
            chaptersElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var unitCounts = chaptersElement
            .EnumerateArray()
            .Where(entry => entry.ValueKind == JsonValueKind.Number && entry.TryGetInt32(out _))
            .Select(entry => entry.GetInt32())
            .ToArray();

        if (unitCounts.Length == 0)
        {
            return false;
        }

        var hebrewTitle = TryGetPropertyIgnoreCase(sectionElement, "heTitle", out var heTitleElement) &&
                          heTitleElement.ValueKind == JsonValueKind.String
            ? CleanSectionHebrewTitle(heTitleElement.GetString())
            : englishTitle;

        var actualEnglishTitle = TryGetPropertyIgnoreCase(sectionElement, "title", out var titleElement) &&
                                 titleElement.ValueKind == JsonValueKind.String
            ? titleElement.GetString() ?? englishTitle
            : englishTitle;

        shape = new HalakhicSectionShape(actualEnglishTitle, hebrewTitle, unitCounts);
        return true;
    }

    private static string CleanSectionHebrewTitle(string? value)
    {
        var title = value?.Trim() ?? string.Empty;
        return title.StartsWith("טור, ", StringComparison.Ordinal)
            ? title["טור, ".Length..]
            : title;
    }

    private void RecordRequest(string endpoint, bool success, string transport, string message)
    {
        _requestLog.Add(new SefariaRequestLogEntry(endpoint, success, transport, message));
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
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
