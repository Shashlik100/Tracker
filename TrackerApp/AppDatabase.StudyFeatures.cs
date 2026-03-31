using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace TrackerApp;

public sealed partial class AppDatabase
{
    public IReadOnlyList<TagModel> GetTags()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT t.Id,
                   t.Name,
                   COUNT(sit.StudyItemId) AS UsageCount
            FROM Tags t
            LEFT JOIN StudyItemTags sit ON sit.TagId = t.Id
            GROUP BY t.Id, t.Name
            ORDER BY t.Name COLLATE NOCASE;
            """;

        using var reader = command.ExecuteReader();
        var tags = new List<TagModel>();
        while (reader.Read())
        {
            tags.Add(new TagModel
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                UsageCount = reader.GetInt32(2)
            });
        }

        return tags;
    }

    public IReadOnlyList<TagModel> GetTagsForStudyItem(int itemId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT t.Id, t.Name
            FROM Tags t
            INNER JOIN StudyItemTags sit ON sit.TagId = t.Id
            WHERE sit.StudyItemId = $itemId
            ORDER BY t.Name COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$itemId", itemId);

        using var reader = command.ExecuteReader();
        var tags = new List<TagModel>();
        while (reader.Read())
        {
            tags.Add(new TagModel
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }

        return tags;
    }

    public int AddTag(string name)
    {
        var normalizedName = NormalizeTagName(name);
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var existingId = FindTagId(connection, transaction, normalizedName);
        if (existingId.HasValue)
        {
            transaction.Commit();
            return existingId.Value;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO Tags (Name)
            VALUES ($name);
            """;
        command.Parameters.AddWithValue("$name", normalizedName);
        command.ExecuteNonQuery();

