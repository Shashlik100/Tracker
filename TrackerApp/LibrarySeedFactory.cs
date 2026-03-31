namespace TrackerApp;

internal static class LibrarySeedFactory
{
    public const string SeedVersion = "library-hebrew-tanakh-talmud-mishnah-rambam-tur-shulchan-arukh-v4";

    private static readonly Lazy<IReadOnlyList<TalmudCollectionMetadata>> Collections = new(BuildCollections);
    private static readonly Lazy<Dictionary<string, TalmudTractateMetadata>> TractatesByPath = new(BuildTractateLookup);

    private static readonly string[] BavliMetadataLines = SplitMetadataLines(
    """
    סדר זרעים|ברכות|1:2-13,2:13-17,3:17-26,4:26-30,5:30-34,6:35-45,7:45-51,8:51-53,9:54-64
    סדר מועד|שבת|1:2-20,2:20-36,3:36-47,4:47-51,5:51-56,6:57-67,7:67-76,8:76-82,9:82-90,10:90-96,11:96-102,12:102-105,13:105-107,14:107-111,15:111-115,16:115-122,17:122-126,18:126-129,19:130-137,20:137-141,21:141-143,22:143-148,23:148-153,24:153-157
    סדר מועד|עירובין|1:2-17,2:17-26,3:26-41,4:41-52,5:52-61,6:61-76,7:76-82,8:82-89,9:89-95,10:95-105
    סדר מועד|פסחים|1:2-21,2:21-42,3:42-50,4:50-57,5:58-65,6:65-73,7:74-86,8:87-92,9:92-99,10:99-121
    סדר מועד|שקלים|1:2-4,2:4-6,3:6-9,4:9-12,5:12-14,6:14-17,7:17-20,8:20-22
    סדר מועד|יומא|1:2-21,2:22-28,3:28-39,4:39-46,5:47-62,6:62-68,7:68-73,8:73-88
    סדר מועד|סוכה|1:2-20,2:20-29,3:29-42,4:42-50,5:50-56
    סדר מועד|ביצה|1:2-15,2:15-23,3:23-29,4:29-35,5:35-40
    סדר מועד|ראש השנה|1:2-22,2:22-25,3:25-29,4:29-35
    סדר מועד|תענית|1:2-15,2:15-18,3:18-26,4:26-31
    סדר מועד|מגילה|1:2-17,2:17-21,3:21-25,4:25-32
    סדר מועד|מועד קטן|1:2-11,2:11-13,3:13-29
    סדר מועד|חגיגה|1:2-11,2:11-20,3:20-27
    סדר נשים|יבמות|1:2-17,2:17-26,3:26-35,4:35-50,5:50-53,6:53-66,7:66-70,8:70-84,9:84-87,10:87-97,11:97-101,12:101-106,13:107-112,14:112-114,15:114-118,16:119-122
    סדר נשים|כתובות|1:2-15,2:15-28,3:29-41,4:41-54,5:54-65,6:65-70,7:70-77,8:78-82,9:83-90,10:90-95,11:95-101,12:101-104,13:104-112
    סדר נשים|נדרים|1:2-13,2:13-20,3:20-32,4:32-45,5:45-48,6:49-53,7:54-60,8:60-63,9:64-66,10:66-79,11:79-91
    סדר נשים|נזיר|1:2-8,2:9-16,3:16-20,4:20-30,5:30-34,6:34-47,7:47-57,8:57-61,9:61-66
    סדר נשים|סוטה|1:2-14,2:14-19,3:19-23,4:23-27,5:27-31,6:31-32,7:32-42,8:42-44,9:44-49
    סדר נשים|גיטין|1:2-15,2:15-24,3:24-32,4:32-48,5:48-62,6:62-67,7:67-77,8:77-82,9:82-90
    סדר נשים|קידושין|1:2-41,2:41-58,3:58-69,4:69-82
    סדר נזיקין|בבא קמא|1:2-17,2:17-27,3:27-36,4:36-46,5:46-55,6:55-62,7:62-83,8:83-93,9:93-111,10:111-119
    סדר נזיקין|בבא מציעא|1:2-21,2:21-33,3:33-44,4:44-60,5:60-75,6:75-83,7:83-94,8:94-103,9:103-116,10:116-119
    סדר נזיקין|בבא בתרא|1:2-17,2:17-27,3:28-60,4:61-73,5:73-91,6:92-102,7:102-108,8:108-139,9:139-159,10:160-176
    סדר נזיקין|סנהדרין|1:2-18,2:18-22,3:23-31,4:32-39,5:40-42,6:42-49,7:49-68,8:68-75,9:75-84,10:84-90,11:90-113
    סדר נזיקין|מכות|1:2-7,2:7-13,3:13-24
    סדר נזיקין|שבועות|1:2-14,2:14-19,3:19-29,4:30-36,5:36-38,6:38-44,7:44-49,8:49-49
    סדר נזיקין|עבודה זרה|1:2-22,2:22-40,3:40-49,4:49-61,5:62-76
    סדר נזיקין|הוריות|1:2-6,2:6-9,3:9-14
    סדר קדשים|זבחים|1:2-15,2:15-31,3:31-36,4:36-47,5:47-57,6:58-66,7:66-70,8:70-83,9:83-88,10:89-92,11:92-98,12:98-106,13:106-112,14:112-120
    סדר קדשים|מנחות|1:2-13,2:13-17,3:17-38,4:38-52,5:52-63,6:63-72,7:72-76,8:76-83,9:83-87,10:87-94,11:94-100,12:100-104,13:104-110
    סדר קדשים|חולין|1:2-26,2:27-42,3:42-67,4:68-78,5:78-83,6:83-89,7:89-103,8:103-117,9:117-129,10:130-134,11:135-138,12:138-142
    סדר קדשים|בכורות|1:2-13,2:13-19,3:19-26,4:26-31,5:31-37,6:37-43,7:43-46,8:46-52,9:53-61
    סדר קדשים|ערכין|1:2-7,2:7-13,3:13-17,4:17-19,5:19-21,6:21-24,7:24-27,8:27-29,9:29-34
    סדר קדשים|תמורה|1:2-13,2:14-17,3:17-21,4:21-24,5:24-27,6:28-31,7:31-34
    סדר קדשים|כריתות|1:2-8,2:8-11,3:11-17,4:17-20,5:20-23,6:23-28
    סדר קדשים|מעילה|1:2-8,2:8-10,3:10-14,4:15-18,5:18-20,6:20-22
    סדר קדשים|תמיד|1:25-28,2:28-30,3:30-30,4:30-32,5:32-33,6:33-33,7:33-33
    סדר טהרות|נדה|1:2-12,2:13-21,3:21-31,4:31-39,5:40-48,6:48-54,7:54-57,8:57-59,9:59-64,10:64-73
    """);

