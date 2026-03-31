using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace TrackerApp;

internal sealed class SefariaApiService : IDisposable
{
    private const string BaseUrl = "https://www.sefaria.org/";
    private static readonly TimeSpan CategoryCacheDuration = TimeSpan.FromDays(7);
    private static readonly TimeSpan ShapeCacheDuration = TimeSpan.FromDays(30);
    private static readonly TimeSpan ChapterCacheDuration = TimeSpan.FromDays(30);

    private static readonly IReadOnlyDictionary<string, string> SectionPathMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["תורה"] = "Tanakh/Torah",
        ["נביאים"] = "Tanakh/Prophets",
        ["כתובים"] = "Tanakh/Writings"
    };

    private static readonly IReadOnlyDictionary<string, string> TanakhBookHebrewMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Genesis"] = "בראשית",
        ["Exodus"] = "שמות",
        ["Leviticus"] = "ויקרא",
        ["Numbers"] = "במדבר",
        ["Deuteronomy"] = "דברים",
        ["Joshua"] = "יהושע",
        ["Judges"] = "שופטים",
        ["I Samuel"] = "שמואל א",
        ["II Samuel"] = "שמואל ב",
        ["I Kings"] = "מלכים א",
        ["II Kings"] = "מלכים ב",
        ["Isaiah"] = "ישעיהו",
        ["Jeremiah"] = "ירמיהו",
        ["Ezekiel"] = "יחזקאל",
        ["Hosea"] = "הושע",
        ["Joel"] = "יואל",
        ["Amos"] = "עמוס",
        ["Obadiah"] = "עובדיה",
        ["Jonah"] = "יונה",
        ["Micah"] = "מיכה",
        ["Nahum"] = "נחום",
        ["Habakkuk"] = "חבקוק",
        ["Zephaniah"] = "צפניה",
        ["Haggai"] = "חגי",
        ["Zechariah"] = "זכריה",
        ["Malachi"] = "מלאכי",
        ["Psalms"] = "תהילים",
        ["Proverbs"] = "משלי",
        ["Job"] = "איוב",
        ["Song of Songs"] = "שיר השירים",
        ["Ruth"] = "רות",
        ["Lamentations"] = "איכה",
        ["Ecclesiastes"] = "קהלת",
        ["Esther"] = "אסתר",
        ["Daniel"] = "דניאל",
        ["Ezra"] = "עזרא",
        ["Nehemiah"] = "נחמיה",
        ["I Chronicles"] = "דברי הימים א",
        ["II Chronicles"] = "דברי הימים ב"
    };

    private static readonly IReadOnlyDictionary<string, string[]> SectionBookOrder = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["תורה"] =
        [
            "Genesis", "Exodus", "Leviticus", "Numbers", "Deuteronomy"
        ],
        ["נביאים"] =
        [
            "Joshua", "Judges", "I Samuel", "II Samuel", "I Kings", "II Kings",
            "Isaiah", "Jeremiah", "Ezekiel", "Hosea", "Joel", "Amos", "Obadiah",
            "Jonah", "Micah", "Nahum", "Habakkuk", "Zephaniah", "Haggai",
            "Zechariah", "Malachi"
        ],
        ["כתובים"] =
        [
            "Psalms", "Proverbs", "Job", "Song of Songs", "Ruth", "Lamentations",
            "Ecclesiastes", "Esther", "Daniel", "Ezra", "Nehemiah",
            "I Chronicles", "II Chronicles"
        ]
    };

    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly Dictionary<string, IReadOnlyList<SefariaBookInfo>> _booksBySection = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TanakhBookShape> _bookShapes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _chapterVerseCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SefariaRequestLogEntry> _requestLog = [];

    public SefariaApiService(string cacheDirectory)
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

    public IReadOnlyList<SefariaBookInfo> GetBooksForSection(string hebrewSectionName)
    {
        if (_booksBySection.TryGetValue(hebrewSectionName, out var cachedBooks))
        {
            return cachedBooks;
        }

        if (!SectionPathMap.TryGetValue(hebrewSectionName, out var categoryPath))
        {
            throw new InvalidOperationException($"קטגוריית תנ\"ך לא מוכרת: {hebrewSectionName}");
        }

        using var document = GetCachedJsonDocument(
            $"category-{categoryPath.Replace('/', '-')}",
            $"api/category/{categoryPath}",
            CategoryCacheDuration);

        var books = ParseBooksForSection(hebrewSectionName, document.RootElement);
        if (books.Count == 0)
        {
            throw new InvalidOperationException($"לא התקבלו ספרים מ-Sefaria עבור {hebrewSectionName}.");
        }

        _booksBySection[hebrewSectionName] = books;
        return books;
    }

    public TanakhBookShape GetBookShape(string englishBookTitle)
    {
        if (_bookShapes.TryGetValue(englishBookTitle, out var cachedShape))
        {
            return cachedShape;
        }

        using var document = GetCachedJsonDocument(
            $"shape-{SanitizeCacheKey(englishBookTitle)}",
            $"api/shape/{Uri.EscapeDataString(englishBookTitle)}",
            ShapeCacheDuration);

        var verseCounts = FindFirstIntegerArray(document.RootElement);
        if (verseCounts.Count == 0)
        {
            throw new InvalidOperationException($"לא ניתן היה לפענח את מבנה הספר {englishBookTitle} מ-Sefaria.");
        }

        var hebrewTitle = ExtractHebrewTitle(document.RootElement)
            ?? TanakhBookHebrewMap.GetValueOrDefault(englishBookTitle, englishBookTitle);

        var shape = new TanakhBookShape(englishBookTitle, hebrewTitle, verseCounts);
        _bookShapes[englishBookTitle] = shape;
        return shape;
    }

    public int GetVerseCountForChapter(string englishBookTitle, int chapterNumber)
    {
        var chapterKey = $"{englishBookTitle}|{chapterNumber}";
        if (_chapterVerseCounts.TryGetValue(chapterKey, out var cachedCount))
        {
            return cachedCount;
        }

        try
        {
            using var document = GetCachedJsonDocument(
                $"chapter-{SanitizeCacheKey(englishBookTitle)}-{chapterNumber}",
                $"api/v3/texts/{Uri.EscapeDataString($"{englishBookTitle} {chapterNumber}")}",
                ChapterCacheDuration);

            var verseCount = FindMaxStringArrayLength(document.RootElement);
            if (verseCount > 0)
            {
                _chapterVerseCounts[chapterKey] = verseCount;
                return verseCount;
            }
        }
        catch
        {
        }

        var shape = GetBookShape(englishBookTitle);
        if (chapterNumber >= 1 && chapterNumber <= shape.VerseCountsByChapter.Count)
        {
            var fallbackCount = shape.VerseCountsByChapter[chapterNumber - 1];
            _chapterVerseCounts[chapterKey] = fallbackCount;
            return fallbackCount;
        }

        throw new InvalidOperationException($"לא ניתן היה לקבוע את מספר הפסוקים עבור {englishBookTitle} פרק {chapterNumber}.");
    }

    public IReadOnlyList<SefariaRequestLogEntry> GetRequestLog()
    {
        return _requestLog.ToList();
    }

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
            File.WriteAllText(cachePath, json);
            return JsonDocument.Parse(json);
        }
        catch (Exception fetchException)
        {
            RecordRequest(relativeUrl, false, "network", fetchException.Message);
            if (File.Exists(cachePath))
            {
                RecordRequest(relativeUrl, true, "cache", "stale cache fallback");
                return JsonDocument.Parse(File.ReadAllText(cachePath));
            }

            throw new InvalidOperationException(
                "לא ניתן לטעון כעת נתוני ספריא. בדקו את החיבור לרשת ונסו שוב.",
                fetchException);
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

        document = JsonDocument.Parse(File.ReadAllText(cachePath));
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
            throw new InvalidOperationException(
                $"Sefaria fetch failed דרך node עבור {relativeUrl}: {error}".Trim());
        }

        return output;
    }

    private void RecordRequest(string endpoint, bool success, string transport, string message)
    {
        _requestLog.Add(new SefariaRequestLogEntry(endpoint, success, transport, message));
        if (_requestLog.Count > 50)
        {
            _requestLog.RemoveAt(0);
        }
    }

    private static IReadOnlyList<SefariaBookInfo> ParseBooksForSection(string hebrewSectionName, JsonElement root)
    {
        var books = new Dictionary<string, SefariaBookInfo>(StringComparer.OrdinalIgnoreCase);
        var directChildren = FindCandidateArray(root, "contents", "children", "books");
        if (directChildren.HasValue)
        {
            foreach (var entry in directChildren.Value.EnumerateArray())
            {
                TryAddBook(entry, books);
            }
        }

        if (SectionBookOrder.TryGetValue(hebrewSectionName, out var orderedEnglishTitles))
        {
            return orderedEnglishTitles
                .Where(books.ContainsKey)
                .Select(title => books[title])
                .ToArray();
        }

        return books.Values.OrderBy(book => book.HebrewTitle, StringComparer.CurrentCulture).ToArray();
    }

    private static void TryAddBook(JsonElement entry, IDictionary<string, SefariaBookInfo> target)
    {
        var englishTitle = ExtractEnglishTitle(entry);
        if (string.IsNullOrWhiteSpace(englishTitle))
        {
            return;
        }

        var hebrewTitle = ExtractHebrewTitle(entry)
            ?? TanakhBookHebrewMap.GetValueOrDefault(englishTitle, englishTitle);

        target[englishTitle] = new SefariaBookInfo(englishTitle, hebrewTitle);
    }

    private static JsonElement? FindCandidateArray(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetPropertyIgnoreCase(element, propertyName, out var property) &&
                property.ValueKind == JsonValueKind.Array)
            {
                return property;
            }
        }

        return null;
    }

    private static IReadOnlyList<int> FindFirstIntegerArray(JsonElement element)
    {
        foreach (var propertyName in new[] { "lengths", "chapter_lengths", "chapterLengths", "sectionLengths" })
        {
            if (TryGetPropertyIgnoreCase(element, propertyName, out var property) &&
                TryReadIntegerArray(property, out var values))
            {
                return values;
            }
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var nested = FindFirstIntegerArray(property.Value);
                if (nested.Count > 0)
                {
                    return nested;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                var nested = FindFirstIntegerArray(child);
                if (nested.Count > 0)
                {
                    return nested;
                }
            }
        }

        return [];
    }

    private static bool TryReadIntegerArray(JsonElement element, out IReadOnlyList<int> values)
    {
        values = [];
        if (element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var numbers = new List<int>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var number))
            {
                return false;
            }

            numbers.Add(number);
        }

        values = numbers;
        return numbers.Count > 0;
    }

    private static int FindMaxStringArrayLength(JsonElement element)
    {
        var maxLength = 0;
        if (element.ValueKind == JsonValueKind.Array)
        {
            if (element.GetArrayLength() > 0 && element.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String))
            {
                return element.GetArrayLength();
            }

            foreach (var child in element.EnumerateArray())
            {
                maxLength = Math.Max(maxLength, FindMaxStringArrayLength(child));
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                maxLength = Math.Max(maxLength, FindMaxStringArrayLength(property.Value));
            }
        }

        return maxLength;
    }

    private static string? ExtractEnglishTitle(JsonElement element)
    {
        foreach (var propertyName in new[] { "title", "enTitle", "indexTitle", "book" })
        {
            if (TryGetStringIgnoreCase(element, propertyName, out var value) && TanakhBookHebrewMap.ContainsKey(value))
            {
                return value;
            }
        }

        if (TryGetPropertyIgnoreCase(element, "titles", out var titles) &&
            titles.ValueKind == JsonValueKind.Array)
        {
            foreach (var title in titles.EnumerateArray())
            {
                if (TryGetStringIgnoreCase(title, "lang", out var lang) &&
                    lang.Equals("en", StringComparison.OrdinalIgnoreCase) &&
                    TryGetStringIgnoreCase(title, "text", out var text) &&
                    TanakhBookHebrewMap.ContainsKey(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static string? ExtractHebrewTitle(JsonElement element)
    {
        foreach (var propertyName in new[] { "heTitle", "he", "heIndexTitle" })
        {
            if (TryGetStringIgnoreCase(element, propertyName, out var value))
            {
                return value;
            }
        }

        if (TryGetPropertyIgnoreCase(element, "titles", out var titles) &&
            titles.ValueKind == JsonValueKind.Array)
        {
            foreach (var title in titles.EnumerateArray())
            {
                if (TryGetStringIgnoreCase(title, "lang", out var lang) &&
                    lang.Equals("he", StringComparison.OrdinalIgnoreCase) &&
                    TryGetStringIgnoreCase(title, "text", out var text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals(propertyName) || property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetStringIgnoreCase(JsonElement element, string propertyName, out string value)
    {
        if (TryGetPropertyIgnoreCase(element, propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        value = string.Empty;
        return false;
    }

    private static string SanitizeCacheKey(string key)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(key.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return sanitized.Replace(' ', '_');
    }
}

public sealed record SefariaRequestLogEntry(string Endpoint, bool Success, string Transport, string Message);
