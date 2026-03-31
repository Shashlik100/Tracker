using System.Globalization;
using Microsoft.Data.Sqlite;

namespace TrackerApp;

public sealed partial class AppDatabase
{
    public IReadOnlyList<StudyItemModel> GetStudyItemsByIds(IEnumerable<int> itemIds)
    {
        var ids = itemIds.Distinct().OrderBy(id => id).ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"""
             SELECT Id, SubjectId, Topic, Question, Answer, CreatedAt, ModifiedAt, DueDate, Level,
                    TotalReviews, RepetitionCount, Lapses, EaseFactor, IntervalDays, LastRating, LastReviewedAt, ManualDifficulty
             FROM StudyItems
             WHERE Id IN ({string.Join(",", ids)})
             ORDER BY date(DueDate) ASC, Topic COLLATE NOCASE ASC;
             """;

        return LoadStudyItems(connection, command);
    }

    public void AddTagsToStudyItems(IEnumerable<int> itemIds, IEnumerable<int> tagIds)
    {
        var ids = itemIds.Distinct().ToArray();
        var tags = tagIds.Distinct().ToArray();
        if (ids.Length == 0 || tags.Length == 0)
        {
            return;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var itemId in ids)
        {
            foreach (var tagId in tags)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    """
                    INSERT INTO StudyItemTags (StudyItemId, TagId)
                    VALUES ($itemId, $tagId)
                    ON CONFLICT(StudyItemId, TagId) DO NOTHING;
                    """;
                command.Parameters.AddWithValue("$itemId", itemId);
                command.Parameters.AddWithValue("$tagId", tagId);
                command.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    public void RemoveTagsFromStudyItems(IEnumerable<int> itemIds, IEnumerable<int> tagIds)
    {
        var ids = itemIds.Distinct().ToArray();
        var tags = tagIds.Distinct().ToArray();
        if (ids.Length == 0 || tags.Length == 0)
        {
            return;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var itemId in ids)
        {
            foreach (var tagId in tags)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM StudyItemTags WHERE StudyItemId = $itemId AND TagId = $tagId;";
                command.Parameters.AddWithValue("$itemId", itemId);
                command.Parameters.AddWithValue("$tagId", tagId);
                command.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    public void SetManualDifficultyForStudyItems(IEnumerable<int> itemIds, StudyDifficulty difficulty)
    {
        var ids = itemIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"""
             UPDATE StudyItems
             SET ManualDifficulty = $difficulty,
                 ModifiedAt = $modifiedAt
             WHERE Id IN ({string.Join(",", ids)});
             """;
        command.Parameters.AddWithValue("$difficulty", difficulty.ToString());
        command.Parameters.AddWithValue("$modifiedAt", FormatDate(DateTime.Now));
        command.ExecuteNonQuery();
    }

    public void DeleteStudyItems(IEnumerable<int> itemIds)
    {
        foreach (var itemId in itemIds.Distinct().ToArray())
        {
            DeleteStudyItem(itemId);
        }
    }

    public CsvImportPreviewResult PreviewCsvImport(string filePath, CsvImportMapping mapping)
    {
        var rows = CsvUtility.ReadRows(filePath);
        if (rows.Count == 0)
        {
            return new CsvImportPreviewResult();
        }

        var bodyRows = mapping.HasHeaderRow ? rows.Skip(1).ToArray() : rows.ToArray();
        var previewRows = new List<CsvImportPreviewRow>();

        for (var index = 0; index < bodyRows.Length; index++)
        {
            var row = bodyRows[index];
            previewRows.Add(BuildImportPreviewRow(index + 1, row, mapping));
        }

        return new CsvImportPreviewResult
        {
            Rows = previewRows,
            AcceptedCount = previewRows.Count(row => row.IsValid),
            RejectedCount = previewRows.Count(row => !row.IsValid)
        };
    }

    public int ImportCsvWithMapping(string filePath, CsvImportMapping mapping)
    {
        var preview = PreviewCsvImport(filePath, mapping);
        if (preview.Rows.Count == 0)
        {
            return 0;
        }

        var rows = CsvUtility.ReadRows(filePath);
        var bodyRows = mapping.HasHeaderRow ? rows.Skip(1).ToArray() : rows.ToArray();

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var imported = 0;
        for (var index = 0; index < bodyRows.Length; index++)
        {
            var previewRow = preview.Rows[index];
            if (!previewRow.IsValid)
            {
                continue;
            }

            var parsed = ParseImportRow(bodyRows[index], mapping, previewRow.SubjectPath, createTags: false);
            var tagIds = EnsureTagIds(connection, transaction, parsed.TagNames);
            InsertStudyItemWithTags(
                connection,
                transaction,
                parsed.SubjectId,
                parsed.Topic,
                parsed.Question,
                parsed.Answer,
                parsed.Difficulty,
                tagIds);
            imported++;
        }

        transaction.Commit();
        RefreshSubjectCache();
        return imported;
    }

    private CsvImportPreviewRow BuildImportPreviewRow(int rowNumber, string[] row, CsvImportMapping mapping)
    {
        try
        {
            var parsed = ParseImportRow(row, mapping, createTags: false);
            return new CsvImportPreviewRow
            {
                RowNumber = rowNumber,
                Topic = parsed.Topic,
                Question = parsed.Question,
                Answer = parsed.Answer,
                SubjectPath = GetSubjectPath(parsed.SubjectId),
                Difficulty = parsed.Difficulty?.ToString() ?? string.Empty,
                Tags = string.Join(", ", parsed.TagNames),
                IsValid = true,
                Reason = "תקין"
            };
        }
        catch (Exception exception)
        {
            return new CsvImportPreviewRow
            {
                RowNumber = rowNumber,
                Topic = ReadMappedValue(row, mapping, CsvFieldType.Topic),
                Question = ReadMappedValue(row, mapping, CsvFieldType.Question),
                Answer = ReadMappedValue(row, mapping, CsvFieldType.Answer),
                SubjectPath = string.Empty,
                Difficulty = ReadMappedValue(row, mapping, CsvFieldType.Difficulty),
                Tags = ReadMappedValue(row, mapping, CsvFieldType.Tags),
                IsValid = false,
                Reason = exception.Message
            };
        }
    }

    private (int SubjectId, string Topic, string Question, string Answer, StudyDifficulty? Difficulty, IReadOnlyList<int> TagIds, IReadOnlyList<string> TagNames) ParseImportRow(string[] row, CsvImportMapping mapping, string? forcedSubjectPath = null, bool createTags = false)
    {
        var topic = ReadMappedValue(row, mapping, CsvFieldType.Topic);
        var question = ReadMappedValue(row, mapping, CsvFieldType.Question);
        var answer = ReadMappedValue(row, mapping, CsvFieldType.Answer);
        var book = ReadMappedValue(row, mapping, CsvFieldType.Book);
        var chapter = ReadMappedValue(row, mapping, CsvFieldType.Chapter);
        var verse = ReadMappedValue(row, mapping, CsvFieldType.Verse);
        var difficultyRaw = ReadMappedValue(row, mapping, CsvFieldType.Difficulty);
        var tagsRaw = ReadMappedValue(row, mapping, CsvFieldType.Tags);

        if (string.IsNullOrWhiteSpace(topic) || string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
        {
            throw new InvalidOperationException("חסרים נושא, שאלה או תשובה.");
        }

        var subjectId = !string.IsNullOrWhiteSpace(forcedSubjectPath)
            ? ResolveSubjectIdByPath(forcedSubjectPath)
            : ResolveSubjectIdFromLocation(book, chapter, verse, mapping.FallbackSubjectId);

        var difficulty = ParseDifficulty(difficultyRaw);
        var tagNames = SplitTagNames(tagsRaw);
        var tagIds = createTags ? tagNames.Select(AddTag).Distinct().ToArray() : Array.Empty<int>();
        return (subjectId, topic.Trim(), question.Trim(), answer.Trim(), difficulty, tagIds, tagNames);
    }

    private int InsertStudyItemWithTags(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int subjectId,
        string topic,
        string question,
        string answer,
        StudyDifficulty? difficulty,
        IReadOnlyList<int> tagIds)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var now = DateTime.Now;
        command.CommandText =
            """
            INSERT INTO StudyItems (
                SubjectId, Topic, Question, Answer, SubjectPathCache, RootCategoryCache, CreatedAt, ModifiedAt, DueDate, Level,
                TotalReviews, RepetitionCount, Lapses, EaseFactor, IntervalDays, LastRating, LastReviewedAt, ManualDifficulty
            )
            VALUES (
                $subjectId, $topic, $question, $answer, $subjectPathCache, $rootCategoryCache, $createdAt, $modifiedAt, $dueDate, 1,
                0, 0, 0, 2.5, 0, '', NULL, $manualDifficulty
            );
            """;
        command.Parameters.AddWithValue("$subjectId", subjectId);
        command.Parameters.AddWithValue("$topic", topic);
        command.Parameters.AddWithValue("$question", question);
        command.Parameters.AddWithValue("$answer", answer);
        command.Parameters.AddWithValue("$subjectPathCache", GetSubjectPath(subjectId));
        command.Parameters.AddWithValue("$rootCategoryCache", GetRootCategory(subjectId));
        command.Parameters.AddWithValue("$createdAt", FormatDate(now));
        command.Parameters.AddWithValue("$modifiedAt", FormatDate(now));
        command.Parameters.AddWithValue("$dueDate", FormatDate(now.Date));
        command.Parameters.AddWithValue("$manualDifficulty", difficulty?.ToString() ?? string.Empty);
        command.ExecuteNonQuery();

        var itemId = GetLastInsertRowId(connection, transaction);
        foreach (var tagId in tagIds)
        {
            using var tagCommand = connection.CreateCommand();
            tagCommand.Transaction = transaction;
            tagCommand.CommandText = "INSERT INTO StudyItemTags (StudyItemId, TagId) VALUES ($itemId, $tagId);";
            tagCommand.Parameters.AddWithValue("$itemId", itemId);
            tagCommand.Parameters.AddWithValue("$tagId", tagId);
            tagCommand.ExecuteNonQuery();
        }

        return itemId;
    }

    private int ResolveSubjectIdFromLocation(string book, string chapter, string verse, int? fallbackSubjectId)
    {
        if (string.IsNullOrWhiteSpace(book) && fallbackSubjectId.HasValue)
        {
            return fallbackSubjectId.Value;
        }

        var candidates = _subjects.Where(subject => string.Equals(subject.Name, book.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException($"הספר '{book}' לא נמצא בעץ הספרייה.");
        }

        var subject = candidates[0];
        if (!string.IsNullOrWhiteSpace(chapter))
        {
            var chapterName = NormalizeLevelName(chapter, "פרק");
            subject = _subjects.FirstOrDefault(child => child.ParentId == subject.Id && string.Equals(child.Name, chapterName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"הפרק '{chapter}' לא נמצא תחת הספר '{book}'.");
        }

        if (!string.IsNullOrWhiteSpace(verse))
        {
            var verseName = NormalizeLevelName(verse, "פסוק");
            subject = _subjects.FirstOrDefault(child => child.ParentId == subject.Id && string.Equals(child.Name, verseName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"הפסוק '{verse}' לא נמצא תחת '{book}'.");
        }

        return subject.Id;
    }

    private int ResolveSubjectIdByPath(string path)
    {
        var normalized = path.Replace(" / ", " > ", StringComparison.Ordinal);
        var subject = _subjects.FirstOrDefault(candidate => string.Equals(GetSubjectPath(candidate.Id), normalized, StringComparison.Ordinal));
        return subject?.Id ?? throw new InvalidOperationException($"הנתיב '{path}' לא נמצא.");
    }

    private static string ReadMappedValue(string[] row, CsvImportMapping mapping, CsvFieldType fieldType)
    {
        var match = mapping.ColumnMappings.FirstOrDefault(pair => pair.Value == fieldType);
        if (match.Value != fieldType)
        {
            return string.Empty;
        }

        return match.Key >= 0 && match.Key < row.Length ? row[match.Key] : string.Empty;
    }

    private static string NormalizeLevelName(string rawValue, string prefix)
    {
        var normalized = rawValue.Trim();
        if (normalized.StartsWith(prefix, StringComparison.Ordinal))
        {
            return normalized;
        }

        if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return $"{prefix} {LibrarySeedFactory.ToHebrewNumeral(number)}";
        }

        return $"{prefix} {normalized}";
    }

    private static StudyDifficulty? ParseDifficulty(string rawValue)
    {
        var normalized = rawValue.Trim();
        return normalized switch
        {
            "" => null,
            "קשה" => StudyDifficulty.Hard,
            "בינונית" => StudyDifficulty.Medium,
            "קלה" => StudyDifficulty.Easy,
            _ when Enum.TryParse<StudyDifficulty>(normalized, true, out var difficulty) => difficulty,
            _ => null
        };
    }

    private static IReadOnlyList<string> SplitTagNames(string rawValue)
    {
        return rawValue
            .Split([';', ',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<int> EnsureTagIds(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<string> tagNames)
    {
        var tagIds = new List<int>();
        foreach (var tagName in tagNames)
        {
            using var findCommand = connection.CreateCommand();
            findCommand.Transaction = transaction;
            findCommand.CommandText = "SELECT Id FROM Tags WHERE Name = $name COLLATE NOCASE LIMIT 1;";
            findCommand.Parameters.AddWithValue("$name", tagName);
            var existing = findCommand.ExecuteScalar();
            if (existing is not null)
            {
                tagIds.Add(Convert.ToInt32(existing, CultureInfo.InvariantCulture));
                continue;
            }

            using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = "INSERT INTO Tags (Name) VALUES ($name);";
            insertCommand.Parameters.AddWithValue("$name", tagName);
            insertCommand.ExecuteNonQuery();
            tagIds.Add(GetLastInsertRowId(connection, transaction));
        }

        return tagIds;
    }
}