    private static readonly string[] YerushalmiMetadataLines = SplitMetadataLines(
    """
    סדר זרעים|ברכות|1:1a-11b,2:12a-21a,3:21b-29a,4:29a-36b,5:36b-41a,6:41a-50b,7:51a-56b,8:56b-62a,9:62a-68a
    סדר זרעים|פאה|1:1a-9b,2:10b-14a,3:14a-19b,4:20a-24b,5:24b-27b,6:28a-31a,7:31b-35a,8:35a-37b
    סדר זרעים|דמאי|1:1a-6a,2:7a-11a,3:11b-16b,4:16b-19b,5:19b-24b,6:25a-29b,7:29b-34a
    סדר זרעים|כלאים|1:1a-5b,2:6a-12b,3:13a-18b,4:18b-23a,5:23b-27b,6:28a-31b,7:31b-35b,8:35b-39b,9:39b-44b
    סדר זרעים|שביעית|1:1a-3a,2:3a-6b,3:6b-9b,4:9b-13a,5:13a-15b,6:15b-18a,7:18b-21a,8:21b-24b,9:24b-27b,10:27b-31a
    סדר זרעים|תרומות|1:1a-8b,2:8b-14a,3:14b-18a,4:18a-25a,5:25a-30b,6:31a-34b,7:34b-38b,8:38b-47a,9:47b-50b,10:50b-54b,11:54b-59a
    סדר זרעים|מעשרות|1:1a-6b,2:7a-12a,3:12b-18a,4:18b-20b,5:21a-26b
    סדר זרעים|מעשר שני|1:1a-8a,2:8a-14a,3:14a-20b,4:20b-27b,5:28a-33b
    סדר זרעים|חלה|1:1a-9b,2:10a-14b,3:14b-22b,4:22b-28b
    סדר זרעים|ערלה|1:1a-8a,2:8b-16b,3:16b-20b
    סדר זרעים|בכורים|1:1a-6a,2:6b-10b,3:10b-13a
    סדר מועד|שבת|1:1a-14a,2:14b-20b,3:21a-28a,4:28a-31a,5:31a-33a,6:33a-39b,7:40a-53b,8:54a-57a,9:57a-61b,10:61b-64b,11:64b-68a,12:68a-71a,13:71a-74a,14:74a-77a,15:77a-78b,16:78b-81b,17:82a-84a,18:84a-86a,19:86a-90b,20:90b-92a,21:92a-92a,22:92a-92a,23:92a-92b,24:92b-92b
    סדר מועד|עירובין|1:1a-13a,2:13b-18a,3:18a-26b,4:26b-30b,5:30b-37b,6:38a-45a,7:45a-50a,8:50b-55b,9:56a-58b,10:58b-65b
    סדר מועד|פסחים|1:1a-10a,2:10b-19a,3:19b-24b,4:25a-30b,5:30b-38b,6:39a-46a,7:46a-57b,8:57b-63b,9:63b-68a,10:68a-71b
    סדר מועד|ביצה|1:1a-9b,2:9b-13a,3:13a-16a,4:16a-19a,5:19a-22b
    סדר מועד|ראש השנה|1:1a-10a,2:10b-14b,3:14b-18a,4:18a-22a
    סדר מועד|יומא|1:1a-8a,2:8a-13b,3:13b-20a,4:20a-24a,5:24b-32a,6:32a-36b,7:36b-38b,8:38b-42b
    סדר מועד|סוכה|1:1a-8a,2:8a-12a,3:12a-17b,4:17b-22a,5:22a-26b
    סדר מועד|תענית|1:1a-7b,2:7b-13b,3:13b-17b,4:17b-26b
    סדר מועד|שקלים|1:1a-7b,2:7b-11b,3:11b-14b,4:15a-20b,5:21a-24a,6:24a-28a,7:28b-31b,8:31b-33b
    סדר מועד|מגילה|1:1a-18a,2:18a-22b,3:22b-27b,4:27b-34a
    סדר מועד|חגיגה|1:1a-8a,2:8a-14b,3:15a-22b
    סדר מועד|מועד קטן|1:1a-7a,2:7b-9b,3:10a-19b
    סדר נשים|יבמות|1:1a-9a,2:9b-15b,3:15b-22a,4:22a-30a,5:30a-34b,6:34b-38b,7:38b-43a,8:43a-50b,9:51a-53a,10:53a-60a,11:60a-65b,12:65b-69b,13:69b-75b,14:75b-77a,15:77a-81b,16:81b-85a
    סדר נשים|סוטה|1:1a-8b,2:8b-13b,3:13b-18b,4:18b-20b,5:20b-26a,6:26a-29a,7:29a-34a,8:34a-39b,9:39b-47a
    סדר נשים|כתובות|1:1a-8b,2:9a-15a,3:15b-23a,4:23a-31a,5:31b-38b,6:38b-42b,7:43a-47a,8:47b-51a,9:51a-57b,10:58a-61a,11:61a-64a,12:64b-67b,13:67b-72a
    סדר נשים|נדרים|1:1a-5a,2:5a-7b,3:7b-13a,4:13a-17a,5:17a-19b,6:19b-24a,7:24a-25b,8:26a-28b,9:28b-31b,10:31b-35b,11:35b-40a
    סדר נשים|נזיר|1:1a-5a,2:5a-11a,3:11a-16a,4:16a-20a,5:20a-24a,6:24a-32b,7:32b-38b,8:38b-42a,9:42a-47b
    סדר נשים|גיטין|1:1a-9a,2:9a-14a,3:14b-19b,4:19b-26a,5:26a-33b,6:34a-38b,7:38b-44a,8:44a-49b,9:49b-54b
    סדר נשים|קידושין|1:1a-23a,2:23a-31a,3:31a-41b,4:41b-48b
    סדר נזיקין|בבא קמא|1:1a-6b,2:7a-11b,3:11b-17a,4:17b-23a,5:23a-26b,6:26b-30a,7:30a-34a,8:34a-37a,9:37b-42a,10:42a-44b
    סדר נזיקין|בבא מציעא|1:1a-6a,2:6a-10a,3:10a-13b,4:13b-18b,5:18b-24b,6:25a-27b,7:27b-29b,8:29b-32a,9:32a-35b,10:35b-37a
    סדר נזיקין|בבא בתרא|1:1a-3b,2:3b-7b,3:7b-13a,4:13a-15a,5:15b-18a,6:18a-20a,7:20a-21a,8:21b-26b,9:26b-30b,10:30b-34a
    סדר נזיקין|שבועות|1:1a-8a,2:8b-11b,3:11b-19a,4:19a-23a,5:23a-27b,6:27b-33a,7:33b-39b,8:39b-44b
    סדר נזיקין|מכות|1:1a-4b,2:5a-8a,3:8a-9b
    סדר נזיקין|סנהדרין|1:1a-9b,2:9b-13b,3:13b-20b,4:20b-23b,5:24a-27a,6:27a-30a,7:30a-41a,8:41b-44b,9:44b-48b,10:49a-54b,11:54b-57b
    סדר נזיקין|עבודה זרה|1:1a-8b,2:8b-18a,3:18a-25a,4:25a-31a,5:31a-37b
    סדר נזיקין|הוריות|1:1a-8a,2:8a-11b,3:11b-19b
    סדר טהרות|נדה|1:1a-6a,2:6a-9a,3:9a-12b,4:12b-13a
    """);

