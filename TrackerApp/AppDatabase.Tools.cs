using System.Globalization;
using Microsoft.Data.Sqlite;

namespace TrackerApp;

public sealed partial class AppDatabase
{
    public void EnsureSubjectChildrenLoaded(int parentId)
    {
        var parentSubject = _subjects.FirstOrDefault(subject => subject.Id == parentId);
        if (parentSubject is null)
        {
            return;
        }

        var lazyChildren = ResolveLazyChildren(parentSubject);
        if (lazyChildren.Count == 0)
        {
            return;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var sortOrder = 1;
        foreach (var child in lazyChildren)
        {
            EnsureLibraryNode(connection, transaction, parentId, child, sortOrder++);
        }

        transaction.Commit();
        RefreshSubjectCache();
    }

    public IReadOnlyList<SubjectNodeModel> GetTreeChildren(int? parentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT s.Id,
                   s.ParentId,
                   s.Name,
                   s.NodeType,
                   s.SourceSystem,
                   s.SourceKey,
                   s.SortOrder,
                   EXISTS(SELECT 1 FROM Subjects child WHERE child.ParentId = s.Id) AS HasChildren,
                   sup.IsMarkedLearned,
                   sup.NextReviewDate,
                   sup.CurrentStage,
                   sup.CompletedReviewCount,
                   sup.Status,
                   sup.StudyCount
            FROM Subjects s
            LEFT JOIN StudyUnitProgress sup ON sup.SubjectId = s.Id
            WHERE (($parentId IS NULL AND s.ParentId IS NULL) OR s.ParentId = $parentId)
            ORDER BY s.SortOrder, s.Id;
            """;
        command.Parameters.AddWithValue("$parentId", parentId.HasValue ? parentId.Value : DBNull.Value);

        using var reader = command.ExecuteReader();
        var subjects = new List<SubjectNodeModel>();
        while (reader.Read())
        {
            var id = reader.GetInt32(0);
            var displayPath = GetSubjectPath(id);
            var hasDatabaseChildren = reader.GetInt32(7) == 1;
            var progress = reader.IsDBNull(8)
                ? null
                : FinalizeStudyUnitProgress(new StudyUnitProgressModel
                {
                    SubjectId = id,
                    SubjectPath = displayPath,
                    IsMarkedLearned = reader.GetInt32(8) == 1,
                    NextReviewDate = reader.IsDBNull(9) ? null : ParseDate(reader.GetString(9)),
                    CurrentStage = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                    CompletedReviewCount = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                    Status = ParseStudyUnitStatus(reader.IsDBNull(12) ? string.Empty : reader.GetString(12)),
                    StudyCount = reader.IsDBNull(13) ? 0 : reader.GetInt32(13)
                });
            subjects.Add(new SubjectNodeModel
            {
                Id = id,
                ParentId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                Name = reader.GetString(2),
                SortOrder = reader.GetInt32(6),
                DisplayPath = displayPath,
                HasChildren = hasDatabaseChildren || HasPotentialLazyChildren(new SubjectNodeModel
                {
                    Id = id,
                    ParentId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    Name = reader.GetString(2),
                    DisplayPath = displayPath,
                    SortOrder = reader.GetInt32(6),
                    NodeType = ParseLibraryNodeType(reader.GetString(3)),
                    SourceSystem = reader.GetString(4),
                    SourceKey = reader.GetString(5)
                }),
                NodeType = ParseLibraryNodeType(reader.GetString(3)),
                SourceSystem = reader.GetString(4),
                SourceKey = reader.GetString(5),
                UnitStatus = progress?.Status ?? StudyUnitStatus.NotStudied,
                UnitNextReviewDate = progress?.NextReviewDate,
                UnitCurrentStage = progress?.CurrentStage ?? 0,
                UnitCompletedReviews = progress?.CompletedReviewCount ?? 0,
                UnitStatusText = TranslateStudyUnitStatus(progress)
            });
        }

        return subjects;
    }

    public IReadOnlyList<SubjectNodeModel> GetSubjectLineage(int subjectId)
    {
        var lookup = _subjects.ToDictionary(subject => subject.Id);
        if (!lookup.TryGetValue(subjectId, out var current))
        {
            return [];
        }

        var lineage = new Stack<SubjectNodeModel>();
        while (true)
        {
            lineage.Push(current);
            if (!current.ParentId.HasValue || !lookup.TryGetValue(current.ParentId.Value, out current))
            {
                break;
            }
        }

        return lineage.ToList();
    }

    public int ImportFromCsv(string filePath)
    {
        var rows = CsvUtility.ReadRows(filePath);
        if (rows.Count == 0)
        {
            return 0;
        }

        var header = rows[0];
        var hasHeader = header.Any(value => value.Equals("SubjectPath", StringComparison.OrdinalIgnoreCase));
        var bodyRows = hasHeader ? rows.Skip(1) : rows;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var imported = 0;
        foreach (var row in bodyRows)
        {
            if (row.Length == 0 || row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var subjectPath = GetCell(row, header, hasHeader, 0, "SubjectPath");
            var topic = GetCell(row, header, hasHeader, 1, "Topic");
            var sourceText = GetCell(row, header, hasHeader, 2, "SourceText");
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                sourceText = GetCell(row, header, hasHeader, 2, "Question");
            }

            var pshatText = GetCell(row, header, hasHeader, 4, "PshatText");
            var kushyaText = GetCell(row, header, hasHeader, 5, "KushyaText");
            var terutzText = GetCell(row, header, hasHeader, 6, "TerutzText");
            var chidushText = GetCell(row, header, hasHeader, 7, "ChidushText");
            var reviewNotes = GetCell(row, header, hasHeader, 8, "ReviewNotes");

            var personalSummary = GetCell(row, header, hasHeader, 3, "PersonalSummary");
            if (string.IsNullOrWhiteSpace(personalSummary))
            {
                personalSummary = GetCell(row, header, hasHeader, 3, "Answer");
            }

            var hasStudyContent =
                !string.IsNullOrWhiteSpace(sourceText) ||
                !string.IsNullOrWhiteSpace(pshatText) ||
                !string.IsNullOrWhiteSpace(kushyaText) ||
                !string.IsNullOrWhiteSpace(terutzText) ||
                !string.IsNullOrWhiteSpace(chidushText) ||
                !string.IsNullOrWhiteSpace(personalSummary) ||
                !string.IsNullOrWhiteSpace(reviewNotes);

            if (string.IsNullOrWhiteSpace(subjectPath) ||
                string.IsNullOrWhiteSpace(topic) ||
                !hasStudyContent)
            {
                continue;
            }

            var subjectId = EnsureSubjectPath(connection, transaction, SplitPath(subjectPath));
            InsertStudyItem(
                connection,
                transaction,
                new StudyItemDraftModel
                {
                    SubjectId = subjectId,
                    Topic = topic,
                    SourceText = sourceText,
                    PshatText = pshatText,
                    KushyaText = kushyaText,
                    TerutzText = terutzText,
                    ChidushText = chidushText,
                    PersonalSummary = personalSummary,
                    ReviewNotes = reviewNotes
                },
                DateTime.Now);
            imported++;
        }

        transaction.Commit();
        RefreshSubjectCache();
        return imported;
    }

    public void ExportToCsv(string filePath)
    {
        var rows = new List<string[]>
        {
            new[]
            {
                "Id", "SubjectPath", "Topic", "SourceText", "PshatText", "KushyaText", "TerutzText", "ChidushText", "PersonalSummary", "ReviewNotes",
                "DueDate", "EaseFactor", "IntervalDays", "RepetitionCount", "LastRating"
            }
        };

        foreach (var item in GetStudyItemsForSubject(null))
        {
            rows.Add(
                new[]
                {
                    item.Id.ToString(CultureInfo.InvariantCulture),
                    item.SubjectPath,
                    item.Topic,
                    item.SourceText,
                    item.PshatText,
                    item.KushyaText,
                    item.TerutzText,
                    item.ChidushText,
                    item.PersonalSummary,
                    item.ReviewNotes,
                    item.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    item.EaseFactor.ToString("0.00", CultureInfo.InvariantCulture),
                    item.IntervalDays.ToString("0.##", CultureInfo.InvariantCulture),
                    item.RepetitionCount.ToString(CultureInfo.InvariantCulture),
                    item.LastRating
                });
        }

        CsvUtility.WriteRows(filePath, rows);
    }

    public void ExportPrintableHtml(DateTime startDate, DateTime endDate, string filePath)
    {
        PrintExportService.ExportHtml(filePath, startDate, endDate, GetPrintableItems(startDate, endDate));
    }

    public DashboardStats GetDashboardStats()
    {
        var allItems = GetStudyItemsForSubject(null);
        var today = DateTime.Today;

        using var connection = OpenConnection();
        var completedToday = ExecuteScalarCount(connection,
            "SELECT COUNT(*) FROM ReviewHistory WHERE date(ReviewedAt) = date('now', 'localtime');");
        var retentionTotal = ExecuteScalarCount(connection, "SELECT COUNT(*) FROM ReviewHistory;");
        var retentionSuccess = ExecuteScalarCount(connection, "SELECT COUNT(*) FROM ReviewHistory WHERE WasSuccessful = 1;");

        var timeline = new List<ReviewTimelinePoint>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT date(ReviewedAt), AVG(Score), COUNT(*)
                FROM ReviewHistory
                GROUP BY date(ReviewedAt)
                ORDER BY date(ReviewedAt);
                """;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                timeline.Add(new ReviewTimelinePoint
                {
                    Day = ParseDate(reader.GetString(0)),
                    AverageScore = reader.IsDBNull(1) ? 0 : reader.GetDouble(1),
                    ReviewCount = reader.GetInt32(2)
                });
            }
        }

