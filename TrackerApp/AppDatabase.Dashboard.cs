using Microsoft.Data.Sqlite;

namespace TrackerApp;

public sealed partial class AppDatabase
{
    public DailyDashboardModel GetDailyDashboardModel(int? selectedSubjectId = null)
    {
        var allItems = GetStudyItemsForSubject(null);
        var today = DateTime.Today;
        var dueUnits = GetStudyUnitsDueForReview();
        var failedUnits = GetStudyUnitsFailedRecently();
        var allTrackedUnits = QueryStudyUnitProgressList(null, "AND sup.IsMarkedLearned = 1");

        return new DailyDashboardModel
        {
            DueTodayCount = allItems.Count(item => item.DueDate.Date <= today),
            OverdueCount = allItems.Count(item => item.DueDate.Date < today),
            NewCount = allItems.Count(item => item.TotalReviews == 0),
            FailedRecentlyCount = GetRecentlyFailedItemIds().Count,
            ReviewLaterCount = GetReviewLaterItemIds().Count,
            HasPausedSession = HasPausedReviewSession(),
            UnitsStudiedTodayCount = allTrackedUnits.Count(unit => unit.LastStudiedAt?.Date == today),
            UnitsWaitingCount = allTrackedUnits.Count(unit => unit.Status == StudyUnitStatus.WaitingReview || unit.IsFirstStudy),
            UnitsDueCount = dueUnits.Count,
            UnitsFailedCount = failedUnits.Count,
            UnitsCompletedCycleCount = allTrackedUnits.Count(unit => unit.IsCycleCompleted),
            UnitsForSelectedNodeCount = selectedSubjectId.HasValue ? GetTrackedUnitCount(selectedSubjectId) : 0
        };
    }

    public IReadOnlyList<StudyItemModel> GetSmartQueueItems(SmartQueueKind queueKind, int? selectedSubjectId = null, int? tagId = null)
    {
        var items = queueKind == SmartQueueKind.SelectedNode
            ? GetStudyItemsForSubject(selectedSubjectId)
            : GetStudyItemsForSubject(null);
        var today = DateTime.Today;

        return queueKind switch
        {
            SmartQueueKind.Daily => items.Where(item => item.DueDate.Date <= today).OrderBy(item => item.DueDate).ToList(),
            SmartQueueKind.Overdue => items.Where(item => item.DueDate.Date < today).OrderBy(item => item.DueDate).ToList(),
            SmartQueueKind.FailedRecently => items.Where(item => GetRecentlyFailedItemIds().Contains(item.Id)).ToList(),
            SmartQueueKind.Hard => items.Where(item => item.Difficulty == StudyDifficulty.Hard).OrderBy(item => item.DueDate).ToList(),
            SmartQueueKind.New => items.Where(item => item.TotalReviews == 0).OrderByDescending(item => item.CreatedAt).ToList(),
            SmartQueueKind.ReviewLater => items.Where(item => GetReviewLaterItemIds().Contains(item.Id)).ToList(),
            SmartQueueKind.SelectedNode => items.ToList(),
            SmartQueueKind.Tag when tagId.HasValue => SearchStudyItems(new StudySearchQuery
            {
                TagId = tagId,
                LimitToSelectedNode = false
            }).ToList(),
            _ => []
        };
    }