    public static IReadOnlyList<LibrarySeedNode> GetDefaultHierarchy()
    {
        return
        [
            BuildTanakhTree(),
            BuildMishnahTree(),
            BuildRambamTree(),
            BuildTurTree(),
            BuildShulchanArukhTree(),
            BuildTalmudTree()
        ];
    }

    public static IReadOnlyList<TalmudCollectionMetadata> GetTalmudCollections() => Collections.Value;

    public static IReadOnlyList<(string[] Path, string Topic, string Question, string Answer)> GetSampleCards()
    {
        return
        [
            (
                ["תלמוד", "בבלי"],
                "קריאת שמע של ערבית",
                "מדוע מסכת ברכות פותחת בזמן קריאת שמע של ערבית?",
                "הפתיחה קושרת את קבלת עול מלכות שמים לסדרי היום ההלכתיים, וממקמת את עבודת האדם כבר מתחילת הלילה."
            ),
            (
                ["תלמוד", "בבלי"],
                "במה מדליקין",
                "מה עיקר עניינו של פרק במה מדליקין?",
                "הפרק עוסק בחומרי ההדלקה והפתילה, בגדרי שמן הראוי לנר שבת, וביחס בין הידור המצוה לחשש כיבוי והטיה."
            ),
            (
                ["תלמוד", "ירושלמי"],
                "סמיכות גאולה לתפילה",
                "כיצד מדגיש הירושלמי את הערך של סמיכות גאולה לתפילה?",
                "הירושלמי מדגיש את הרצף בין הזכרת הגאולה לבין העמידה לפני ה', ומתאר זאת כביטוי של אמונה חיה והישענות מלאה על גאולת ישראל."
            )
        ];
    }