        transaction.Commit();
        return GetLastInsertRowId(connection, null);
    }

    public void UpdateTag(int tagId, string name)
    {
        var normalizedName = NormalizeTagName(name);
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var existingId = FindTagId(connection, transaction, normalizedName);
        if (existingId.HasValue && existingId.Value != tagId)
        {
            throw new InvalidOperationException("כבר קיימת תגית בשם זה.");
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE Tags
            SET Name = $name
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", tagId);
        command.Parameters.AddWithValue("$name", normalizedName);
        command.ExecuteNonQuery();

        transaction.Commit();
    }

    public void DeleteTag(int tagId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var relationCommand = connection.CreateCommand())
        {
            relationCommand.Transaction = transaction;
            relationCommand.CommandText = "DELETE FROM StudyItemTags WHERE TagId = $tagId;";
            relationCommand.Parameters.AddWithValue("$tagId", tagId);
            relationCommand.ExecuteNonQuery();
        }

        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM Tags WHERE Id = $tagId;";
            deleteCommand.Parameters.AddWithValue("$tagId", tagId);
            deleteCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void SetTagsForStudyItem(int itemId, IEnumerable<int> tagIds)
    {
        var normalizedTagIds = tagIds
            .Distinct()
            .OrderBy(tagId => tagId)
            .ToArray();

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM StudyItemTags WHERE StudyItemId = $itemId;";
            deleteCommand.Parameters.AddWithValue("$itemId", itemId);
            deleteCommand.ExecuteNonQuery();
        }

        foreach (var tagId in normalizedTagIds)
        {
            using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
                """
                INSERT INTO StudyItemTags (StudyItemId, TagId)
                VALUES ($itemId, $tagId);
                """;
            insertCommand.Parameters.AddWithValue("$itemId", itemId);
            insertCommand.Parameters.AddWithValue("$tagId", tagId);
            insertCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public IReadOnlyList<StudyItemModel> SearchStudyItems(StudySearchQuery query)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        var sql = new StringBuilder(
            """
            WITH RECURSIVE SubjectTree AS (
                SELECT Id FROM Subjects WHERE Id = $subjectId
                UNION ALL
                SELECT Subjects.Id
                FROM Subjects
                INNER JOIN SubjectTree ON Subjects.ParentId = SubjectTree.Id
            )
            SELECT Id, SubjectId, Topic, Question, Answer, CreatedAt, ModifiedAt, DueDate, Level,
                   TotalReviews, RepetitionCount, Lapses, EaseFactor, IntervalDays, LastRating, LastReviewedAt, ManualDifficulty
            FROM StudyItems i
            WHERE 1 = 1 
            """);

        if (query.LimitToSelectedNode && query.SubjectId.HasValue)
        {
            sql.AppendLine(" AND i.SubjectId IN (SELECT Id FROM SubjectTree)");
            command.Parameters.AddWithValue("$subjectId", query.SubjectId.Value);
        }
        else
        {
            command.Parameters.AddWithValue("$subjectId", DBNull.Value);
        }

        if (query.TagId.HasValue)
        {
            sql.AppendLine(" AND EXISTS (SELECT 1 FROM StudyItemTags sit WHERE sit.StudyItemId = i.Id AND sit.TagId = $tagId)");
            command.Parameters.AddWithValue("$tagId", query.TagId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Book))
        {
            sql.AppendLine(" AND i.SubjectPathCache LIKE $book");
            command.Parameters.AddWithValue("$book", $"%{query.Book.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(query.Chapter))
        {
            sql.AppendLine(" AND i.SubjectPathCache LIKE $chapter");
            command.Parameters.AddWithValue("$chapter", $"%{NormalizeLibraryLevel(query.Chapter, "פרק")}%");
        }

        if (!string.IsNullOrWhiteSpace(query.Verse))
        {
            sql.AppendLine(" AND i.SubjectPathCache LIKE $verse");
            command.Parameters.AddWithValue("$verse", $"%{NormalizeLibraryLevel(query.Verse, "פסוק")}%");
        }

        var tokens = SplitSearchTokens(query.SearchText);
        for (var index = 0; index < tokens.Length; index++)
        {
            var parameterName = $"$token{index}";
            sql.AppendLine(
                $"""
                 AND (
                        i.SubjectPathCache LIKE {parameterName}
                     OR i.Topic LIKE {parameterName}
                     OR i.Question LIKE {parameterName}
                     OR i.Answer LIKE {parameterName}
                     OR EXISTS (
                            SELECT 1
                            FROM StudyItemTags sit
                            INNER JOIN Tags t ON t.Id = sit.TagId
                            WHERE sit.StudyItemId = i.Id
                              AND t.Name LIKE {parameterName}
                        )
                 )
                 """);
            command.Parameters.AddWithValue(parameterName, $"%{tokens[index]}%");
        }

        if (query.FailedRecentlyOnly)
        {
            sql.AppendLine(
                """
                 AND EXISTS (
                        SELECT 1
                        FROM ReviewHistory rh
                        WHERE rh.StudyItemId = i.Id
                          AND rh.WasSuccessful = 0
                          AND date(rh.ReviewedAt) >= date('now', '-14 day')
                    )
                 """);
        }

        AppendDifficultyFilter(sql, query.Difficulty);

        sql.AppendLine(" ORDER BY date(i.DueDate) ASC, i.Topic COLLATE NOCASE ASC;");
        command.CommandText = sql.ToString();

        return LoadStudyItems(connection, command);
    }

    public IReadOnlyList<StudyItemModel> GetStudyItemsForReview(StudyReviewFilter filter)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        var sql = new StringBuilder(
            """
            WITH RECURSIVE SubjectTree AS (
                SELECT Id FROM Subjects WHERE Id = $subjectId
                UNION ALL
                SELECT Subjects.Id
                FROM Subjects
                INNER JOIN SubjectTree ON Subjects.ParentId = SubjectTree.Id
            )
            SELECT Id, SubjectId, Topic, Question, Answer, CreatedAt, ModifiedAt, DueDate, Level,
                   TotalReviews, RepetitionCount, Lapses, EaseFactor, IntervalDays, LastRating, LastReviewedAt, ManualDifficulty
            FROM StudyItems i
            WHERE 1 = 1 
            """);

        if (filter.RestrictToSelectedNode && filter.SubjectId.HasValue)
        {
            sql.AppendLine(" AND i.SubjectId IN (SELECT Id FROM SubjectTree)");
            command.Parameters.AddWithValue("$subjectId", filter.SubjectId.Value);
        }
        else
        {
            command.Parameters.AddWithValue("$subjectId", DBNull.Value);
        }

        if (filter.TagId.HasValue)
        {
            sql.AppendLine(" AND EXISTS (SELECT 1 FROM StudyItemTags sit WHERE sit.StudyItemId = i.Id AND sit.TagId = $tagId)");
            command.Parameters.AddWithValue("$tagId", filter.TagId.Value);
        }

        if (filter.DueOnly)
        {
            sql.AppendLine(" AND date(i.DueDate) <= date('now', 'localtime')");
        }

        if (filter.FailedRecentlyOnly)
        {
            sql.AppendLine(
                """
                 AND EXISTS (
                        SELECT 1
                        FROM ReviewHistory rh
                        WHERE rh.StudyItemId = i.Id
                          AND rh.WasSuccessful = 0
                          AND date(rh.ReviewedAt) >= date('now', '-14 day')
                    )
                 """);
        }

        AppendDifficultyFilter(sql, filter.Difficulty);
        sql.AppendLine(" ORDER BY date(i.DueDate) ASC, i.EaseFactor ASC, i.Lapses DESC, i.Topic COLLATE NOCASE ASC;");
        command.CommandText = sql.ToString();

        return LoadStudyItems(connection, command);
    }

    private IReadOnlyList<StudyItemModel> LoadStudyItems(SqliteConnection connection, SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var items = new List<StudyItemModel>();
        while (reader.Read())
        {
            items.Add(MapStudyItem(reader));
        }

        AttachTagsToItems(connection, items);
        return items;
    }

    private void AttachTagsToItems(SqliteConnection connection, IReadOnlyList<StudyItemModel> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        var tagLookup = items.ToDictionary(item => item.Id, _ => new List<TagModel>());
        var idList = string.Join(",", items.Select(item => item.Id.ToString(CultureInfo.InvariantCulture)));

        using var command = connection.CreateCommand();
        command.CommandText =
            $"""
             SELECT sit.StudyItemId, t.Id, t.Name
             FROM StudyItemTags sit
             INNER JOIN Tags t ON t.Id = sit.TagId
             WHERE sit.StudyItemId IN ({idList})
             ORDER BY t.Name COLLATE NOCASE;
             """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tagLookup[reader.GetInt32(0)].Add(new TagModel
            {
                Id = reader.GetInt32(1),
                Name = reader.GetString(2)
            });
        }

        foreach (var item in items)
        {
            item.Tags.Clear();
            item.Tags.AddRange(tagLookup[item.Id]);
        }
    }

    private static string NormalizeTagName(string name)
    {
        var normalized = name.Trim();
        if (normalized.Length == 0)
        {
            throw new InvalidOperationException("יש להזין שם תגית.");
        }

        return normalized;
    }

    private static int? FindTagId(SqliteConnection connection, SqliteTransaction transaction, string name)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Id FROM Tags WHERE Name = $name COLLATE NOCASE LIMIT 1;";
        command.Parameters.AddWithValue("$name", name);
        var result = command.ExecuteScalar();
        return result is null ? null : Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static string[] SplitSearchTokens(string rawSearchText)
    {
        return rawSearchText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static void AppendDifficultyFilter(StringBuilder sql, StudyDifficulty difficulty)
    {
        switch (difficulty)
        {
            case StudyDifficulty.Easy:
                sql.AppendLine(" AND ((i.ManualDifficulty = 'Easy') OR (i.ManualDifficulty = '' AND i.Lapses = 0 AND i.EaseFactor >= 2.45 AND (i.LastRating = '' OR i.LastRating IN ('Good', 'Easy', 'Perfect'))))");
                break;
            case StudyDifficulty.Medium:
                sql.AppendLine(" AND ((i.ManualDifficulty = 'Medium') OR (i.ManualDifficulty = '' AND ((i.Lapses = 1) OR (i.EaseFactor >= 2.2 AND i.EaseFactor < 2.45))))");
                break;
            case StudyDifficulty.Hard:
                sql.AppendLine(" AND ((i.ManualDifficulty = 'Hard') OR (i.ManualDifficulty = '' AND (i.Lapses >= 2 OR i.EaseFactor < 2.2 OR i.LastRating IN ('Again', 'Hard'))))");
                break;
        }
    }

    private static string NormalizeLibraryLevel(string rawValue, string prefix)
    {
        var normalized = rawValue.Trim();
        if (normalized.StartsWith(prefix, StringComparison.Ordinal))
        {
            return normalized;
        }

        if (int.TryParse(normalized, out var numeric))
        {
            return $"{prefix} {ToHebrewNumeral(numeric)}";
        }

        return $"{prefix} {normalized}";
    }

    private static string ToHebrewNumeral(int number)
    {
        return LibrarySeedFactory.ToHebrewNumeral(number);
    }
}