        return new DashboardStats
        {
            TotalItems = allItems.Count,
            DueToday = allItems.Count(item => item.DueDate.Date <= today),
            CompletedToday = completedToday,
            MasteredItems = allItems.Count(item => item.IsMastered),
            RetentionRate = retentionTotal == 0 ? 0 : retentionSuccess * 100.0 / retentionTotal,
            ItemsBySubject = allItems
                .GroupBy(item => item.RootCategory)
                .Select(group => new SubjectCountModel { Name = group.Key, Count = group.Count() })
                .OrderByDescending(group => group.Count)
                .ToList(),
            DueBySubject = allItems
                .Where(item => item.DueDate.Date <= today)
                .GroupBy(item => item.RootCategory)
                .Select(group => new SubjectCountModel { Name = group.Key, Count = group.Count() })
                .OrderByDescending(group => group.Count)
                .ToList(),
            MasteredByCategory = allItems
                .Where(item => item.IsMastered)
                .GroupBy(item => item.RootCategory)
                .Select(group => new SubjectCountModel { Name = group.Key, Count = group.Count() })
                .OrderByDescending(group => group.Count)
                .ToList(),
            ReviewTimeline = timeline.TakeLast(21).ToList(),
            Heatmap = GetHeatmapData(connection, 84)
        };
    }

    private void RefreshSubjectCache()
    {
        using var connection = OpenConnection();
        _subjects = LoadSubjects(connection);
    }

    private StudyItemModel LoadStudyItemForUpdate(SqliteConnection connection, SqliteTransaction transaction, int itemId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {StudyItemSelectColumns} FROM StudyItems WHERE Id = $itemId;";
        command.Parameters.AddWithValue("$itemId", itemId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Study item {itemId} was not found.");
        }

        return MapStudyItem(reader);
    }

    private void InsertReviewHistory(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int itemId,
        StudyItemModel currentItem,
        ReviewResult result,
        ReviewRating rating,
        DateTime reviewedAt)
    {
        using var historyCommand = connection.CreateCommand();
        historyCommand.Transaction = transaction;
        historyCommand.CommandText =
            """
            INSERT INTO ReviewHistory (
                StudyItemId, ReviewedAt, Rating, Score, WasSuccessful,
                EaseFactorBefore, EaseFactorAfter, IntervalBefore, IntervalAfter
            )
            VALUES (
                $studyItemId, $reviewedAt, $rating, $score, $wasSuccessful,
                $easeFactorBefore, $easeFactorAfter, $intervalBefore, $intervalAfter
            );
            """;
        historyCommand.Parameters.AddWithValue("$studyItemId", itemId);
        historyCommand.Parameters.AddWithValue("$reviewedAt", FormatDate(reviewedAt));
        historyCommand.Parameters.AddWithValue("$rating", rating.ToString());
        historyCommand.Parameters.AddWithValue("$score", (int)rating);
        historyCommand.Parameters.AddWithValue("$wasSuccessful", result.IsSuccessful ? 1 : 0);
        historyCommand.Parameters.AddWithValue("$easeFactorBefore", currentItem.EaseFactor);
        historyCommand.Parameters.AddWithValue("$easeFactorAfter", result.NextEaseFactor);
        historyCommand.Parameters.AddWithValue("$intervalBefore", currentItem.IntervalDays);
        historyCommand.Parameters.AddWithValue("$intervalAfter", result.NextIntervalDays);
        historyCommand.ExecuteNonQuery();
    }

    private StudyItemModel MapStudyItem(SqliteDataReader reader)
    {
        var subjectId = reader.GetInt32(1);
        return new StudyItemModel
        {
            Id = reader.GetInt32(0),
            SubjectId = subjectId,
            SubjectPath = GetSubjectPath(subjectId),
            RootCategory = GetRootCategory(subjectId),
            Topic = reader.GetString(2),
            SourceText = reader.IsDBNull(5) ? reader.GetString(3) : reader.GetString(5),
            PshatText = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            KushyaText = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
            TerutzText = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
            ChidushText = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
            PersonalSummary = reader.IsDBNull(10) ? reader.GetString(4) : reader.GetString(10),
            ReviewNotes = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
            CreatedAt = ParseDate(reader.GetString(12)),
            ModifiedAt = ParseDate(reader.GetString(13)),
            DueDate = ParseDate(reader.GetString(14)),
            Level = reader.GetInt32(15),
            TotalReviews = reader.GetInt32(16),
            RepetitionCount = reader.GetInt32(17),
            Lapses = reader.GetInt32(18),
            EaseFactor = reader.GetDouble(19),
            IntervalDays = reader.GetDouble(20),
            LastRating = reader.GetString(21),
            LastReviewedAt = reader.IsDBNull(22) ? null : ParseDate(reader.GetString(22)),
            ManualDifficulty = reader.IsDBNull(23) ? string.Empty : reader.GetString(23)
        };
    }

    private List<SubjectNodeModel> LoadSubjects(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, ParentId, Name, NodeType, SourceSystem, SourceKey, SortOrder
            FROM Subjects
            ORDER BY COALESCE(ParentId, 0), SortOrder, Id;
            """;

        using var reader = command.ExecuteReader();
        var subjects = new List<SubjectNodeModel>();
        while (reader.Read())
        {
            var id = reader.GetInt32(0);
            subjects.Add(new SubjectNodeModel
            {
                Id = id,
                ParentId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                Name = reader.GetString(2),
                NodeType = ParseLibraryNodeType(reader.GetString(3)),
                SourceSystem = reader.GetString(4),
                SourceKey = reader.GetString(5),
                SortOrder = reader.GetInt32(6),
                DisplayPath = string.Empty,
                HasChildren = false
            });
        }

        return subjects;
    }

    private List<PrintableScheduleItem> GetPrintableItems(DateTime startDate, DateTime endDate)
    {
        return GetStudyItemsForSubject(null)
            .Where(item => item.DueDate.Date >= startDate.Date && item.DueDate.Date <= endDate.Date)
            .Select(item => new PrintableScheduleItem
            {
                SubjectPath = item.SubjectPath,
                Topic = item.Topic,
                SourceText = item.SourceText,
                PshatText = item.PshatText,
                KushyaText = item.KushyaText,
                TerutzText = item.TerutzText,
                ChidushText = item.ChidushText,
                PersonalSummary = item.PersonalSummary,
                ReviewNotes = item.ReviewNotes,
                DueDate = item.DueDate
            })
            .ToList();
    }

    private List<HeatmapDayModel> GetHeatmapData(SqliteConnection connection, int daysBack)
    {
        var counts = new Dictionary<DateTime, int>();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT date(ReviewedAt), COUNT(*)
            FROM ReviewHistory
            WHERE date(ReviewedAt) >= date('now', '-' || $days || ' day')
            GROUP BY date(ReviewedAt);
            """;
        command.Parameters.AddWithValue("$days", daysBack);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            counts[ParseDate(reader.GetString(0)).Date] = reader.GetInt32(1);
        }

        var start = DateTime.Today.AddDays(-(daysBack - 1));
        var result = new List<HeatmapDayModel>();
        for (var day = start.Date; day <= DateTime.Today; day = day.AddDays(1))
        {
            counts.TryGetValue(day, out var reviewCount);
            result.Add(new HeatmapDayModel { Day = day, ReviewCount = reviewCount });
        }

        return result;
    }

    private string GetRootCategory(int subjectId)
    {
        var path = GetSubjectPath(subjectId);
        return path.Split(" > ", StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "ללא קטגוריה";
    }

    private static int ExecuteScalarCount(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static int GetLastInsertRowId(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static IEnumerable<string> SplitPath(string subjectPath)
    {
        return subjectPath
            .Split(new[] { '>', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string GetCell(string[] row, string[] header, bool hasHeader, int fallbackIndex, string columnName)
    {
        if (!hasHeader)
        {
            return fallbackIndex < row.Length ? row[fallbackIndex] : string.Empty;
        }

        for (var index = 0; index < header.Length; index++)
        {
            if (header[index].Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return index < row.Length ? row[index] : string.Empty;
            }
        }

        return string.Empty;
    }

    private static string FormatDate(DateTime value) => value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static DateTime ParseDate(string value) => DateTime.Parse(value, CultureInfo.InvariantCulture);

    private IReadOnlyList<LibrarySeedNode> ResolveLazyChildren(SubjectNodeModel parentSubject)
    {
        if (string.Equals(parentSubject.SourceSystem, "Sefaria", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveSefariaChildren(parentSubject);
        }

        if (string.Equals(parentSubject.SourceSystem, "SefariaMishnah", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveMishnahChildren(parentSubject);
        }

        if (string.Equals(parentSubject.SourceSystem, "SefariaRambam", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveRambamChildren(parentSubject);
        }

        if (string.Equals(parentSubject.SourceSystem, "SefariaTur", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveTurChildren(parentSubject);
        }

        if (string.Equals(parentSubject.SourceSystem, "SefariaShulchanArukh", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveShulchanArukhChildren(parentSubject);
        }

        if (string.Equals(parentSubject.SourceSystem, "SefariaTalmud", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveTalmudChildren(parentSubject);
        }

        var path = GetSubjectPath(parentSubject.Id);
        return LibrarySeedFactory.GetLazyChildren(SplitPath(path).ToArray());
    }

    private IReadOnlyList<LibrarySeedNode> ResolveSefariaChildren(SubjectNodeModel parentSubject)
    {
        return parentSubject.NodeType switch
        {
            LibraryNodeType.Section => _sefariaApiService.GetBooksForSection(parentSubject.Name)
                .Select(book => new LibrarySeedNode(book.HebrewTitle, [], LibraryNodeType.Book, "Sefaria", book.EnglishTitle))
                .ToArray(),
            LibraryNodeType.Book => BuildTanakhChapterNodes(parentSubject.SourceKey),
            LibraryNodeType.Chapter => BuildTanakhVerseNodes(parentSubject.SourceKey),
            _ => []
        };
    }

    private IReadOnlyList<LibrarySeedNode> ResolveMishnahChildren(SubjectNodeModel parentSubject)
    {
        return parentSubject.NodeType switch
        {
            LibraryNodeType.Category => _sefariaMishnahService.GetSedarim()
                .Select(seder => new LibrarySeedNode(seder.HebrewTitle, [], LibraryNodeType.Section, "SefariaMishnah", seder.EnglishCategory))
                .ToArray(),
            LibraryNodeType.Section => _sefariaMishnahService.GetTractatesForSeder(parentSubject.SourceKey)
                .Select(tractate => new LibrarySeedNode(tractate.HebrewTitle, [], LibraryNodeType.Book, "SefariaMishnah", tractate.EnglishTitle))
                .ToArray(),
            LibraryNodeType.Book => BuildMishnahChapterNodes(parentSubject.SourceKey),
            LibraryNodeType.Chapter => BuildMishnahUnitNodes(parentSubject.SourceKey),
            _ => []
        };
    }

    private IReadOnlyList<LibrarySeedNode> ResolveRambamChildren(SubjectNodeModel parentSubject)
    {
        var rambamKey = ResolveRambamContext(parentSubject);
        return parentSubject.NodeType switch
        {
            LibraryNodeType.Category when string.Equals(parentSubject.SourceKey, "Rambam", StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(parentSubject.SourceKey, "Mishneh Torah", StringComparison.OrdinalIgnoreCase)
                => _sefariaRambamService.GetSefarim()
                    .Select(sefer => new LibrarySeedNode(
                        sefer.HebrewTitle,
                        [],
                        LibraryNodeType.Section,
                        "SefariaRambam",
                        $"Rambam|sefer|{sefer.EnglishCategory}"))
                    .ToArray(),
            LibraryNodeType.Section when rambamKey.EnglishSefer is not null
                => _sefariaRambamService.GetHalakhotForSefer(rambamKey.EnglishSefer)
                    .Select(halakhot => new LibrarySeedNode(
                        halakhot.HebrewTitle,
                        [],
                        LibraryNodeType.Book,
                        "SefariaRambam",
                        $"Rambam|halakhot|{halakhot.EnglishSefer}|{halakhot.EnglishTitle}"))
                    .ToArray(),
            LibraryNodeType.Book when rambamKey.EnglishHalakhot is not null
                => BuildRambamChapterNodes(rambamKey.EnglishHalakhot),
            LibraryNodeType.Chapter when rambamKey.EnglishHalakhot is not null && rambamKey.ChapterNumber.HasValue
                => BuildRambamHalakhahNodes(rambamKey.EnglishHalakhot, rambamKey.ChapterNumber.Value),
            _ => []
        };
    }

    private IReadOnlyList<LibrarySeedNode> ResolveTurChildren(SubjectNodeModel parentSubject)
    {
        var turKey = ResolveTurContext(parentSubject);
        return parentSubject.NodeType switch
        {
            LibraryNodeType.Category when string.Equals(parentSubject.SourceKey, "Tur", StringComparison.OrdinalIgnoreCase)
                => _sefariaTurService.GetSections()
                    .Select(section => new LibrarySeedNode(
                        section.HebrewTitle,
                        [],
                        LibraryNodeType.Section,
                        "SefariaTur",
                        $"Tur|section|{section.EnglishTitle}"))
                    .ToArray(),
            LibraryNodeType.Section when !string.IsNullOrWhiteSpace(turKey.EnglishSection)
                => BuildTurSimanNodes(turKey.EnglishSection),
            _ => []
        };
    }

    private IReadOnlyList<LibrarySeedNode> ResolveShulchanArukhChildren(SubjectNodeModel parentSubject)
    {
        var shulchanArukhKey = ResolveShulchanArukhContext(parentSubject);
        return parentSubject.NodeType switch
        {
            LibraryNodeType.Category when string.Equals(parentSubject.SourceKey, "ShulchanArukh", StringComparison.OrdinalIgnoreCase)
                => _sefariaShulchanArukhService.GetSections()
                    .Select(section => new LibrarySeedNode(
                        section.HebrewTitle,
                        [],
                        LibraryNodeType.Section,
                        "SefariaShulchanArukh",
                        $"ShulchanArukh|section|{section.EnglishTitle}"))
                    .ToArray(),
            LibraryNodeType.Section when !string.IsNullOrWhiteSpace(shulchanArukhKey.EnglishSection)
                => BuildShulchanArukhSimanNodes(shulchanArukhKey.EnglishSection),
            LibraryNodeType.Chapter when !string.IsNullOrWhiteSpace(shulchanArukhKey.EnglishSection) && shulchanArukhKey.SimanNumber.HasValue
                => BuildShulchanArukhSeifNodes(shulchanArukhKey.EnglishSection, shulchanArukhKey.SimanNumber.Value),
            _ => []
        };
    }

    private IReadOnlyList<LibrarySeedNode> ResolveTalmudChildren(SubjectNodeModel parentSubject)
    {
        var talmudKey = ResolveTalmudContext(parentSubject);
        return parentSubject.NodeType switch
        {
            LibraryNodeType.Category when string.Equals(parentSubject.SourceKey, "Talmud", StringComparison.OrdinalIgnoreCase)
                => _sefariaTalmudService.GetCollections()
                    .Select(collection => new LibrarySeedNode(
                        collection.HebrewTitle,
                        [],
                        LibraryNodeType.Category,
                        "SefariaTalmud",
                        $"Talmud/{collection.CollectionKey}"))
                    .ToArray(),
            LibraryNodeType.Category when talmudKey.CollectionKey is not null
                => _sefariaTalmudService.GetSedarim(talmudKey.CollectionKey)
                    .Select(seder => new LibrarySeedNode(
                        seder.HebrewTitle,
                        [],
                        LibraryNodeType.Section,
                        "SefariaTalmud",
                        $"{talmudKey.CollectionKey}|seder|{seder.EnglishCategory}"))
                    .ToArray(),
            LibraryNodeType.Section when talmudKey.CollectionKey is not null && talmudKey.EnglishSeder is not null
                => _sefariaTalmudService.GetTractates(talmudKey.CollectionKey, talmudKey.EnglishSeder)
                    .Select(tractate => new LibrarySeedNode(
                        tractate.HebrewTitle,
                        [],
                        LibraryNodeType.Book,
                        "SefariaTalmud",
                        $"{talmudKey.CollectionKey}|tractate|{tractate.EnglishSeder}|{tractate.EnglishTitle}"))
                    .ToArray(),
            LibraryNodeType.Book when string.Equals(talmudKey.CollectionKey, "Bavli", StringComparison.OrdinalIgnoreCase)
                => BuildBavliChapterNodes(GetSubjectPath(parentSubject.Id)),
            LibraryNodeType.Book when string.Equals(talmudKey.CollectionKey, "Yerushalmi", StringComparison.OrdinalIgnoreCase) && talmudKey.EnglishTractate is not null
                => BuildYerushalmiChapterNodes(talmudKey.EnglishTractate),
            LibraryNodeType.Chapter when string.Equals(talmudKey.CollectionKey, "Bavli", StringComparison.OrdinalIgnoreCase)
                => BuildBavliPageNodes(GetSubjectPath(parentSubject.Id)),
            LibraryNodeType.Chapter when string.Equals(talmudKey.CollectionKey, "Yerushalmi", StringComparison.OrdinalIgnoreCase) && talmudKey.EnglishTractate is not null && talmudKey.ChapterNumber.HasValue
                => BuildYerushalmiHalakhahNodes(talmudKey.EnglishTractate, talmudKey.ChapterNumber.Value),
            _ => []
        };
    }

    private IReadOnlyList<LibrarySeedNode> BuildTanakhChapterNodes(string englishBookTitle)
    {
        var shape = _sefariaApiService.GetBookShape(englishBookTitle);
        var chapters = new List<LibrarySeedNode>();
        for (var chapterNumber = 1; chapterNumber <= shape.VerseCountsByChapter.Count; chapterNumber++)
        {
            chapters.Add(new LibrarySeedNode(
                $"פרק {LibrarySeedFactory.ToHebrewNumeral(chapterNumber)}",
                [],
                LibraryNodeType.Chapter,
                "Sefaria",
                $"{englishBookTitle}|{chapterNumber}"));
        }

        return chapters;
    }

    private IReadOnlyList<LibrarySeedNode> BuildTanakhVerseNodes(string chapterKey)
    {
        var parts = chapterKey.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var chapterNumber))
        {
            throw new InvalidOperationException($"מפתח פרק לא תקין: {chapterKey}");
        }

        var verseCount = _sefariaApiService.GetVerseCountForChapter(parts[0], chapterNumber);
        var verses = new List<LibrarySeedNode>();
        for (var verseNumber = 1; verseNumber <= verseCount; verseNumber++)
        {
            verses.Add(new LibrarySeedNode(
                $"פסוק {LibrarySeedFactory.ToHebrewNumeral(verseNumber)}",
                [],
                LibraryNodeType.Verse,
                "Sefaria",
                $"{parts[0]}|{chapterNumber}|{verseNumber}"));
        }

        return verses;
    }

    private IReadOnlyList<LibrarySeedNode> BuildMishnahChapterNodes(string englishTractateTitle)
    {
        var shape = _sefariaMishnahService.GetTractateShape(englishTractateTitle);
        var chapters = new List<LibrarySeedNode>();
        for (var chapterNumber = 1; chapterNumber <= shape.MishnahCountsByChapter.Count; chapterNumber++)
        {
            chapters.Add(new LibrarySeedNode(
                $"פרק {LibrarySeedFactory.ToHebrewNumeral(chapterNumber)}",
                [],
                LibraryNodeType.Chapter,
                "SefariaMishnah",
                $"{englishTractateTitle}|{chapterNumber}"));
        }

        return chapters;
    }

    private IReadOnlyList<LibrarySeedNode> BuildMishnahUnitNodes(string chapterKey)
    {
        var parts = chapterKey.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var chapterNumber))
        {
            throw new InvalidOperationException($"מפתח פרק משנה לא תקין: {chapterKey}");
        }

        var shape = _sefariaMishnahService.GetTractateShape(parts[0]);
        if (chapterNumber < 1 || chapterNumber > shape.MishnahCountsByChapter.Count)
        {
            throw new InvalidOperationException($"מספר פרק משנה לא תקין: {chapterKey}");
        }

        var count = shape.MishnahCountsByChapter[chapterNumber - 1];
        var mishnayot = new List<LibrarySeedNode>();
        for (var mishnahNumber = 1; mishnahNumber <= count; mishnahNumber++)
        {
            mishnayot.Add(new LibrarySeedNode(
                $"משנה {LibrarySeedFactory.ToHebrewNumeral(mishnahNumber)}",
                [],
                LibraryNodeType.Mishnah,
                "SefariaMishnah",
                $"{parts[0]}|{chapterNumber}|{mishnahNumber}"));
        }

        return mishnayot;
    }

    private IReadOnlyList<LibrarySeedNode> BuildRambamChapterNodes(string englishHalakhotTitle)
    {
        var shape = _sefariaRambamService.GetHalakhotShape(englishHalakhotTitle);
        var chapters = new List<LibrarySeedNode>();
        for (var chapterNumber = 1; chapterNumber <= shape.HalakhotByChapter.Count; chapterNumber++)
        {
            chapters.Add(new LibrarySeedNode(
                $"פרק {LibrarySeedFactory.ToHebrewNumeral(chapterNumber)}",
                [],
                LibraryNodeType.Chapter,
                "SefariaRambam",
                $"Rambam|chapter|{englishHalakhotTitle}|{chapterNumber}"));
        }

        return chapters;
    }

    private IReadOnlyList<LibrarySeedNode> BuildRambamHalakhahNodes(string englishHalakhotTitle, int chapterNumber)
    {
        var shape = _sefariaRambamService.GetHalakhotShape(englishHalakhotTitle);
        if (chapterNumber < 1 || chapterNumber > shape.HalakhotByChapter.Count)
        {
            throw new InvalidOperationException($"מספר פרק רמב״ם לא תקין: {englishHalakhotTitle}|{chapterNumber}");
        }

        var count = shape.HalakhotByChapter[chapterNumber - 1];
        var halakhot = new List<LibrarySeedNode>();
        for (var halakhahNumber = 1; halakhahNumber <= count; halakhahNumber++)
        {
            halakhot.Add(new LibrarySeedNode(
                $"הלכה {LibrarySeedFactory.ToHebrewNumeral(halakhahNumber)}",
                [],
                LibraryNodeType.Halakhah,
                "SefariaRambam",
                $"Rambam|halakhah|{englishHalakhotTitle}|{chapterNumber}|{halakhahNumber}"));
        }

        return halakhot;
    }

    private IReadOnlyList<LibrarySeedNode> BuildTurSimanNodes(string englishSectionTitle)
    {
        var shape = _sefariaTurService.GetSectionShape(englishSectionTitle);
        var simanim = new List<LibrarySeedNode>();
        for (var simanNumber = 1; simanNumber <= shape.UnitCountsBySiman.Count; simanNumber++)
        {
            simanim.Add(new LibrarySeedNode(
                $"סימן {LibrarySeedFactory.ToHebrewNumeral(simanNumber)}",
                [],
                LibraryNodeType.Chapter,
                "SefariaTur",
                $"Tur|siman|{englishSectionTitle}|{simanNumber}"));
        }

        return simanim;
    }

    private IReadOnlyList<LibrarySeedNode> BuildShulchanArukhSimanNodes(string englishSectionTitle)
    {
        var shape = _sefariaShulchanArukhService.GetSectionShape(englishSectionTitle);
        var simanim = new List<LibrarySeedNode>();
        for (var simanNumber = 1; simanNumber <= shape.UnitCountsBySiman.Count; simanNumber++)
        {
            simanim.Add(new LibrarySeedNode(
                $"סימן {LibrarySeedFactory.ToHebrewNumeral(simanNumber)}",
                [],
                LibraryNodeType.Chapter,
                "SefariaShulchanArukh",
                $"ShulchanArukh|siman|{englishSectionTitle}|{simanNumber}"));
        }

        return simanim;
    }

    private IReadOnlyList<LibrarySeedNode> BuildShulchanArukhSeifNodes(string englishSectionTitle, int simanNumber)
    {
        var shape = _sefariaShulchanArukhService.GetSectionShape(englishSectionTitle);
        if (simanNumber < 1 || simanNumber > shape.UnitCountsBySiman.Count)
        {
            throw new InvalidOperationException($"מספר סימן שולחן ערוך לא תקין: {englishSectionTitle}|{simanNumber}");
        }

        var count = shape.UnitCountsBySiman[simanNumber - 1];
        var seifim = new List<LibrarySeedNode>();
        for (var seifNumber = 1; seifNumber <= count; seifNumber++)
        {
            seifim.Add(new LibrarySeedNode(
                $"סעיף {LibrarySeedFactory.ToHebrewNumeral(seifNumber)}",
                [],
                LibraryNodeType.Halakhah,
                "SefariaShulchanArukh",
                $"ShulchanArukh|seif|{englishSectionTitle}|{simanNumber}|{seifNumber}"));
        }

        return seifim;
    }

    private IReadOnlyList<LibrarySeedNode> BuildBavliChapterNodes(string tractatePath)
    {
        var pathSegments = SplitPath(tractatePath).ToArray();
        var lazyChildren = LibrarySeedFactory.GetLazyChildren(pathSegments);
        return lazyChildren
            .Select(chapter => chapter with
            {
                SourceSystem = "SefariaTalmud",
                SourceKey = $"Bavli|chapter|{ParseChapterNumberFromName(chapter.Name)}"
            })
            .ToArray();
    }

    private IReadOnlyList<LibrarySeedNode> BuildBavliPageNodes(string chapterPath)
    {
        var pathSegments = SplitPath(chapterPath).ToArray();
        var lazyChildren = LibrarySeedFactory.GetLazyChildren(pathSegments);
        return lazyChildren
            .Select(page => new LibrarySeedNode(page.Name, [], LibraryNodeType.Page, "SefariaTalmud", $"Bavli|page|{page.Name}"))
            .ToArray();
    }

    private IReadOnlyList<LibrarySeedNode> BuildYerushalmiChapterNodes(string englishTractateTitle)
    {
        var shape = _sefariaTalmudService.GetYerushalmiShape(englishTractateTitle);
        var chapters = new List<LibrarySeedNode>();
        for (var chapterNumber = 1; chapterNumber <= shape.HalakhotByChapter.Count; chapterNumber++)
        {
            chapters.Add(new LibrarySeedNode(
                $"פרק {LibrarySeedFactory.ToHebrewNumeral(chapterNumber)}",
                [],
                LibraryNodeType.Chapter,
                "SefariaTalmud",
                $"Yerushalmi|chapter|{englishTractateTitle}|{chapterNumber}"));
        }

        return chapters;
    }

    private IReadOnlyList<LibrarySeedNode> BuildYerushalmiHalakhahNodes(string englishTractateTitle, int chapterNumber)
    {
        var shape = _sefariaTalmudService.GetYerushalmiShape(englishTractateTitle);
        if (chapterNumber < 1 || chapterNumber > shape.HalakhotByChapter.Count)
        {
            throw new InvalidOperationException($"מספר פרק ירושלמי לא תקין: {englishTractateTitle}|{chapterNumber}");
        }

        var halakhot = new List<LibrarySeedNode>();
        var count = shape.HalakhotByChapter[chapterNumber - 1];
        for (var halakhahNumber = 1; halakhahNumber <= count; halakhahNumber++)
        {
            halakhot.Add(new LibrarySeedNode(
                $"הלכה {LibrarySeedFactory.ToHebrewNumeral(halakhahNumber)}",
                [],
                LibraryNodeType.Halakhah,
                "SefariaTalmud",
                $"Yerushalmi|halakhah|{englishTractateTitle}|{chapterNumber}|{halakhahNumber}"));
        }

        return halakhot;
    }

    private (string? EnglishSefer, string? EnglishHalakhot, int? ChapterNumber) ResolveRambamContext(SubjectNodeModel subject)
    {
        var parsed = ParseRambamSourceKey(subject.SourceKey);
        var pathSegments = SplitPath(GetSubjectPath(subject.Id)).ToArray();

        var englishSefer = parsed.EnglishSefer;
        if (!string.IsNullOrWhiteSpace(englishSefer) &&
            !_sefariaRambamService.GetSefarim().Any(sefer => string.Equals(sefer.EnglishCategory, englishSefer, StringComparison.Ordinal)))
        {
            englishSefer = null;
        }

        if (string.IsNullOrWhiteSpace(englishSefer) && pathSegments.Length >= 2)
        {
            englishSefer = _sefariaRambamService.GetSefarim()
                .FirstOrDefault(sefer => string.Equals(sefer.HebrewTitle, pathSegments[1], StringComparison.Ordinal))
                ?.EnglishCategory;
        }

        var englishHalakhot = parsed.EnglishHalakhot;
        if (!string.IsNullOrWhiteSpace(englishSefer) &&
            !string.IsNullOrWhiteSpace(englishHalakhot) &&
            !_sefariaRambamService.GetHalakhotForSefer(englishSefer)
                .Any(halakhot => string.Equals(halakhot.EnglishTitle, englishHalakhot, StringComparison.Ordinal)))
        {
            englishHalakhot = null;
        }

        if (string.IsNullOrWhiteSpace(englishHalakhot) && !string.IsNullOrWhiteSpace(englishSefer) && pathSegments.Length >= 3)
        {
            englishHalakhot = _sefariaRambamService.GetHalakhotForSefer(englishSefer)
                .FirstOrDefault(halakhot => string.Equals(halakhot.HebrewTitle, pathSegments[2], StringComparison.Ordinal))
                ?.EnglishTitle;
        }

        var chapterNumber = parsed.ChapterNumber;
        if (!chapterNumber.HasValue && pathSegments.Length >= 4)
        {
            var parsedChapter = ParseChapterNumberFromName(pathSegments[3]);
            chapterNumber = parsedChapter > 0 ? parsedChapter : null;
        }

        return (englishSefer, englishHalakhot, chapterNumber);
    }

    private (string? EnglishSection, int? SimanNumber) ResolveTurContext(SubjectNodeModel subject)
    {
        var parsed = ParseTurSourceKey(subject.SourceKey);
        var pathSegments = SplitPath(GetSubjectPath(subject.Id)).ToArray();

        var englishSection = parsed.EnglishSection;
        if (!string.IsNullOrWhiteSpace(englishSection) &&
            !_sefariaTurService.GetSections().Any(section => string.Equals(section.EnglishTitle, englishSection, StringComparison.Ordinal)))
        {
            englishSection = null;
        }

        if (string.IsNullOrWhiteSpace(englishSection) && pathSegments.Length >= 2)
        {
            englishSection = _sefariaTurService.GetSections()
                .FirstOrDefault(section => string.Equals(section.HebrewTitle, pathSegments[1], StringComparison.Ordinal))
                ?.EnglishTitle;
        }

        var simanNumber = parsed.SimanNumber;
        if (!simanNumber.HasValue && pathSegments.Length >= 3)
        {
            var parsedSiman = ParseIndexedLabel(pathSegments[2], "סימן ");
            simanNumber = parsedSiman > 0 ? parsedSiman : null;
        }

        return (englishSection, simanNumber);
    }

    private (string? EnglishSection, int? SimanNumber, int? SeifNumber) ResolveShulchanArukhContext(SubjectNodeModel subject)
    {
        var parsed = ParseShulchanArukhSourceKey(subject.SourceKey);
        var pathSegments = SplitPath(GetSubjectPath(subject.Id)).ToArray();

        var englishSection = parsed.EnglishSection;
        if (!string.IsNullOrWhiteSpace(englishSection) &&
            !_sefariaShulchanArukhService.GetSections().Any(section => string.Equals(section.EnglishTitle, englishSection, StringComparison.Ordinal)))
        {
            englishSection = null;
        }

        if (string.IsNullOrWhiteSpace(englishSection) && pathSegments.Length >= 2)
        {
            englishSection = _sefariaShulchanArukhService.GetSections()
                .FirstOrDefault(section => string.Equals(section.HebrewTitle, pathSegments[1], StringComparison.Ordinal))
                ?.EnglishTitle;
        }

        var simanNumber = parsed.SimanNumber;
        if (!simanNumber.HasValue && pathSegments.Length >= 3)
        {
            var parsedSiman = ParseIndexedLabel(pathSegments[2], "סימן ");
            simanNumber = parsedSiman > 0 ? parsedSiman : null;
        }

        var seifNumber = parsed.SeifNumber;
        if (!seifNumber.HasValue && pathSegments.Length >= 4)
        {
            var parsedSeif = ParseIndexedLabel(pathSegments[3], "סעיף ");
            seifNumber = parsedSeif > 0 ? parsedSeif : null;
        }

        return (englishSection, simanNumber, seifNumber);
    }

    private (string? CollectionKey, string? EnglishSeder, string? EnglishTractate, int? ChapterNumber, string? HebrewTractate) ResolveTalmudContext(SubjectNodeModel subject)
    {
        var parsed = ParseTalmudSourceKey(subject.SourceKey);
        var pathSegments = SplitPath(GetSubjectPath(subject.Id)).ToArray();

        var collectionKey = parsed.CollectionKey;
        if ((string.IsNullOrWhiteSpace(collectionKey) || string.Equals(collectionKey, "Talmud", StringComparison.OrdinalIgnoreCase)) && pathSegments.Length >= 2)
        {
            collectionKey = pathSegments[1] switch
            {
                "בבלי" => "Bavli",
                "ירושלמי" => "Yerushalmi",
                _ => parsed.CollectionKey
            };
        }

        var englishSeder = parsed.EnglishSeder;
        if (!string.IsNullOrWhiteSpace(collectionKey) &&
            !string.IsNullOrWhiteSpace(englishSeder) &&
            !_sefariaTalmudService.GetSedarim(collectionKey)
                .Any(seder => string.Equals(seder.EnglishCategory, englishSeder, StringComparison.Ordinal)))
        {
            englishSeder = null;
        }

        if (string.IsNullOrWhiteSpace(englishSeder) && !string.IsNullOrWhiteSpace(collectionKey) && pathSegments.Length >= 3)
        {
            englishSeder = _sefariaTalmudService.GetSedarim(collectionKey)
                .FirstOrDefault(seder => string.Equals(seder.HebrewTitle, pathSegments[2], StringComparison.Ordinal))
                ?.EnglishCategory;
        }

        var englishTractate = parsed.EnglishTractate;
        if (!string.IsNullOrWhiteSpace(collectionKey) &&
            !string.IsNullOrWhiteSpace(englishSeder) &&
            !string.IsNullOrWhiteSpace(englishTractate) &&
            !_sefariaTalmudService.GetTractates(collectionKey, englishSeder)
                .Any(tractate => string.Equals(tractate.EnglishTitle, englishTractate, StringComparison.Ordinal)))
        {
            englishTractate = null;
        }

        if (string.IsNullOrWhiteSpace(englishTractate) && !string.IsNullOrWhiteSpace(collectionKey) && !string.IsNullOrWhiteSpace(englishSeder) && pathSegments.Length >= 4)
        {
            englishTractate = _sefariaTalmudService.GetTractates(collectionKey, englishSeder)
                .FirstOrDefault(tractate => string.Equals(tractate.HebrewTitle, pathSegments[3], StringComparison.Ordinal))
                ?.EnglishTitle;
        }

        var chapterNumber = parsed.ChapterNumber;
        if (!chapterNumber.HasValue && pathSegments.Length >= 5)
        {
            var parsedChapter = ParseChapterNumberFromName(pathSegments[4]);
            chapterNumber = parsedChapter > 0 ? parsedChapter : null;
        }

        var hebrewTractate = parsed.HebrewTractate;
        if (string.IsNullOrWhiteSpace(hebrewTractate) && pathSegments.Length >= 4)
        {
            hebrewTractate = pathSegments[3];
        }

        return (collectionKey, englishSeder, englishTractate, chapterNumber, hebrewTractate);
    }

    private bool HasPotentialLazyChildren(SubjectNodeModel subject)
    {
        if (string.Equals(subject.SourceSystem, "Sefaria", StringComparison.OrdinalIgnoreCase))
        {
            return subject.NodeType is LibraryNodeType.Section or LibraryNodeType.Book or LibraryNodeType.Chapter;
        }

        if (string.Equals(subject.SourceSystem, "SefariaMishnah", StringComparison.OrdinalIgnoreCase))
        {
            return subject.NodeType is LibraryNodeType.Category or LibraryNodeType.Section or LibraryNodeType.Book or LibraryNodeType.Chapter;
        }

        if (string.Equals(subject.SourceSystem, "SefariaRambam", StringComparison.OrdinalIgnoreCase))
        {
            return subject.NodeType is LibraryNodeType.Category or LibraryNodeType.Section or LibraryNodeType.Book or LibraryNodeType.Chapter;
        }

        if (string.Equals(subject.SourceSystem, "SefariaTur", StringComparison.OrdinalIgnoreCase))
        {
            return subject.NodeType is LibraryNodeType.Category or LibraryNodeType.Section;
        }

        if (string.Equals(subject.SourceSystem, "SefariaShulchanArukh", StringComparison.OrdinalIgnoreCase))
        {
            return subject.NodeType is LibraryNodeType.Category or LibraryNodeType.Section or LibraryNodeType.Chapter;
        }

        if (string.Equals(subject.SourceSystem, "SefariaTalmud", StringComparison.OrdinalIgnoreCase))
        {
            return subject.NodeType is LibraryNodeType.Category or LibraryNodeType.Section or LibraryNodeType.Book or LibraryNodeType.Chapter;
        }

        return LibrarySeedFactory.HasLazyChildren(SplitPath(subject.DisplayPath).ToArray());
    }

    private static int ParseChapterNumberFromName(string chapterName)
    {
        return ParseIndexedLabel(chapterName, "פרק ");
    }

    private static int ParseIndexedLabel(string label, string prefix)
    {
        if (!label.StartsWith(prefix, StringComparison.Ordinal))
        {
            return 0;
        }

        var numeral = label[prefix.Length..].Trim();
        foreach (var value in Enumerable.Range(1, 2000))
        {
            if (string.Equals(LibrarySeedFactory.ToHebrewNumeral(value), numeral, StringComparison.Ordinal))
            {
                return value;
            }
        }

        return 0;
    }

    private static (string? EnglishSefer, string? EnglishHalakhot, int? ChapterNumber) ParseRambamSourceKey(string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            return (null, null, null);
        }

        if (string.Equals(sourceKey, "Rambam", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sourceKey, "Mishneh Torah", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null, null);
        }

        var parts = sourceKey.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !string.Equals(parts[0], "Rambam", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null, null);
        }

        if (parts[1] == "sefer" && parts.Length >= 3)
        {
            return (parts[2], null, null);
        }

        if (parts[1] == "halakhot" && parts.Length >= 4)
        {
            return (parts[2], parts[3], null);
        }

        if (parts[1] == "chapter" && parts.Length >= 4 && int.TryParse(parts[3], out var chapterNumber))
        {
            return (null, parts[2], chapterNumber);
        }

        if (parts[1] == "halakhah" && parts.Length >= 5 && int.TryParse(parts[3], out chapterNumber))
        {
            return (null, parts[2], chapterNumber);
        }

        return (null, null, null);
    }

    private static (string? EnglishSection, int? SimanNumber) ParseTurSourceKey(string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey) || string.Equals(sourceKey, "Tur", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null);
        }

        var parts = sourceKey.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !string.Equals(parts[0], "Tur", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null);
        }

        if (parts[1] == "section" && parts.Length >= 3)
        {
            return (parts[2], null);
        }

        if (parts[1] == "siman" && parts.Length >= 4 && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var simanNumber))
        {
            return (parts[2], simanNumber);
        }

        return (null, null);
    }

    private static (string? EnglishSection, int? SimanNumber, int? SeifNumber) ParseShulchanArukhSourceKey(string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey) || string.Equals(sourceKey, "ShulchanArukh", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null, null);
        }

        var parts = sourceKey.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !string.Equals(parts[0], "ShulchanArukh", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null, null);
        }

        if (parts[1] == "section" && parts.Length >= 3)
        {
            return (parts[2], null, null);
        }

        if (parts[1] == "siman" && parts.Length >= 4 && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var simanNumber))
        {
            return (parts[2], simanNumber, null);
        }

        if (parts[1] == "seif" && parts.Length >= 5 &&
            int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out simanNumber) &&
            int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seifNumber))
        {
            return (parts[2], simanNumber, seifNumber);
        }

        return (null, null, null);
    }

    private static (string? CollectionKey, string? EnglishSeder, string? EnglishTractate, int? ChapterNumber, string? HebrewTractate) ParseTalmudSourceKey(string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            return (null, null, null, null, null);
        }

        if (string.Equals(sourceKey, "Talmud", StringComparison.OrdinalIgnoreCase))
        {
            return ("Talmud", null, null, null, null);
        }

        if (sourceKey.StartsWith("Talmud/", StringComparison.OrdinalIgnoreCase))
        {
            return (sourceKey["Talmud/".Length..], null, null, null, null);
        }

        var parts = sourceKey.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return (null, null, null, null, null);
        }

        return parts[0] switch
        {
            "Bavli" => (
                "Bavli",
                parts[1] == "seder" && parts.Length > 2 ? parts[2]
                    : parts[1] == "tractate" && parts.Length > 2 ? parts[2]
                    : parts[1].StartsWith("Seder ", StringComparison.Ordinal) ? parts[1]
                    : null,
                parts[1] == "tractate" && parts.Length > 3 ? parts[3]
                    : parts[1].StartsWith("Seder ", StringComparison.Ordinal) && parts.Length > 2 ? parts[2]
                    : null,
                parts[1] == "chapter" && parts.Length > 2 && int.TryParse(parts[2], out var bavliChapter) ? bavliChapter
                    : parts[1].StartsWith("Seder ", StringComparison.Ordinal) && parts.Length > 3 && int.TryParse(parts[3], out bavliChapter) ? bavliChapter
                    : null,
                null),
            "Yerushalmi" => (
                "Yerushalmi",
                parts[1] == "seder" && parts.Length > 2 ? parts[2]
                    : parts[1] == "tractate" && parts.Length > 2 ? parts[2]
                    : parts[1].StartsWith("Seder ", StringComparison.Ordinal) ? parts[1]
                    : null,
                parts[1] == "tractate" && parts.Length > 3 ? parts[3]
                    : parts[1] == "chapter" && parts.Length > 2 ? parts[2]
                    : parts[1] == "halakhah" && parts.Length > 2 ? parts[2]
                    : parts[1].StartsWith("Seder ", StringComparison.Ordinal) && parts.Length > 2 ? parts[2]
                    : parts.Length > 2 && int.TryParse(parts[2], out _) ? parts[1]
                    : null,
                parts[1] == "chapter" && parts.Length > 3 && int.TryParse(parts[3], out var yerushalmiChapter) ? yerushalmiChapter
                    : parts[1] == "halakhah" && parts.Length > 3 && int.TryParse(parts[3], out yerushalmiChapter) ? yerushalmiChapter
                    : parts[1].StartsWith("Seder ", StringComparison.Ordinal) && parts.Length > 3 && int.TryParse(parts[3], out yerushalmiChapter) ? yerushalmiChapter
                    : parts.Length > 2 && int.TryParse(parts[2], out yerushalmiChapter) ? yerushalmiChapter
                    : null,
                null),
            _ => (null, null, null, null, null)
        };
    }

    private static LibraryNodeType ParseLibraryNodeType(string rawValue)
    {
        return Enum.TryParse<LibraryNodeType>(rawValue, out var nodeType)
            ? nodeType
            : LibraryNodeType.Generic;
    }
}