    public static bool HasLazyChildren(IReadOnlyList<string> pathSegments)
    {
        return pathSegments.Count switch
        {
            4 => TryGetTractate(pathSegments, out _),
            5 => TryGetChapter(pathSegments, out _, out _),
            _ => false
        };
    }

    public static IReadOnlyList<LibrarySeedNode> GetLazyChildren(IReadOnlyList<string> pathSegments)
    {
        if (pathSegments.Count == 4 && TryGetTractate(pathSegments, out var tractate))
        {
            return tractate.Chapters
                .Select(chapter => Node(
                    $"פרק {ToHebrewNumeral(chapter.Number)}",
                    LibraryNodeType.Chapter,
                    string.Empty,
                    $"{tractate.Name}|{chapter.Number}"))
                .ToArray();
        }

        if (pathSegments.Count == 5 && TryGetChapter(pathSegments, out tractate, out var chapter))
        {
            return EnumeratePageNames(tractate.Addressing, chapter)
                .Select(pageName => Leaf(pageName, LibraryNodeType.Page, string.Empty, pageName))
                .ToArray();
        }

        return [];
    }

    public static string ToHebrewNumeral(int number)
    {
        if (number <= 0)
        {
            return number.ToString();
        }

        var result = "";
        while (number >= 400)
        {
            result += "ת";
            number -= 400;
        }

        string[] hundreds = ["", "ק", "ר", "ש"];
        string[] tens = ["", "י", "כ", "ל", "מ", "נ", "ס", "ע", "פ", "צ"];
        string[] ones = ["", "א", "ב", "ג", "ד", "ה", "ו", "ז", "ח", "ט"];

        if (number >= 100)
        {
            result += hundreds[number / 100];
            number %= 100;
        }

        if (number == 15)
        {
            return result + "טו";
        }

        if (number == 16)
        {
            return result + "טז";
        }

        if (number >= 10)
        {
            result += tens[number / 10];
            number %= 10;
        }

        if (number > 0)
        {
            result += ones[number];
        }

        return result;
    }

