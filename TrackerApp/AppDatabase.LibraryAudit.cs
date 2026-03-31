using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace TrackerApp;

public sealed partial class AppDatabase
{
    private static readonly IReadOnlyDictionary<string, string> SubjectAliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Tanakh"] = "תנ\"ך",
        ["Torah"] = "תורה",
        ["Prophets"] = "נביאים",
        ["Writings"] = "כתובים",
        ["Talmud"] = "תלמוד",
        ["Babylonian Talmud"] = "בבלי",
        ["Jerusalem Talmud"] = "ירושלמי",
        ["תלמוד בבלי"] = "בבלי",
        ["תלמוד ירושלמי"] = "ירושלמי",
        ["Bavli"] = "בבלי",
        ["Yerushalmi"] = "ירושלמי",
        ["Mishnah"] = "משנה",
        ["Rambam"] = "רמב\"ם",
        ["Mishneh Torah"] = "רמב\"ם",
        ["משנה תורה"] = "רמב\"ם",
        ["Tur"] = "טור",
        ["Arba'ah Turim"] = "טור",
        ["Shulchan Arukh"] = "שולחן ערוך",
        ["Shulchan Aruch"] = "שולחן ערוך",
        ["שו\"ע"] = "שולחן ערוך",
        ["Seder Zeraim"] = "סדר זרעים",
        ["Seder Moed"] = "סדר מועד",
        ["Seder Nashim"] = "סדר נשים",
        ["Seder Nezikin"] = "סדר נזיקין",
        ["Seder Kodashim"] = "סדר קדשים",
        ["Seder Tahorot"] = "סדר טהרות"
    };

    private LibraryAuditSummary AuditAndNormalizeLibrary(SqliteConnection connection, SqliteTransaction transaction)
    {
        var existingRootsBefore = GetRootNames(connection, transaction);
        var addedBranches = new List<string>();
        var fixedIssues = new List<string>();
        var completedBranches = new List<string>();

        foreach (var rootName in new[] { "תנ\"ך", "משנה", "רמב\"ם", "טור", "שולחן ערוך", "תלמוד" })
        {
            if (!existingRootsBefore.Contains(rootName, StringComparer.Ordinal))
            {
                addedBranches.Add($"נוסף שורש ספרייה: {rootName}");
            }
        }

        var renamedAliases = NormalizeKnownAliases(connection, transaction);
        if (renamedAliases > 0)
        {
            fixedIssues.Add($"נרמלו {renamedAliases} שמות ישנים או לועזיים לעברית.");
        }

        EnsureDefaultLibraryStructure(connection, transaction);

        var normalizedLegacyTalmudNodes = NormalizeLegacyTalmudSourceKeys(connection, transaction);
        if (normalizedLegacyTalmudNodes > 0)
        {
            fixedIssues.Add($"תוקנו {normalizedLegacyTalmudNodes} מפתחות ישנים בענף התלמוד לצורך תאימות למסד קיים.");
        }

        var normalizedLegacyRambamNodes = NormalizeLegacyRambamSourceKeys(connection, transaction);
        if (normalizedLegacyRambamNodes > 0)
        {
            fixedIssues.Add($"תוקנו {normalizedLegacyRambamNodes} מפתחות ישנים בענף רמב\"ם לצורך תאימות למסד קיים.");
        }

        var normalizedLegacyTurNodes = NormalizeLegacyTurSourceKeys(connection, transaction);
        if (normalizedLegacyTurNodes > 0)
        {
            fixedIssues.Add($"תוקנו {normalizedLegacyTurNodes} מפתחות ישנים בענף טור לצורך תאימות למסד קיים.");
        }

        var normalizedLegacyShulchanArukhNodes = NormalizeLegacyShulchanArukhSourceKeys(connection, transaction);
        if (normalizedLegacyShulchanArukhNodes > 0)
        {
            fixedIssues.Add($"תוקנו {normalizedLegacyShulchanArukhNodes} מפתחות ישנים בענף שולחן ערוך לצורך תאימות למסד קיים.");
        }

        var renamedSourceNodes = NormalizeSourceDrivenSubjectNames(connection, transaction);
        if (renamedSourceNodes > 0)
        {
            fixedIssues.Add($"עודכנו {renamedSourceNodes} שמות צמתים לפי מקורות Sefaria והמבנה הקנוני.");
        }

        var mergeResult = MergeDuplicateSubjects(connection, transaction);
        if (mergeResult.DuplicateGroupsResolved > 0)
        {
            fixedIssues.Add($"אוחדו {mergeResult.DuplicateGroupsResolved} קבוצות כפולות בעץ הספרייה.");
        }

        EnsureDefaultLibraryStructure(connection, transaction);
        var finalSourceRenames = NormalizeSourceDrivenSubjectNames(connection, transaction);
        if (finalSourceRenames > 0)
        {
            fixedIssues.Add($"בוצע מעבר נרמול נוסף על {finalSourceRenames} צמתים לאחר האיחוד.");
        }

        completedBranches.Add("נבדק והובטח מבנה ברירת המחדל של תנ\"ך.");
        completedBranches.Add("נורמל שורש התלמוד למבנה קנוני של תלמוד > בבלי / ירושלמי.");
        completedBranches.Add("בבלי עודכן למבנה עקבי של סדר > מסכת > פרק > דף עם טעינה עצלה.");
        completedBranches.Add("ירושלמי עודכן למבנה מקור-מונחה של סדר > מסכת > פרק > הלכה עם טעינה עצלה.");
        completedBranches.Add("נוסף שורש משנה מוכן לטעינה עצלה של סדרים, מסכתות, פרקים ומשניות.");
        completedBranches.Add("נוסף שורש רמב\"ם מוכן לטעינה עצלה של ספר > הלכות > פרק > הלכה.");
        completedBranches.Add("נוסף שורש טור מוכן לטעינה עצלה של חלק > סימן.");
        completedBranches.Add("נוסף שורש שולחן ערוך מוכן לטעינה עצלה של חלק > סימן > סעיף.");

        return new LibraryAuditSummary
        {
            ExistingRootsBefore = existingRootsBefore,
            AddedBranches = addedBranches,
            FixedIssues = fixedIssues,
            CompletedBranches = completedBranches,
            RemainingLimitations =
            {
                "בבלי נשען על Sefaria עבור אוספים, סדרים ומסכתות, אך גבולות פרקים-דפים עדיין מבוססים על מיפוי פנימי מתוקנן.",
                "ירושלמי נטען מ-Sefaria עד רמת הלכה; לא נוספה כרגע רמת סגמנט כדי לשמור על עץ שימושי ולא כבד מדי.",
                "שלמות תנ\"ך ומשנה מאומתת מול Sefaria בזמן טעינה/אימות ולא נזרעת כולה מראש למסד כדי לשמור על ביצועים.",
                "טור נטען עד רמת סימן, בהתאם למבנה השימושי שנבחר בשלב זה.",
                "שולחן ערוך נטען עד רמת סעיף על בסיס מבנה ה-shape של Sefaria, ללא שכבת מפרשים."
            },
            RenamedNodes = renamedAliases + renamedSourceNodes + finalSourceRenames,
            DuplicateGroupsResolved = mergeResult.DuplicateGroupsResolved,
            MergedSubjects = mergeResult.MergedSubjects,
            ReassignedStudyItems = mergeResult.ReassignedStudyItems,
            ReassignedPresets = mergeResult.ReassignedPresets
        };
    }

    public LibraryVerificationSummary BuildLibraryVerificationSummary()
    {
        using var connection = OpenConnection();

        var tanakhSections = new[] { "תורה", "נביאים", "כתובים" };
        var tanakhBookCount = 0;
        var tanakhChapterCount = 0;
        var tanakhVerseCount = 0;
        foreach (var section in tanakhSections)
        {
            var books = _sefariaApiService.GetBooksForSection(section);
            tanakhBookCount += books.Count;
            foreach (var book in books)
            {
                var shape = _sefariaApiService.GetBookShape(book.EnglishTitle);
                tanakhChapterCount += shape.VerseCountsByChapter.Count;
                tanakhVerseCount += shape.VerseCountsByChapter.Sum();
            }
        }

        var fallbackCollections = LibrarySeedFactory.GetTalmudCollections();
        var bavliCollection = fallbackCollections.First(collection => collection.Name == "בבלי");
        var bavliSedarim = _sefariaTalmudService.GetSedarim("Bavli");
        var bavliTractates = bavliSedarim
            .SelectMany(seder => _sefariaTalmudService.GetTractates("Bavli", seder.EnglishCategory))
            .ToArray();
        var bavliChapterCount = bavliCollection.Sedarim.Sum(seder => seder.Tractates.Sum(tractate => tractate.Chapters.Count));
        var bavliPageCount = bavliCollection.Sedarim.Sum(seder => seder.Tractates.Sum(tractate => tractate.Chapters.Sum(chapter => CountTalmudPages(tractate.Addressing, chapter))));

        var yerushalmiSedarim = _sefariaTalmudService.GetSedarim("Yerushalmi");
        var yerushalmiTractates = yerushalmiSedarim
            .SelectMany(seder => _sefariaTalmudService.GetTractates("Yerushalmi", seder.EnglishCategory))
            .ToArray();
        var yerushalmiChapterCount = 0;
        var yerushalmiHalakhahCount = 0;
        foreach (var tractate in yerushalmiTractates)
        {
            var shape = _sefariaTalmudService.GetYerushalmiShape(tractate.EnglishTitle);
            yerushalmiChapterCount += shape.HalakhotByChapter.Count;
            yerushalmiHalakhahCount += shape.HalakhotByChapter.Sum();
        }

        var talmudCollectionCount = 2;
        var talmudSederCount = bavliSedarim.Count + yerushalmiSedarim.Count;
        var talmudTractateCount = bavliTractates.Length + yerushalmiTractates.Length;
        var talmudChapterCount = bavliChapterCount + yerushalmiChapterCount;
        var talmudPageCount = bavliPageCount + yerushalmiHalakhahCount;

        var sedarim = _sefariaMishnahService.GetSedarim();
        var mishnahTractateCount = 0;
        var mishnahChapterCount = 0;
        var mishnahUnitCount = 0;
        foreach (var seder in sedarim)
        {
            var tractates = _sefariaMishnahService.GetTractatesForSeder(seder.EnglishCategory);
            mishnahTractateCount += tractates.Count;
            foreach (var tractate in tractates)
            {
                var shape = _sefariaMishnahService.GetTractateShape(tractate.EnglishTitle);
                mishnahChapterCount += shape.MishnahCountsByChapter.Count;
                mishnahUnitCount += shape.MishnahCountsByChapter.Sum();
            }
        }

        var sefarim = _sefariaRambamService.GetSefarim();
        var rambamHalakhotCount = 0;
        var rambamChapterCount = 0;
        var rambamUnitCount = 0;
        foreach (var sefer in sefarim)
        {
            var halakhot = _sefariaRambamService.GetHalakhotForSefer(sefer.EnglishCategory);
            rambamHalakhotCount += halakhot.Count;
            foreach (var halakhotEntry in halakhot)
            {
                var shape = _sefariaRambamService.GetHalakhotShape(halakhotEntry.EnglishTitle);
                rambamChapterCount += shape.HalakhotByChapter.Count;
                rambamUnitCount += shape.HalakhotByChapter.Sum();
            }
        }

        var turSections = _sefariaTurService.GetSections();
        var turSimanCount = 0;
        foreach (var section in turSections)
        {
            turSimanCount += _sefariaTurService.GetSectionShape(section.EnglishTitle).UnitCountsBySiman.Count;
        }

        var shulchanArukhSections = _sefariaShulchanArukhService.GetSections();
        var shulchanArukhSimanCount = 0;
        var shulchanArukhSeifCount = 0;
        foreach (var section in shulchanArukhSections)
        {
            var shape = _sefariaShulchanArukhService.GetSectionShape(section.EnglishTitle);
            shulchanArukhSimanCount += shape.UnitCountsBySiman.Count;
            shulchanArukhSeifCount += shape.UnitCountsBySiman.Sum();
        }

        return new LibraryVerificationSummary
        {
            Audit = GetLibraryAuditSummary(),
            RootCount = GetRootNames(connection, null).Count,
            LoadedSubjectCount = _subjects.Count,
            TanakhSectionCount = tanakhSections.Length,
            TanakhBookCount = tanakhBookCount,
            TanakhChapterCount = tanakhChapterCount,
            TanakhVerseCount = tanakhVerseCount,
            TalmudCollectionCount = talmudCollectionCount,
            TalmudSederCount = talmudSederCount,
            TalmudTractateCount = talmudTractateCount,
            TalmudChapterCount = talmudChapterCount,
            TalmudPageCount = talmudPageCount,
            BavliSederCount = bavliSedarim.Count,
            BavliTractateCount = bavliTractates.Length,
            BavliChapterCount = bavliChapterCount,
            BavliPageCount = bavliPageCount,
            YerushalmiSederCount = yerushalmiSedarim.Count,
            YerushalmiTractateCount = yerushalmiTractates.Length,
            YerushalmiChapterCount = yerushalmiChapterCount,
            YerushalmiHalakhahCount = yerushalmiHalakhahCount,
            MishnahSederCount = sedarim.Count,
            MishnahTractateCount = mishnahTractateCount,
            MishnahChapterCount = mishnahChapterCount,
            MishnahUnitCount = mishnahUnitCount,
            RambamSeferCount = sefarim.Count,
            RambamHalakhotCount = rambamHalakhotCount,
            RambamChapterCount = rambamChapterCount,
            RambamUnitCount = rambamUnitCount,
            TurSectionCount = turSections.Count,
            TurSimanCount = turSimanCount,
            ShulchanArukhSectionCount = shulchanArukhSections.Count,
            ShulchanArukhSimanCount = shulchanArukhSimanCount,
            ShulchanArukhSeifCount = shulchanArukhSeifCount,
            DuplicateSiblingGroupCount = CountDuplicateSiblingGroups(connection, null)
        };
    }

    private int NormalizeKnownAliases(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT Id, Name
            FROM Subjects;
            """;

        using var reader = command.ExecuteReader();
        var updates = new List<(int Id, string Name)>();
        while (reader.Read())
        {
            var id = reader.GetInt32(0);
            var currentName = reader.GetString(1);
            if (SubjectAliasMap.TryGetValue(currentName.Trim(), out var desiredName) &&
                !string.Equals(currentName, desiredName, StringComparison.Ordinal))
            {
                updates.Add((id, desiredName));
            }
        }

        reader.Close();
        foreach (var update in updates)
        {
            using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = "UPDATE Subjects SET Name = $name WHERE Id = $id;";
            updateCommand.Parameters.AddWithValue("$id", update.Id);
            updateCommand.Parameters.AddWithValue("$name", update.Name);
            updateCommand.ExecuteNonQuery();
        }

        return updates.Count;
    }

    private int NormalizeSourceDrivenSubjectNames(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT Id, NodeType, SourceSystem, SourceKey, Name
            FROM Subjects
            WHERE SourceSystem <> '';
            """;

        using var reader = command.ExecuteReader();
        var updates = new List<(int Id, string Name)>();
        while (reader.Read())
        {
            var id = reader.GetInt32(0);
            var nodeType = ParseLibraryNodeType(reader.GetString(1));
            var sourceSystem = reader.GetString(2);
            var sourceKey = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            var currentName = reader.GetString(4);
            var expectedName = ResolveCanonicalSourceName(sourceSystem, nodeType, sourceKey);
            if (!string.IsNullOrWhiteSpace(expectedName) &&
                !string.Equals(currentName, expectedName, StringComparison.Ordinal))
            {
                updates.Add((id, expectedName));
            }
        }

        reader.Close();
        foreach (var update in updates)
        {
            using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = "UPDATE Subjects SET Name = $name WHERE Id = $id;";
            updateCommand.Parameters.AddWithValue("$id", update.Id);
            updateCommand.Parameters.AddWithValue("$name", update.Name);
            updateCommand.ExecuteNonQuery();
        }

        return updates.Count;
    }

    private int NormalizeLegacyTalmudSourceKeys(SqliteConnection connection, SqliteTransaction transaction)
    {
        var subjects = LoadSubjects(connection);
        var lookup = subjects.ToDictionary(subject => subject.Id);
        var talmudRoot = subjects.FirstOrDefault(subject => !subject.ParentId.HasValue && subject.Name == "תלמוד");
        if (talmudRoot is null)
        {
            return 0;
        }

        var updates = new List<(int Id, string SourceSystem, string SourceKey)>();
        foreach (var subject in subjects)
        {
            if (!IsDescendantOrSelf(subject.Id, talmudRoot.Id, lookup))
            {
                continue;
            }

            var path = BuildPathSegments(subject.Id, lookup);
            if (path.Count == 0 || path[0] != "תלמוד")
            {
                continue;
            }

            var normalized = TryBuildCanonicalTalmudSource(subject, path);
            if (normalized is null)
            {
                continue;
            }

            if (!string.Equals(subject.SourceSystem, normalized.Value.SourceSystem, StringComparison.Ordinal) ||
                !string.Equals(subject.SourceKey, normalized.Value.SourceKey, StringComparison.Ordinal))
            {
                updates.Add((subject.Id, normalized.Value.SourceSystem, normalized.Value.SourceKey));
            }
        }

        foreach (var update in updates)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE Subjects
                SET SourceSystem = $sourceSystem,
                    SourceKey = $sourceKey
                WHERE Id = $id;
                """;
            command.Parameters.AddWithValue("$id", update.Id);
            command.Parameters.AddWithValue("$sourceSystem", update.SourceSystem);
            command.Parameters.AddWithValue("$sourceKey", update.SourceKey);
            command.ExecuteNonQuery();
        }

        return updates.Count;
    }

    private int NormalizeLegacyRambamSourceKeys(SqliteConnection connection, SqliteTransaction transaction)
    {
        var subjects = LoadSubjects(connection);
        var lookup = subjects.ToDictionary(subject => subject.Id);
        var rambamRoot = subjects.FirstOrDefault(subject =>
            !subject.ParentId.HasValue &&
            (subject.Name == "רמב\"ם" || subject.Name == "משנה תורה"));
        if (rambamRoot is null)
        {
            return 0;
        }

        var updates = new List<(int Id, string SourceSystem, string SourceKey)>();
        foreach (var subject in subjects)
        {
            if (!IsDescendantOrSelf(subject.Id, rambamRoot.Id, lookup))
            {
                continue;
            }

            var path = BuildPathSegments(subject.Id, lookup);
            if (path.Count == 0 || (path[0] != "רמב\"ם" && path[0] != "משנה תורה"))
            {
                continue;
            }

            var normalized = TryBuildCanonicalRambamSource(subject, path);
            if (normalized is null)
            {
                continue;
            }

            if (!string.Equals(subject.SourceSystem, normalized.Value.SourceSystem, StringComparison.Ordinal) ||
                !string.Equals(subject.SourceKey, normalized.Value.SourceKey, StringComparison.Ordinal))
            {
                updates.Add((subject.Id, normalized.Value.SourceSystem, normalized.Value.SourceKey));
            }
        }

        foreach (var update in updates)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE Subjects
                SET SourceSystem = $sourceSystem,
                    SourceKey = $sourceKey
                WHERE Id = $id;
                """;
            command.Parameters.AddWithValue("$id", update.Id);
            command.Parameters.AddWithValue("$sourceSystem", update.SourceSystem);
            command.Parameters.AddWithValue("$sourceKey", update.SourceKey);
            command.ExecuteNonQuery();
        }

        return updates.Count;
    }

    private int NormalizeLegacyTurSourceKeys(SqliteConnection connection, SqliteTransaction transaction)
    {
        var subjects = LoadSubjects(connection);
        var lookup = subjects.ToDictionary(subject => subject.Id);
        var turRoot = subjects.FirstOrDefault(subject => !subject.ParentId.HasValue && subject.Name == "טור");
        if (turRoot is null)
        {
            return 0;
        }

        var updates = new List<(int Id, string SourceSystem, string SourceKey)>();
        foreach (var subject in subjects)
        {
            if (!IsDescendantOrSelf(subject.Id, turRoot.Id, lookup))
            {
                continue;
            }

            var path = BuildPathSegments(subject.Id, lookup);
            if (path.Count == 0 || path[0] != "טור")
            {
                continue;
            }

            var normalized = TryBuildCanonicalTurSource(subject, path);
            if (normalized is null)
            {
                continue;
            }

            if (!string.Equals(subject.SourceSystem, normalized.Value.SourceSystem, StringComparison.Ordinal) ||
                !string.Equals(subject.SourceKey, normalized.Value.SourceKey, StringComparison.Ordinal))
            {
                updates.Add((subject.Id, normalized.Value.SourceSystem, normalized.Value.SourceKey));
            }
        }

        foreach (var update in updates)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE Subjects
                SET SourceSystem = $sourceSystem,
                    SourceKey = $sourceKey
                WHERE Id = $id;
                """;
            command.Parameters.AddWithValue("$id", update.Id);
            command.Parameters.AddWithValue("$sourceSystem", update.SourceSystem);
            command.Parameters.AddWithValue("$sourceKey", update.SourceKey);
            command.ExecuteNonQuery();
        }

        return updates.Count;
    }

    private int NormalizeLegacyShulchanArukhSourceKeys(SqliteConnection connection, SqliteTransaction transaction)
    {
        var subjects = LoadSubjects(connection);
        var lookup = subjects.ToDictionary(subject => subject.Id);
        var root = subjects.FirstOrDefault(subject =>
            !subject.ParentId.HasValue &&
            (subject.Name == "שולחן ערוך" || subject.Name == "שו\"ע"));
        if (root is null)
        {
            return 0;
        }

        var updates = new List<(int Id, string SourceSystem, string SourceKey)>();
        foreach (var subject in subjects)
        {
            if (!IsDescendantOrSelf(subject.Id, root.Id, lookup))
            {
                continue;
            }

            var path = BuildPathSegments(subject.Id, lookup);
            if (path.Count == 0 || (path[0] != "שולחן ערוך" && path[0] != "שו\"ע"))
            {
                continue;
            }

            var normalized = TryBuildCanonicalShulchanArukhSource(subject, path);
            if (normalized is null)
            {
                continue;
            }

            if (!string.Equals(subject.SourceSystem, normalized.Value.SourceSystem, StringComparison.Ordinal) ||
                !string.Equals(subject.SourceKey, normalized.Value.SourceKey, StringComparison.Ordinal))
            {
                updates.Add((subject.Id, normalized.Value.SourceSystem, normalized.Value.SourceKey));
            }
        }

        foreach (var update in updates)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE Subjects
                SET SourceSystem = $sourceSystem,
                    SourceKey = $sourceKey
                WHERE Id = $id;
                """;
            command.Parameters.AddWithValue("$id", update.Id);
            command.Parameters.AddWithValue("$sourceSystem", update.SourceSystem);
            command.Parameters.AddWithValue("$sourceKey", update.SourceKey);
            command.ExecuteNonQuery();
        }

        return updates.Count;
    }

    private (string SourceSystem, string SourceKey)? TryBuildCanonicalRambamSource(SubjectNodeModel subject, IReadOnlyList<string> path)
    {
        if (path.Count == 1)
        {
            return ("SefariaRambam", "Rambam");
        }

        var englishSefer = path.Count >= 2
            ? ResolveEnglishRambamSefer(path[1])
            : string.Empty;
        if (path.Count == 2 && subject.NodeType == LibraryNodeType.Section && !string.IsNullOrWhiteSpace(englishSefer))
        {
            return ("SefariaRambam", $"Rambam|sefer|{englishSefer}");
        }

        if (path.Count >= 3 && subject.NodeType == LibraryNodeType.Book)
        {
            var englishHalakhot = ResolveEnglishRambamHalakhot(englishSefer, path[2]);
            if (!string.IsNullOrWhiteSpace(englishSefer) && !string.IsNullOrWhiteSpace(englishHalakhot))
            {
                return ("SefariaRambam", $"Rambam|halakhot|{englishSefer}|{englishHalakhot}");
            }
        }

        if (path.Count >= 4 && subject.NodeType == LibraryNodeType.Chapter)
        {
            var chapterNumber = ParseHebrewIndexedLabel(path[3], "פרק ");
            var englishHalakhot = ResolveEnglishRambamHalakhot(englishSefer, path[2]);
            if (chapterNumber > 0 && !string.IsNullOrWhiteSpace(englishHalakhot))
            {
                return ("SefariaRambam", $"Rambam|chapter|{englishHalakhot}|{chapterNumber}");
            }
        }

        if (path.Count >= 5 && subject.NodeType == LibraryNodeType.Halakhah)
        {
            var chapterNumber = ParseHebrewIndexedLabel(path[3], "פרק ");
            var halakhahNumber = ParseHebrewIndexedLabel(path[4], "הלכה ");
            var englishHalakhot = ResolveEnglishRambamHalakhot(englishSefer, path[2]);
            if (chapterNumber > 0 && halakhahNumber > 0 && !string.IsNullOrWhiteSpace(englishHalakhot))
            {
                return ("SefariaRambam", $"Rambam|halakhah|{englishHalakhot}|{chapterNumber}|{halakhahNumber}");
            }
        }

        return null;
    }

    private (string SourceSystem, string SourceKey)? TryBuildCanonicalTurSource(SubjectNodeModel subject, IReadOnlyList<string> path)
    {
        if (path.Count == 1)
        {
            return ("SefariaTur", "Tur");
        }

        var englishSection = path.Count >= 2
            ? ResolveEnglishTurSection(path[1])
            : string.Empty;
        if (path.Count == 2 && subject.NodeType == LibraryNodeType.Section && !string.IsNullOrWhiteSpace(englishSection))
        {
            return ("SefariaTur", $"Tur|section|{englishSection}");
        }

        if (path.Count >= 3 && subject.NodeType == LibraryNodeType.Chapter)
        {
            var simanNumber = ParseHebrewIndexedLabel(path[2], "סימן ");
            if (simanNumber > 0 && !string.IsNullOrWhiteSpace(englishSection))
            {
                return ("SefariaTur", $"Tur|siman|{englishSection}|{simanNumber}");
            }
        }

        return null;
    }

    private (string SourceSystem, string SourceKey)? TryBuildCanonicalShulchanArukhSource(SubjectNodeModel subject, IReadOnlyList<string> path)
    {
        if (path.Count == 1)
        {
            return ("SefariaShulchanArukh", "ShulchanArukh");
        }

        var englishSection = path.Count >= 2
            ? ResolveEnglishShulchanArukhSection(path[1])
            : string.Empty;
        if (path.Count == 2 && subject.NodeType == LibraryNodeType.Section && !string.IsNullOrWhiteSpace(englishSection))
        {
            return ("SefariaShulchanArukh", $"ShulchanArukh|section|{englishSection}");
        }

        if (path.Count >= 3 && subject.NodeType == LibraryNodeType.Chapter)
        {
            var simanNumber = ParseHebrewIndexedLabel(path[2], "סימן ");
            if (simanNumber > 0 && !string.IsNullOrWhiteSpace(englishSection))
            {
                return ("SefariaShulchanArukh", $"ShulchanArukh|siman|{englishSection}|{simanNumber}");
            }
        }

        if (path.Count >= 4 && subject.NodeType == LibraryNodeType.Halakhah)
        {
            var simanNumber = ParseHebrewIndexedLabel(path[2], "סימן ");
            var seifNumber = ParseHebrewIndexedLabel(path[3], "סעיף ");
            if (simanNumber > 0 && seifNumber > 0 && !string.IsNullOrWhiteSpace(englishSection))
            {
                return ("SefariaShulchanArukh", $"ShulchanArukh|seif|{englishSection}|{simanNumber}|{seifNumber}");
            }
        }

        return null;
    }

    private (string SourceSystem, string SourceKey)? TryBuildCanonicalTalmudSource(SubjectNodeModel subject, IReadOnlyList<string> path)
    {
        var collectionKey = path.Count >= 2
            ? path[1] switch
            {
                "בבלי" => "Bavli",
                "ירושלמי" => "Yerushalmi",
                _ => string.Empty
            }
            : string.Empty;

        if (path.Count == 1)
        {
            return ("SefariaTalmud", "Talmud");
        }

        if (string.IsNullOrWhiteSpace(collectionKey))
        {
            return null;
        }

        if (path.Count == 2 && subject.NodeType == LibraryNodeType.Category)
        {
            return ("SefariaTalmud", $"Talmud/{collectionKey}");
        }

        if (path.Count >= 3 && subject.NodeType == LibraryNodeType.Section)
        {
            var englishSeder = ResolveEnglishTalmudSeder(collectionKey, path[2]);
            if (!string.IsNullOrWhiteSpace(englishSeder))
            {
                return ("SefariaTalmud", $"{collectionKey}|seder|{englishSeder}");
            }
        }

        if (path.Count >= 4 && subject.NodeType == LibraryNodeType.Book)
        {
            var englishSeder = ResolveEnglishTalmudSeder(collectionKey, path[2]);
            var englishTractate = ResolveEnglishTalmudTractate(collectionKey, englishSeder, path[3]);
            if (!string.IsNullOrWhiteSpace(englishSeder) && !string.IsNullOrWhiteSpace(englishTractate))
            {
                return ("SefariaTalmud", $"{collectionKey}|tractate|{englishSeder}|{englishTractate}");
            }
        }

        if (path.Count >= 5 && subject.NodeType == LibraryNodeType.Chapter)
        {
            var chapterNumber = ParseHebrewIndexedLabel(path[4], "פרק ");
            if (chapterNumber <= 0)
            {
                return null;
            }

            if (collectionKey == "Bavli")
            {
                return ("SefariaTalmud", $"Bavli|chapter|{chapterNumber}");
            }

            var englishSeder = ResolveEnglishTalmudSeder(collectionKey, path[2]);
            var englishTractate = ResolveEnglishTalmudTractate(collectionKey, englishSeder, path[3]);
            if (!string.IsNullOrWhiteSpace(englishTractate))
            {
                return ("SefariaTalmud", $"Yerushalmi|chapter|{englishTractate}|{chapterNumber}");
            }
        }

        if (path.Count >= 6 && subject.NodeType == LibraryNodeType.Page && collectionKey == "Bavli")
        {
            return ("SefariaTalmud", $"Bavli|page|{path[5]}");
        }

        if (path.Count >= 6 && subject.NodeType == LibraryNodeType.Halakhah && collectionKey == "Yerushalmi")
        {
            var chapterNumber = ParseHebrewIndexedLabel(path[4], "פרק ");
            var halakhahNumber = ParseHebrewIndexedLabel(path[5], "הלכה ");
            var englishSeder = ResolveEnglishTalmudSeder(collectionKey, path[2]);
            var englishTractate = ResolveEnglishTalmudTractate(collectionKey, englishSeder, path[3]);
            if (chapterNumber > 0 && halakhahNumber > 0 && !string.IsNullOrWhiteSpace(englishTractate))
            {
                return ("SefariaTalmud", $"Yerushalmi|halakhah|{englishTractate}|{chapterNumber}|{halakhahNumber}");
            }
        }

        return null;
    }

    private string ResolveEnglishTalmudSeder(string collectionKey, string hebrewSeder)
    {
        return _sefariaTalmudService.GetSedarim(collectionKey)
            .FirstOrDefault(seder => string.Equals(seder.HebrewTitle, hebrewSeder, StringComparison.Ordinal))
            ?.EnglishCategory
            ?? string.Empty;
    }

    private string ResolveEnglishRambamSefer(string hebrewSefer)
    {
        return _sefariaRambamService.GetSefarim()
            .FirstOrDefault(sefer => string.Equals(sefer.HebrewTitle, hebrewSefer, StringComparison.Ordinal))
            ?.EnglishCategory
            ?? string.Empty;
    }

    private string ResolveEnglishRambamHalakhot(string englishSefer, string hebrewHalakhot)
    {
        if (string.IsNullOrWhiteSpace(englishSefer))
        {
            return string.Empty;
        }

        return _sefariaRambamService.GetHalakhotForSefer(englishSefer)
            .FirstOrDefault(item => string.Equals(item.HebrewTitle, hebrewHalakhot, StringComparison.Ordinal))
            ?.EnglishTitle
            ?? string.Empty;
    }

    private string ResolveEnglishTurSection(string hebrewSection)
    {
        return _sefariaTurService.GetSections()
            .FirstOrDefault(section => string.Equals(section.HebrewTitle, hebrewSection, StringComparison.Ordinal))
            ?.EnglishTitle
            ?? string.Empty;
    }

    private string ResolveEnglishShulchanArukhSection(string hebrewSection)
    {
        return _sefariaShulchanArukhService.GetSections()
            .FirstOrDefault(section => string.Equals(section.HebrewTitle, hebrewSection, StringComparison.Ordinal))
            ?.EnglishTitle
            ?? string.Empty;
    }

    private string ResolveEnglishTalmudTractate(string collectionKey, string englishSeder, string hebrewTractate)
    {
        if (string.IsNullOrWhiteSpace(englishSeder))
        {
            return string.Empty;
        }

        return _sefariaTalmudService.GetTractates(collectionKey, englishSeder)
            .FirstOrDefault(tractate => string.Equals(tractate.HebrewTitle, hebrewTractate, StringComparison.Ordinal))
            ?.EnglishTitle
            ?? string.Empty;
    }

    private static bool IsDescendantOrSelf(int subjectId, int ancestorId, IReadOnlyDictionary<int, SubjectNodeModel> lookup)
    {
        var currentId = subjectId;
        while (lookup.TryGetValue(currentId, out var current))
        {
            if (current.Id == ancestorId)
            {
                return true;
            }

            if (!current.ParentId.HasValue)
            {
                break;
            }

            currentId = current.ParentId.Value;
        }

        return false;
    }

    private static IReadOnlyList<string> BuildPathSegments(int subjectId, IReadOnlyDictionary<int, SubjectNodeModel> lookup)
    {
        var path = new Stack<string>();
        if (!lookup.TryGetValue(subjectId, out var current))
        {
            return [];
        }

        while (true)
        {
            path.Push(current.Name);
            if (!current.ParentId.HasValue || !lookup.TryGetValue(current.ParentId.Value, out current))
            {
                break;
            }
        }

        return path.ToArray();
    }

    private static int ParseHebrewIndexedLabel(string text, string prefix)
    {
        if (!text.StartsWith(prefix, StringComparison.Ordinal))
        {
            return 0;
        }

        var numeral = text[prefix.Length..].Trim();
        for (var value = 1; value <= 2000; value++)
        {
            if (string.Equals(LibrarySeedFactory.ToHebrewNumeral(value), numeral, StringComparison.Ordinal))
            {
                return value;
            }
        }

        return 0;
    }

    private string ResolveCanonicalSourceName(string sourceSystem, LibraryNodeType nodeType, string sourceKey)
    {
        if (string.Equals(sourceSystem, "Sefaria", StringComparison.OrdinalIgnoreCase))
        {
            return nodeType switch
            {
                LibraryNodeType.Category when string.Equals(sourceKey, "Tanakh", StringComparison.OrdinalIgnoreCase) => "תנ\"ך",
                LibraryNodeType.Section when string.Equals(sourceKey, "Tanakh/Torah", StringComparison.OrdinalIgnoreCase) => "תורה",
                LibraryNodeType.Section when string.Equals(sourceKey, "Tanakh/Prophets", StringComparison.OrdinalIgnoreCase) => "נביאים",
                LibraryNodeType.Section when string.Equals(sourceKey, "Tanakh/Writings", StringComparison.OrdinalIgnoreCase) => "כתובים",
                LibraryNodeType.Book when !string.IsNullOrWhiteSpace(sourceKey) => _sefariaApiService.GetPreferredBookHebrewTitle(sourceKey),
                LibraryNodeType.Chapter when TryParseIndexedSourceKey(sourceKey, 1, out var chapterNumber) => $"פרק {LibrarySeedFactory.ToHebrewNumeral(chapterNumber)}",
                LibraryNodeType.Verse when TryParseIndexedSourceKey(sourceKey, 2, out var verseNumber) => $"פסוק {LibrarySeedFactory.ToHebrewNumeral(verseNumber)}",
                _ => string.Empty
            };
        }

        if (string.Equals(sourceSystem, "SefariaMishnah", StringComparison.OrdinalIgnoreCase))
        {
            return nodeType switch
            {
                LibraryNodeType.Category when string.Equals(sourceKey, "Mishnah", StringComparison.OrdinalIgnoreCase) => "משנה",
                LibraryNodeType.Section when SubjectAliasMap.TryGetValue(sourceKey, out var sederName) => sederName,
                LibraryNodeType.Book when !string.IsNullOrWhiteSpace(sourceKey) => _sefariaMishnahService.GetPreferredTractateHebrewTitle(sourceKey),
                LibraryNodeType.Chapter when TryParseIndexedSourceKey(sourceKey, 1, out var chapterNumber) => $"פרק {LibrarySeedFactory.ToHebrewNumeral(chapterNumber)}",
                LibraryNodeType.Mishnah when TryParseIndexedSourceKey(sourceKey, 2, out var mishnahNumber) => $"משנה {LibrarySeedFactory.ToHebrewNumeral(mishnahNumber)}",
                _ => string.Empty
            };
        }

        if (string.Equals(sourceSystem, "SefariaRambam", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(sourceKey, "Rambam", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sourceKey, "Mishneh Torah", StringComparison.OrdinalIgnoreCase))
            {
                return "רמב\"ם";
            }

            var parts = sourceKey.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return nodeType switch
            {
                LibraryNodeType.Section when parts.Length >= 3 && parts[1] == "sefer"
                    => _sefariaRambamService.GetPreferredSeferHebrewTitle(parts[2]),
                LibraryNodeType.Book when parts.Length >= 4 && parts[1] == "halakhot"
                    => _sefariaRambamService.GetPreferredHalakhotHebrewTitle(parts[3]),
                LibraryNodeType.Chapter when parts.Length >= 4 && parts[1] == "chapter" && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var chapterNumber)
                    => $"פרק {LibrarySeedFactory.ToHebrewNumeral(chapterNumber)}",
                LibraryNodeType.Halakhah when parts.Length >= 5 && parts[1] == "halakhah" && int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var halakhahNumber)
                    => $"הלכה {LibrarySeedFactory.ToHebrewNumeral(halakhahNumber)}",
                _ => string.Empty
            };
        }

        if (string.Equals(sourceSystem, "SefariaTur", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(sourceKey, "Tur", StringComparison.OrdinalIgnoreCase))
            {
                return "טור";
            }

            var parts = sourceKey.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return nodeType switch
            {
                LibraryNodeType.Section when parts.Length >= 3 && parts[1] == "section"
                    => _sefariaTurService.GetPreferredSectionHebrewTitle(parts[2]),
                LibraryNodeType.Chapter when parts.Length >= 4 && parts[1] == "siman" && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var simanNumber)
                    => $"סימן {LibrarySeedFactory.ToHebrewNumeral(simanNumber)}",
                _ => string.Empty
            };
        }

        if (string.Equals(sourceSystem, "SefariaShulchanArukh", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(sourceKey, "ShulchanArukh", StringComparison.OrdinalIgnoreCase))
            {
                return "שולחן ערוך";
            }

            var parts = sourceKey.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return nodeType switch
            {
                LibraryNodeType.Section when parts.Length >= 3 && parts[1] == "section"
                    => _sefariaShulchanArukhService.GetPreferredSectionHebrewTitle(parts[2]),
                LibraryNodeType.Chapter when parts.Length >= 4 && parts[1] == "siman" && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var simanNumber)
                    => $"סימן {LibrarySeedFactory.ToHebrewNumeral(simanNumber)}",
                LibraryNodeType.Halakhah when parts.Length >= 5 && parts[1] == "seif" && int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seifNumber)
                    => $"סעיף {LibrarySeedFactory.ToHebrewNumeral(seifNumber)}",
                _ => string.Empty
            };
        }

        if (string.Equals(sourceSystem, "SefariaTalmud", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(sourceKey, "Talmud", StringComparison.OrdinalIgnoreCase))
            {
                return "תלמוד";
            }

            if (string.Equals(sourceKey, "Talmud/Bavli", StringComparison.OrdinalIgnoreCase))
            {
                return "בבלי";
            }

            if (string.Equals(sourceKey, "Talmud/Yerushalmi", StringComparison.OrdinalIgnoreCase))
            {
                return "ירושלמי";
            }

            var parts = sourceKey.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return nodeType switch
            {
                LibraryNodeType.Section when parts.Length >= 3 && parts[1] == "seder"
                    => _sefariaTalmudService.GetPreferredSederHebrewTitle(parts[2]),
                LibraryNodeType.Book when parts.Length >= 4 && parts[1] == "tractate"
                    => _sefariaTalmudService.GetPreferredTractateHebrewTitle(parts[0], parts[3]),
                LibraryNodeType.Chapter when parts.Length >= 3 && parts[1] == "chapter" && int.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var chapterNumber)
                    => $"פרק {LibrarySeedFactory.ToHebrewNumeral(chapterNumber)}",
                LibraryNodeType.Page when parts.Length >= 3 && parts[1] == "page"
                    => parts[2],
                LibraryNodeType.Halakhah when parts.Length >= 5 && parts[1] == "halakhah" && int.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var halakhahNumber)
                    => $"הלכה {LibrarySeedFactory.ToHebrewNumeral(halakhahNumber)}",
                _ => string.Empty
            };
        }

        return string.Empty;
    }

    private static bool TryParseIndexedSourceKey(string sourceKey, int index, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            return false;
        }

        var parts = sourceKey.Split('|', StringSplitOptions.TrimEntries);
        return parts.Length > index && int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private DuplicateMergeResult MergeDuplicateSubjects(SqliteConnection connection, SqliteTransaction transaction)
    {
        var result = new DuplicateMergeResult();
        var remap = new Dictionary<int, int>();

        while (true)
        {
            var subjects = LoadSubjects(connection);
            var duplicateGroup = subjects
                .GroupBy(subject => $"{subject.ParentId?.ToString(CultureInfo.InvariantCulture) ?? "root"}|{BuildSubjectDuplicateKey(subject)}", StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1);

            if (duplicateGroup is null)
            {
                break;
            }

            var orderedSubjects = duplicateGroup
                .OrderByDescending(subject => !string.IsNullOrWhiteSpace(subject.SourceSystem) && !string.IsNullOrWhiteSpace(subject.SourceKey))
                .ThenBy(subject => subject.SortOrder)
                .ThenBy(subject => subject.Id)
                .ToArray();
            var canonical = orderedSubjects[0];
            result.DuplicateGroupsResolved++;

            foreach (var duplicate in orderedSubjects.Skip(1))
            {
                result.ReassignedStudyItems += ReassignStudyItems(connection, transaction, duplicate.Id, canonical.Id);
                result.ReassignedPresets += ReassignReviewPresets(connection, transaction, duplicate.Id, canonical.Id);
                ReassignChildSubjects(connection, transaction, duplicate.Id, canonical.Id);
                DeleteSubject(connection, transaction, duplicate.Id);
                remap[duplicate.Id] = canonical.Id;
                result.MergedSubjects++;
            }
        }

        if (remap.Count > 0)
        {
            RewritePausedSessionSubjectIds(connection, transaction, remap);
        }

        return result;
    }

    private static string BuildSubjectDuplicateKey(SubjectNodeModel subject)
    {
        if (!string.IsNullOrWhiteSpace(subject.SourceSystem) && !string.IsNullOrWhiteSpace(subject.SourceKey))
        {
            return $"src|{subject.SourceSystem}|{subject.SourceKey}";
        }

        return $"name|{subject.Name.Trim()}";
    }

    private static int ReassignStudyItems(SqliteConnection connection, SqliteTransaction transaction, int fromSubjectId, int toSubjectId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE StudyItems
            SET SubjectId = $toSubjectId
            WHERE SubjectId = $fromSubjectId;
            """;
        command.Parameters.AddWithValue("$fromSubjectId", fromSubjectId);
        command.Parameters.AddWithValue("$toSubjectId", toSubjectId);
        return command.ExecuteNonQuery();
    }

    private static int ReassignReviewPresets(SqliteConnection connection, SqliteTransaction transaction, int fromSubjectId, int toSubjectId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE ReviewPresets
            SET SubjectId = $toSubjectId
            WHERE SubjectId = $fromSubjectId;
            """;
        command.Parameters.AddWithValue("$fromSubjectId", fromSubjectId);
        command.Parameters.AddWithValue("$toSubjectId", toSubjectId);
        return command.ExecuteNonQuery();
    }

    private static void ReassignChildSubjects(SqliteConnection connection, SqliteTransaction transaction, int fromSubjectId, int toSubjectId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE Subjects
            SET ParentId = $toSubjectId
            WHERE ParentId = $fromSubjectId;
            """;
        command.Parameters.AddWithValue("$fromSubjectId", fromSubjectId);
        command.Parameters.AddWithValue("$toSubjectId", toSubjectId);
        command.ExecuteNonQuery();
    }

    private static void DeleteSubject(SqliteConnection connection, SqliteTransaction transaction, int subjectId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM Subjects WHERE Id = $subjectId;";
        command.Parameters.AddWithValue("$subjectId", subjectId);
        command.ExecuteNonQuery();
    }

    private static void RewritePausedSessionSubjectIds(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyDictionary<int, int> remap)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT Id, Payload
            FROM SavedReviewSessions
            WHERE SessionKind = 'PausedReview';
            """;

        using var reader = command.ExecuteReader();
        var updates = new List<(int Id, string Payload)>();
        while (reader.Read())
        {
            var id = reader.GetInt32(0);
            var payload = reader.GetString(1);
            var state = JsonSerializer.Deserialize<PausedReviewSessionState>(payload);
            if (state is null || !state.SelectedSubjectId.HasValue)
            {
                continue;
            }

            if (!remap.TryGetValue(state.SelectedSubjectId.Value, out var mappedSubjectId))
            {
                continue;
            }

            var updatedState = new PausedReviewSessionState
            {
                Title = state.Title,
                ReturnMode = state.ReturnMode,
                OrderMode = state.OrderMode,
                SelectedSubjectId = mappedSubjectId,
                TotalCount = state.TotalCount,
                CompletedCount = state.CompletedCount,
                FailedCount = state.FailedCount,
                HighRatingCount = state.HighRatingCount,
                LowRatingCount = state.LowRatingCount,
                PendingItemIds = state.PendingItemIds,
                FailedItemIds = state.FailedItemIds,
                ReviewLaterItemIds = state.ReviewLaterItemIds,
                SkippedItemIds = state.SkippedItemIds,
                UpdatedAt = state.UpdatedAt
            };

            updates.Add((id, JsonSerializer.Serialize(updatedState)));
        }

        reader.Close();
        foreach (var update in updates)
        {
            using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = "UPDATE SavedReviewSessions SET Payload = $payload WHERE Id = $id;";
            updateCommand.Parameters.AddWithValue("$id", update.Id);
            updateCommand.Parameters.AddWithValue("$payload", update.Payload);
            updateCommand.ExecuteNonQuery();
        }
    }

    private static List<string> GetRootNames(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT Name
            FROM Subjects
            WHERE ParentId IS NULL
            ORDER BY SortOrder, Id;
            """;

        using var reader = command.ExecuteReader();
        var result = new List<string>();
        while (reader.Read())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    private static int CountDuplicateSiblingGroups(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT ParentId, Name, COUNT(*)
            FROM Subjects
            GROUP BY ParentId, Name
            HAVING COUNT(*) > 1;
            """;

        using var reader = command.ExecuteReader();
        var count = 0;
        while (reader.Read())
        {
            count++;
        }

        return count;
    }

    private static int CountTalmudPages(TalmudAddressing addressing, TalmudChapterMetadata chapter)
    {
        return addressing switch
        {
            TalmudAddressing.Bavli => int.Parse(chapter.EndPage, CultureInfo.InvariantCulture) -
                                     int.Parse(chapter.StartPage, CultureInfo.InvariantCulture) + 1,
            TalmudAddressing.Yerushalmi => CountYerushalmiPages(chapter.StartPage, chapter.EndPage),
            _ => 0
        };
    }

    private static int CountYerushalmiPages(string startPage, string endPage)
    {
        var start = ParseYerushalmiAddress(startPage);
        var end = ParseYerushalmiAddress(endPage);
        var count = 1;
        var current = start;
        while (current != end)
        {
            current = current.Side == 'a'
                ? current with { Side = 'b' }
                : new YerushalmiAddress(current.Folio + 1, 'a');
            count++;
        }

        return count;
    }

    private static YerushalmiAddress ParseYerushalmiAddress(string value)
    {
        var side = value[^1];
        var folioValue = value[..^1];
        return new YerushalmiAddress(int.Parse(folioValue, CultureInfo.InvariantCulture), side);
    }

    private sealed class DuplicateMergeResult
    {
        public int DuplicateGroupsResolved { get; set; }
        public int MergedSubjects { get; set; }
        public int ReassignedStudyItems { get; set; }
        public int ReassignedPresets { get; set; }
    }
}