    public IReadOnlyList<ReviewPresetModel> GetReviewPresets()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT rp.Id,
                   rp.Name,
                   rp.SubjectId,
                   rp.RestrictToSubject,
                   rp.TagId,
                   rp.DueOnly,
                   rp.FailedRecentlyOnly,
                   rp.Difficulty,
                   rp.OrderMode
            FROM ReviewPresets rp
            ORDER BY rp.Name COLLATE NOCASE;
            """;

        using var reader = command.ExecuteReader();
        var presets = new List<ReviewPresetModel>();
        var tags = GetTags().ToDictionary(tag => tag.Id, tag => tag.Name);
        while (reader.Read())
        {
            var subjectId = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2);
            var tagId = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);
            presets.Add(new ReviewPresetModel
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                SubjectId = subjectId,
                SubjectPath = subjectId.HasValue ? GetSubjectPath(subjectId.Value) : string.Empty,
                RestrictToSubject = reader.GetInt32(3) == 1,
                TagId = tagId,
                TagName = tagId.HasValue && tags.TryGetValue(tagId.Value, out var tagName) ? tagName : string.Empty,
                DueOnly = reader.GetInt32(5) == 1,
                FailedRecentlyOnly = reader.GetInt32(6) == 1,
                Difficulty = Enum.TryParse<StudyDifficulty>(reader.GetString(7), out var difficulty) ? difficulty : StudyDifficulty.Any,
                OrderMode = Enum.TryParse<ReviewSessionOrderMode>(reader.GetString(8), out var orderMode) ? orderMode : ReviewSessionOrderMode.Default
            });
        }

        return presets;
    }

    public int AddReviewPreset(string name, int? subjectId, bool restrictToSubject, int? tagId, bool dueOnly, bool failedRecentlyOnly, StudyDifficulty difficulty, ReviewSessionOrderMode orderMode)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        var now = FormatDate(DateTime.Now);
        command.CommandText =
            """
            INSERT INTO ReviewPresets (Name, SubjectId, RestrictToSubject, TagId, DueOnly, FailedRecentlyOnly, Difficulty, OrderMode, CreatedAt, UpdatedAt)
            VALUES ($name, $subjectId, $restrictToSubject, $tagId, $dueOnly, $failedRecentlyOnly, $difficulty, $orderMode, $createdAt, $updatedAt);
            """;
        command.Parameters.AddWithValue("$name", name.Trim());
        command.Parameters.AddWithValue("$subjectId", subjectId.HasValue ? subjectId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$restrictToSubject", restrictToSubject ? 1 : 0);
        command.Parameters.AddWithValue("$tagId", tagId.HasValue ? tagId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$dueOnly", dueOnly ? 1 : 0);
        command.Parameters.AddWithValue("$failedRecentlyOnly", failedRecentlyOnly ? 1 : 0);
        command.Parameters.AddWithValue("$difficulty", difficulty.ToString());
        command.Parameters.AddWithValue("$orderMode", orderMode.ToString());
        command.Parameters.AddWithValue("$createdAt", now);
        command.Parameters.AddWithValue("$updatedAt", now);
        command.ExecuteNonQuery();
        return GetLastInsertRowId(connection, null);
    }

    public void UpdateReviewPreset(int presetId, string name, int? subjectId, bool restrictToSubject, int? tagId, bool dueOnly, bool failedRecentlyOnly, StudyDifficulty difficulty, ReviewSessionOrderMode orderMode)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE ReviewPresets
            SET Name = $name,
                SubjectId = $subjectId,
                RestrictToSubject = $restrictToSubject,
                TagId = $tagId,
                DueOnly = $dueOnly,
                FailedRecentlyOnly = $failedRecentlyOnly,
                Difficulty = $difficulty,
                OrderMode = $orderMode,
                UpdatedAt = $updatedAt
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", presetId);
        command.Parameters.AddWithValue("$name", name.Trim());
        command.Parameters.AddWithValue("$subjectId", subjectId.HasValue ? subjectId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$restrictToSubject", restrictToSubject ? 1 : 0);
        command.Parameters.AddWithValue("$tagId", tagId.HasValue ? tagId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$dueOnly", dueOnly ? 1 : 0);
        command.Parameters.AddWithValue("$failedRecentlyOnly", failedRecentlyOnly ? 1 : 0);
        command.Parameters.AddWithValue("$difficulty", difficulty.ToString());
        command.Parameters.AddWithValue("$orderMode", orderMode.ToString());
        command.Parameters.AddWithValue("$updatedAt", FormatDate(DateTime.Now));
        command.ExecuteNonQuery();
    }

    public void DeleteReviewPreset(int presetId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ReviewPresets WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", presetId);
        command.ExecuteNonQuery();
    }

    public void SetReviewLaterFlag(int itemId, bool isMarked)
    {
        using var connection = OpenConnection();
        if (!isMarked)
        {
            using var deleteCommand = connection.CreateCommand();
            deleteCommand.CommandText = "DELETE FROM ReviewItemFlags WHERE StudyItemId = $itemId;";
            deleteCommand.Parameters.AddWithValue("$itemId", itemId);
            deleteCommand.ExecuteNonQuery();
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO ReviewItemFlags (StudyItemId, ReviewLater, UpdatedAt)
            VALUES ($itemId, 1, $updatedAt)
            ON CONFLICT(StudyItemId) DO UPDATE SET
                ReviewLater = 1,
                UpdatedAt = excluded.UpdatedAt;
            """;
        command.Parameters.AddWithValue("$itemId", itemId);
        command.Parameters.AddWithValue("$updatedAt", FormatDate(DateTime.Now));
        command.ExecuteNonQuery();
    }

    public HashSet<int> GetReviewLaterItemIds()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT StudyItemId FROM ReviewItemFlags WHERE ReviewLater = 1;";
        using var reader = command.ExecuteReader();
        var ids = new HashSet<int>();
        while (reader.Read())
        {
            ids.Add(reader.GetInt32(0));
        }

        return ids;
    }

    public HashSet<int> GetRecentlyFailedItemIds()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT DISTINCT StudyItemId
            FROM ReviewHistory
            WHERE WasSuccessful = 0
              AND date(ReviewedAt) >= date('now', '-14 day');
            """;
        using var reader = command.ExecuteReader();
        var ids = new HashSet<int>();
        while (reader.Read())
        {
            ids.Add(reader.GetInt32(0));
        }

        return ids;
    }

    public IReadOnlyList<WeakSpotModel> GetWeakSpots(int top = 24)
    {
        var allItems = GetStudyItemsForSubject(null);
        var itemStats = LoadItemReviewStats();
        var spots = new List<WeakSpotModel>();

        spots.AddRange(BuildWeakSpots("ספר", allItems, item => ExtractBook(item.SubjectPath), itemStats));
        spots.AddRange(BuildWeakSpots("פרק", allItems, item => ExtractChapter(item.SubjectPath), itemStats));
        spots.AddRange(BuildWeakSpots("פסוק", allItems, item => ExtractVerse(item.SubjectPath), itemStats));
        spots.AddRange(BuildWeakSpots("קושי", allItems, item => TranslateDifficulty(item.Difficulty), itemStats));

        var tagGroups = allItems
            .SelectMany(item => item.Tags.Select(tag => new { Item = item, Tag = tag.Name }))
            .GroupBy(entry => entry.Tag)
            .ToList();

        foreach (var group in tagGroups)
        {
            var ids = group.Select(entry => entry.Item.Id).Distinct().ToArray();
            spots.Add(BuildWeakSpotRow("תגית", group.Key, ids, itemStats, allItems.Where(item => ids.Contains(item.Id)).ToList()));
        }

        return spots
            .Where(spot => spot.ItemCount > 0)
            .OrderByDescending(spot => spot.FailureCount)
            .ThenByDescending(spot => spot.LowRatingPercent)
            .ThenByDescending(spot => spot.OverdueCount)
            .Take(top)
            .ToList();
    }

    private Dictionary<int, (int Total, int Low, int Failures)> LoadItemReviewStats()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT StudyItemId,
                   COUNT(*) AS TotalReviews,
                   SUM(CASE WHEN Rating IN ('Again', 'Hard') THEN 1 ELSE 0 END) AS LowRatings,
                   SUM(CASE WHEN WasSuccessful = 0 THEN 1 ELSE 0 END) AS Failures
            FROM ReviewHistory
            GROUP BY StudyItemId;
            """;

        using var reader = command.ExecuteReader();
        var lookup = new Dictionary<int, (int Total, int Low, int Failures)>();
        while (reader.Read())
        {
            lookup[reader.GetInt32(0)] = (
                reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                reader.IsDBNull(3) ? 0 : reader.GetInt32(3));
        }

        return lookup;
    }

    private IEnumerable<WeakSpotModel> BuildWeakSpots(string kind, IReadOnlyList<StudyItemModel> items, Func<StudyItemModel, string> keySelector, Dictionary<int, (int Total, int Low, int Failures)> stats)
    {
        return items
            .GroupBy(keySelector)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .Select(group => BuildWeakSpotRow(kind, group.Key, group.Select(item => item.Id).ToArray(), stats, group.ToList()));
    }

    private WeakSpotModel BuildWeakSpotRow(string kind, string name, IReadOnlyCollection<int> itemIds, Dictionary<int, (int Total, int Low, int Failures)> stats, IReadOnlyList<StudyItemModel> items)
    {
        var aggregate = itemIds
            .Select(id => stats.TryGetValue(id, out var stat) ? stat : (0, 0, 0))
            .ToList();
        var totalReviews = aggregate.Sum(entry => entry.Item1);
        var lowRatings = aggregate.Sum(entry => entry.Item2);
        var failures = aggregate.Sum(entry => entry.Item3);

        return new WeakSpotModel
        {
            Kind = kind,
            Name = name,
            LowRatingPercent = totalReviews == 0 ? 0 : lowRatings * 100.0 / totalReviews,
            FailureCount = failures,
            OverdueCount = items.Count(item => item.DueDate.Date < DateTime.Today),
            ItemCount = itemIds.Count
        };
    }

    private static string ExtractBook(string subjectPath)
    {
        var segments = subjectPath.Split(" > ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.FirstOrDefault(segment => !segment.StartsWith("פרק ", StringComparison.Ordinal) && !segment.StartsWith("פסוק ", StringComparison.Ordinal) && segment is not "תנ\"ך" and not "תורה" and not "נביאים" and not "כתובים")
            ?? string.Empty;
    }

    private static string ExtractChapter(string subjectPath)
    {
        return subjectPath.Split(" > ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(segment => segment.StartsWith("פרק ", StringComparison.Ordinal)) ?? string.Empty;
    }

    private static string ExtractVerse(string subjectPath)
    {
        return subjectPath.Split(" > ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(segment => segment.StartsWith("פסוק ", StringComparison.Ordinal)) ?? string.Empty;
    }

    private static string TranslateDifficulty(StudyDifficulty difficulty)
    {
        return difficulty switch
        {
            StudyDifficulty.Easy => "קלה",
            StudyDifficulty.Medium => "בינונית",
            StudyDifficulty.Hard => "קשה",
            _ => "ללא"
        };
    }
}