    private static IReadOnlyList<TalmudCollectionMetadata> BuildCollections()
    {
        return
        [
            BuildCollection("בבלי", TalmudAddressing.Bavli, BavliMetadataLines),
            BuildCollection("ירושלמי", TalmudAddressing.Yerushalmi, YerushalmiMetadataLines)
        ];
    }

    private static TalmudCollectionMetadata BuildCollection(string name, TalmudAddressing addressing, IEnumerable<string> metadataLines)
    {
        var tractates = metadataLines.Select(line => ParseTractate(name, addressing, line)).ToArray();
        var sedarim = tractates
            .GroupBy(tractate => tractate.SederName)
            .Select(group => new TalmudSederMetadata(group.Key, group.ToArray()))
            .ToArray();

        return new TalmudCollectionMetadata(name, addressing, sedarim);
    }

    private static TalmudTractateMetadata ParseTractate(string collectionName, TalmudAddressing addressing, string line)
    {
        var segments = line.Split('|', StringSplitOptions.TrimEntries);
        if (segments.Length != 3)
        {
            throw new InvalidOperationException($"Invalid metadata line: {line}");
        }

        var chapters = segments[2]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseChapter)
            .OrderBy(chapter => chapter.Number)
            .ToArray();

        return new TalmudTractateMetadata(collectionName, segments[0], segments[1], addressing, chapters);
    }

    private static TalmudChapterMetadata ParseChapter(string value)
    {
        var parts = value.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException($"Invalid chapter range: {value}");
        }

        var range = parts[1].Split('-', StringSplitOptions.TrimEntries);
        if (range.Length != 2 || !int.TryParse(parts[0], out var number))
        {
            throw new InvalidOperationException($"Invalid chapter range: {value}");
        }

        return new TalmudChapterMetadata(number, range[0], range[1]);
    }

    private static Dictionary<string, TalmudTractateMetadata> BuildTractateLookup()
    {
        return Collections.Value
            .SelectMany(collection => collection.Sedarim)
            .SelectMany(seder => seder.Tractates)
            .ToDictionary(
                tractate => BuildTractateKey(tractate.CollectionName, tractate.SederName, tractate.Name),
                tractate => tractate,
                StringComparer.Ordinal);
    }

    private static LibrarySeedNode BuildTalmudTree()
    {
        return Node(
            "תלמוד",
            LibraryNodeType.Category,
            "SefariaTalmud",
            "Talmud",
            Node("בבלי", LibraryNodeType.Category, "SefariaTalmud", "Talmud/Bavli"),
            Node("ירושלמי", LibraryNodeType.Category, "SefariaTalmud", "Talmud/Yerushalmi"));
    }

    private static LibrarySeedNode BuildMishnahTree()
    {
        return Node("משנה", LibraryNodeType.Category, "SefariaMishnah", "Mishnah");
    }

    private static LibrarySeedNode BuildRambamTree()
    {
        return Node("רמב\"ם", LibraryNodeType.Category, "SefariaRambam", "Rambam");
    }

    private static LibrarySeedNode BuildTurTree()
    {
        return Node("טור", LibraryNodeType.Category, "SefariaTur", "Tur");
    }

    private static LibrarySeedNode BuildShulchanArukhTree()
    {
        return Node("שולחן ערוך", LibraryNodeType.Category, "SefariaShulchanArukh", "ShulchanArukh");
    }

    private static LibrarySeedNode BuildTanakhTree()
    {
        return Node(
            "תנ\"ך",
            LibraryNodeType.Category,
            "Sefaria",
            "Tanakh",
            Node("תורה", LibraryNodeType.Section, "Sefaria", "Tanakh/Torah"),
            Node("נביאים", LibraryNodeType.Section, "Sefaria", "Tanakh/Prophets"),
            Node("כתובים", LibraryNodeType.Section, "Sefaria", "Tanakh/Writings"));
    }

    private static LibrarySeedNode BuildCollectionNode(TalmudCollectionMetadata collection)
    {
        return Node(collection.Name, LibraryNodeType.Category, string.Empty, collection.Name, collection.Sedarim.Select(BuildSederNode));
    }

    private static LibrarySeedNode BuildSederNode(TalmudSederMetadata seder)
    {
        return Node(seder.Name, LibraryNodeType.Section, string.Empty, seder.Name, seder.Tractates.Select(tractate => Leaf(tractate.Name, LibraryNodeType.Book, string.Empty, tractate.Name)));
    }

    private static IEnumerable<string> EnumeratePageNames(TalmudAddressing addressing, TalmudChapterMetadata chapter)
    {
        return addressing switch
        {
            TalmudAddressing.Bavli => EnumerateBavliPageNames(chapter),
            TalmudAddressing.Yerushalmi => EnumerateYerushalmiPageNames(chapter),
            _ => []
        };
    }

    private static IEnumerable<string> EnumerateBavliPageNames(TalmudChapterMetadata chapter)
    {
        var start = int.Parse(chapter.StartPage, System.Globalization.CultureInfo.InvariantCulture);
        var end = int.Parse(chapter.EndPage, System.Globalization.CultureInfo.InvariantCulture);
        for (var daf = start; daf <= end; daf++)
        {
            yield return $"דף {ToHebrewNumeral(daf)}";
        }
    }

    private static IEnumerable<string> EnumerateYerushalmiPageNames(TalmudChapterMetadata chapter)
    {
        var current = ParseYerushalmiAddress(chapter.StartPage);
        var end = ParseYerushalmiAddress(chapter.EndPage);

        while (true)
        {
            yield return FormatYerushalmiPageLabel(current);
            if (current == end)
            {
                yield break;
            }

            current = AdvanceYerushalmiAddress(current);
        }
    }

    private static YerushalmiAddress ParseYerushalmiAddress(string value)
    {
        if (value.Length < 2)
        {
            throw new InvalidOperationException($"Invalid Yerushalmi address: {value}");
        }

        var side = value[^1];
        var numberPart = value[..^1];
        if (!int.TryParse(numberPart, out var folio) || (side != 'a' && side != 'b'))
        {
            throw new InvalidOperationException($"Invalid Yerushalmi address: {value}");
        }

        return new YerushalmiAddress(folio, side);
    }

    private static YerushalmiAddress AdvanceYerushalmiAddress(YerushalmiAddress current)
    {
        return current.Side == 'a'
            ? current with { Side = 'b' }
            : new YerushalmiAddress(current.Folio + 1, 'a');
    }

    private static string FormatYerushalmiPageLabel(YerushalmiAddress address)
    {
        return $"דף {ToHebrewNumeral(address.Folio)} {(address.Side == 'a' ? "ע\"א" : "ע\"ב")}";
    }

    private static bool TryGetTractate(IReadOnlyList<string> pathSegments, out TalmudTractateMetadata tractate)
    {
        if (pathSegments.Count < 4 || pathSegments[0] != "תלמוד")
        {
            tractate = default!;
            return false;
        }

        return TractatesByPath.Value.TryGetValue(BuildTractateKey(pathSegments[1], pathSegments[2], pathSegments[3]), out tractate!);
    }

    private static bool TryGetChapter(
        IReadOnlyList<string> pathSegments,
        out TalmudTractateMetadata tractate,
        out TalmudChapterMetadata chapter)
    {
        chapter = default!;
        if (!TryGetTractate(pathSegments, out tractate))
        {
            return false;
        }

        if (pathSegments.Count < 5 || !TryParseChapterName(pathSegments[4], out var chapterNumber))
        {
            return false;
        }

        chapter = tractate.Chapters.FirstOrDefault(candidate => candidate.Number == chapterNumber)!;
        return chapter is not null;
    }

    private static bool TryParseChapterName(string chapterName, out int chapterNumber)
    {
        const string prefix = "פרק ";
        if (!chapterName.StartsWith(prefix, StringComparison.Ordinal))
        {
            chapterNumber = 0;
            return false;
        }

        return TryParseHebrewNumeral(chapterName[prefix.Length..], out chapterNumber);
    }

    private static bool TryParseHebrewNumeral(string value, out int result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var letter in value.Trim())
        {
            if (!HebrewNumeralValues.TryGetValue(letter, out var number))
            {
                return false;
            }

            result += number;
        }

        return result > 0;
    }

    private static string BuildTractateKey(string collectionName, string sederName, string tractateName)
    {
        return $"{collectionName}|{sederName}|{tractateName}";
    }

    private static string[] SplitMetadataLines(string metadataBlock)
    {
        return metadataBlock
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static LibrarySeedNode Node(
        string name,
        LibraryNodeType nodeType,
        string sourceSystem,
        string sourceKey,
        params LibrarySeedNode[] children) => new(name, children, nodeType, sourceSystem, sourceKey);

    private static LibrarySeedNode Node(
        string name,
        LibraryNodeType nodeType,
        string sourceSystem,
        string sourceKey,
        IEnumerable<LibrarySeedNode> children) => new(name, children.ToArray(), nodeType, sourceSystem, sourceKey);

    private static LibrarySeedNode Leaf(
        string name,
        LibraryNodeType nodeType,
        string sourceSystem,
        string sourceKey) => new(name, [], nodeType, sourceSystem, sourceKey);

    private static readonly IReadOnlyDictionary<char, int> HebrewNumeralValues = new Dictionary<char, int>
    {
        ['א'] = 1,
        ['ב'] = 2,
        ['ג'] = 3,
        ['ד'] = 4,
        ['ה'] = 5,
        ['ו'] = 6,
        ['ז'] = 7,
        ['ח'] = 8,
        ['ט'] = 9,
        ['י'] = 10,
        ['כ'] = 20,
        ['ך'] = 20,
        ['ל'] = 30,
        ['מ'] = 40,
        ['ם'] = 40,
        ['נ'] = 50,
        ['ן'] = 50,
        ['ס'] = 60,
        ['ע'] = 70,
        ['פ'] = 80,
        ['ף'] = 80,
        ['צ'] = 90,
        ['ץ'] = 90,
        ['ק'] = 100,
        ['ר'] = 200,
        ['ש'] = 300,
        ['ת'] = 400
    };
}

internal sealed record LibrarySeedNode(
    string Name,
    IReadOnlyList<LibrarySeedNode> Children,
    LibraryNodeType NodeType = LibraryNodeType.Generic,
    string SourceSystem = "",
    string SourceKey = "");

internal enum TalmudAddressing
{
    Bavli,
    Yerushalmi
}

internal sealed record TalmudCollectionMetadata(
    string Name,
    TalmudAddressing Addressing,
    IReadOnlyList<TalmudSederMetadata> Sedarim);

internal sealed record TalmudSederMetadata(
    string Name,
    IReadOnlyList<TalmudTractateMetadata> Tractates);

internal sealed record TalmudTractateMetadata(
    string CollectionName,
    string SederName,
    string Name,
    TalmudAddressing Addressing,
    IReadOnlyList<TalmudChapterMetadata> Chapters);

internal sealed record TalmudChapterMetadata(int Number, string StartPage, string EndPage);

internal readonly record struct YerushalmiAddress(int Folio, char Side);
