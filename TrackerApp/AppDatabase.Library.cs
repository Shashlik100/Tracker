using System.Globalization;
using Microsoft.Data.Sqlite;

namespace TrackerApp;

public sealed partial class AppDatabase
{
    private const string LibrarySeedVersionKey = "LibrarySeedVersion";

    private void CreateBaseSchema(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Subjects (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ParentId INTEGER NULL,
                Name TEXT NOT NULL,
                NodeType TEXT NOT NULL DEFAULT 'Generic',
                SourceSystem TEXT NOT NULL DEFAULT '',
                SourceKey TEXT NOT NULL DEFAULT '',
                SortOrder INTEGER NOT NULL,
                FOREIGN KEY (ParentId) REFERENCES Subjects(Id)
            );

            CREATE TABLE IF NOT EXISTS StudyItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SubjectId INTEGER NOT NULL,
                Topic TEXT NOT NULL,
                Question TEXT NOT NULL,
                Answer TEXT NOT NULL,
                SubjectPathCache TEXT NOT NULL DEFAULT '',
                RootCategoryCache TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL,
                DueDate TEXT NOT NULL,
                Level INTEGER NOT NULL DEFAULT 1,
                TotalReviews INTEGER NOT NULL DEFAULT 0,
                LastReviewedAt TEXT NULL,
                ManualDifficulty TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (SubjectId) REFERENCES Subjects(Id)
            );

            CREATE TABLE IF NOT EXISTS ReviewHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StudyItemId INTEGER NOT NULL,
                ReviewedAt TEXT NOT NULL,
                Score INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (StudyItemId) REFERENCES StudyItems(Id)
            );

            CREATE TABLE IF NOT EXISTS Tags (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS StudyItemTags (
                StudyItemId INTEGER NOT NULL,
                TagId INTEGER NOT NULL,
                PRIMARY KEY (StudyItemId, TagId),
                FOREIGN KEY (StudyItemId) REFERENCES StudyItems(Id),
                FOREIGN KEY (TagId) REFERENCES Tags(Id)
            );

            CREATE TABLE IF NOT EXISTS AppMetadata (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS SavedReviewSessions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionKind TEXT NOT NULL,
                Payload TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ReviewItemFlags (
                StudyItemId INTEGER PRIMARY KEY,
                ReviewLater INTEGER NOT NULL DEFAULT 0,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (StudyItemId) REFERENCES StudyItems(Id)
            );

            CREATE TABLE IF NOT EXISTS ReviewPresets (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                SubjectId INTEGER NULL,
                RestrictToSubject INTEGER NOT NULL DEFAULT 0,
                TagId INTEGER NULL,
                DueOnly INTEGER NOT NULL DEFAULT 1,
                FailedRecentlyOnly INTEGER NOT NULL DEFAULT 0,
                Difficulty TEXT NOT NULL DEFAULT 'Any',
                OrderMode TEXT NOT NULL DEFAULT 'Default',
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (SubjectId) REFERENCES Subjects(Id),
                FOREIGN KEY (TagId) REFERENCES Tags(Id)
            );

            CREATE TABLE IF NOT EXISTS StudyUnitProgress (
                SubjectId INTEGER PRIMARY KEY,
                IsMarkedLearned INTEGER NOT NULL DEFAULT 0,
                FirstStudiedAt TEXT NULL,
                LastStudiedAt TEXT NULL,
                StudyCount INTEGER NOT NULL DEFAULT 0,
                CompletedReviewCount INTEGER NOT NULL DEFAULT 0,
                NextReviewDate TEXT NULL,
                Status TEXT NOT NULL DEFAULT 'NotStudied',
                LastResult TEXT NOT NULL DEFAULT '',
                LastScore REAL NOT NULL DEFAULT 0,
                CurrentStage INTEGER NOT NULL DEFAULT 0,
                LastReviewAt TEXT NULL,
                LastResultSummary TEXT NOT NULL DEFAULT '',
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (SubjectId) REFERENCES Subjects(Id)
            );

            CREATE TABLE IF NOT EXISTS StudyUnitReviewHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SubjectId INTEGER NOT NULL,
                ReviewedAt TEXT NOT NULL,
                StageBefore INTEGER NOT NULL DEFAULT 0,
                StageAfter INTEGER NOT NULL DEFAULT 0,
                Result TEXT NOT NULL DEFAULT '',
                Score REAL NOT NULL DEFAULT 0,
                TotalCards INTEGER NOT NULL DEFAULT 0,
                SuccessfulCards INTEGER NOT NULL DEFAULT 0,
                FailedCards INTEGER NOT NULL DEFAULT 0,
                NextReviewDate TEXT NULL,
                Summary TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (SubjectId) REFERENCES Subjects(Id)
            );
            """;
        command.ExecuteNonQuery();
    }

    private void UpgradeSchema(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        EnsureColumn(connection, transaction, "Subjects", "NodeType", "TEXT NOT NULL DEFAULT 'Generic'");
        EnsureColumn(connection, transaction, "Subjects", "SourceSystem", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "Subjects", "SourceKey", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "StudyItems", "SubjectPathCache", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "StudyItems", "RootCategoryCache", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "StudyItems", "ModifiedAt", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "StudyItems", "RepetitionCount", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "StudyItems", "Lapses", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "StudyItems", "EaseFactor", "REAL NOT NULL DEFAULT 2.5");
        EnsureColumn(connection, transaction, "StudyItems", "IntervalDays", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "StudyItems", "LastRating", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "StudyItems", "ManualDifficulty", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "ReviewHistory", "Rating", "TEXT NOT NULL DEFAULT 'Good'");
        EnsureColumn(connection, transaction, "ReviewHistory", "WasSuccessful", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumn(connection, transaction, "ReviewHistory", "EaseFactorBefore", "REAL NOT NULL DEFAULT 2.5");
        EnsureColumn(connection, transaction, "ReviewHistory", "EaseFactorAfter", "REAL NOT NULL DEFAULT 2.5");
        EnsureColumn(connection, transaction, "ReviewHistory", "IntervalBefore", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "ReviewHistory", "IntervalAfter", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "StudyUnitProgress", "IsMarkedLearned", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "StudyUnitProgress", "FirstStudiedAt", "TEXT NULL");
        EnsureColumn(connection, transaction, "StudyUnitProgress", "LastStudiedAt", "TEXT NULL");
        EnsureColumn(connection, transaction, "StudyUnitProgress", "StudyCount", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "StudyUnitProgress", "CompletedReviewCount", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "StudyUnitProgress", "NextReviewDate", "TEXT NULL");
        EnsureColumn(connection, transaction, "StudyUnitProgress", "Status", "TEXT NOT NULL DEFAULT 'NotStudied'");
        EnsureColumn(connection, transaction, "StudyUnitProgress", "LastResult", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "StudyUnitProgress", "LastScore", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "StudyUnitProgress", "CurrentStage", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "StudyUnitProgress", "LastReviewAt", "TEXT NULL");
        EnsureColumn(connection, transaction, "StudyUnitProgress", "LastResultSummary", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "StudyUnitProgress", "UpdatedAt", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "StudyUnitReviewHistory", "StageBefore", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "StudyUnitReviewHistory", "StageAfter", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "StudyUnitReviewHistory", "Result", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "StudyUnitReviewHistory", "Score", "REAL NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "StudyUnitReviewHistory", "TotalCards", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "StudyUnitReviewHistory", "SuccessfulCards", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "StudyUnitReviewHistory", "FailedCards", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "StudyUnitReviewHistory", "NextReviewDate", "TEXT NULL");
        EnsureColumn(connection, transaction, "StudyUnitReviewHistory", "Summary", "TEXT NOT NULL DEFAULT ''");

        ExecuteNonQuery(connection, transaction, "CREATE UNIQUE INDEX IF NOT EXISTS IX_Tags_Name ON Tags(Name COLLATE NOCASE);");
        ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_Subjects_ParentId_Name ON Subjects(ParentId, Name);");
        ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_Subjects_ParentId_Source ON Subjects(ParentId, SourceSystem, SourceKey);");
        ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StudyItems_SubjectId ON StudyItems(SubjectId);");
        ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StudyItems_DueDate ON StudyItems(DueDate);");
        ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StudyItems_LastRating ON StudyItems(LastRating);");
        ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StudyItems_SubjectPathCache ON StudyItems(SubjectPathCache);");
        ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StudyItemTags_TagId_StudyItemId ON StudyItemTags(TagId, StudyItemId);");
        ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StudyItemTags_StudyItemId_TagId ON StudyItemTags(StudyItemId, TagId);");
        ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_ReviewHistory_StudyItemId_ReviewedAt ON ReviewHistory(StudyItemId, ReviewedAt);");
        ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_ReviewHistory_FailedRecent ON ReviewHistory(WasSuccessful, ReviewedAt);");
        ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_SavedReviewSessions_Kind_UpdatedAt ON SavedReviewSessions(SessionKind, UpdatedAt);");
        ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_ReviewItemFlags_ReviewLater ON ReviewItemFlags(ReviewLater, UpdatedAt);");
        ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_ReviewPresets_Name ON ReviewPresets(Name COLLATE NOCASE);");
        ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StudyUnitProgress_Status_NextReviewDate ON StudyUnitProgress(Status, NextReviewDate);");
        ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StudyUnitProgress_LastReviewAt ON StudyUnitProgress(LastReviewAt);");
        ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StudyUnitReviewHistory_Subject_ReviewedAt ON StudyUnitReviewHistory(SubjectId, ReviewedAt);");
        ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StudyUnitReviewHistory_Result_ReviewedAt ON StudyUnitReviewHistory(Result, ReviewedAt);");
    }

    private void EnsureColumn(SqliteConnection connection, SqliteTransaction? transaction, string tableName, string columnName, string definition)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        reader.Close();

        using var alterCommand = connection.CreateCommand();
        alterCommand.Transaction = transaction;
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition};";
        alterCommand.ExecuteNonQuery();
    }

    private void EnsureDefaultLibraryStructure(SqliteConnection connection, SqliteTransaction transaction)
    {
        var sortOrder = 1;
        foreach (var rootNode in LibrarySeedFactory.GetDefaultHierarchy())
        {
            EnsureLibraryNode(connection, transaction, null, rootNode, sortOrder++);
        }
    }

    private void EnsureLibrarySeed(SqliteConnection connection, SqliteTransaction transaction)
    {
        var hasSubjects = GetSubjectsCount(connection, transaction) > 0;
        EnsureDefaultLibraryStructure(connection, transaction);
        if (!hasSubjects)
        {
            SeedSampleCardsIfEmpty(connection, transaction);
        }

        SetMetadataValue(connection, transaction, LibrarySeedVersionKey, LibrarySeedFactory.SeedVersion);
    }

    private void SeedSampleCardsIfEmpty(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var countCommand = connection.CreateCommand();
        countCommand.Transaction = transaction;
        countCommand.CommandText = "SELECT COUNT(*) FROM StudyItems;";
        var count = Convert.ToInt32(countCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (count > 0)
        {
            return;
        }

        foreach (var sample in LibrarySeedFactory.GetSampleCards())
        {
            var subjectId = EnsureSubjectPath(connection, transaction, sample.Path);
            InsertStudyItem(connection, transaction, subjectId, sample.Topic, sample.Question, sample.Answer, DateTime.Now);
        }
    }

    private static void ResetLibraryData(SqliteConnection connection, SqliteTransaction transaction)
    {
        ExecuteNonQuery(connection, transaction, "PRAGMA foreign_keys = OFF;");

        foreach (var sql in new[]
                 {
                     "DELETE FROM ReviewHistory;",
                     "DELETE FROM StudyItems;",
                     "DELETE FROM Subjects;",
                     "DELETE FROM sqlite_sequence WHERE name IN ('ReviewHistory', 'StudyItems', 'Subjects');"
                 })
        {
            ExecuteNonQuery(connection, transaction, sql);
        }

        ExecuteNonQuery(connection, transaction, "PRAGMA foreign_keys = ON;");
    }

    private void NormalizeExistingItems(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT Id, SubjectId, CreatedAt, Level, ModifiedAt, EaseFactor, IntervalDays, RepetitionCount
            FROM StudyItems;
            """;

        using var reader = command.ExecuteReader();
        var updates = new List<(int Id, string ModifiedAt, double IntervalDays, int Repetitions, double EaseFactor, string LastRating, string SubjectPathCache, string RootCategoryCache)>();
        while (reader.Read())
        {
            var id = reader.GetInt32(0);
            var subjectId = reader.GetInt32(1);
            var createdAt = ParseDate(reader.GetString(2));
            var level = reader.GetInt32(3);
            var modifiedAt = reader.IsDBNull(4) || string.IsNullOrWhiteSpace(reader.GetString(4))
                ? FormatDate(createdAt)
                : reader.GetString(4);
            var easeFactor = reader.IsDBNull(5) ? 2.5 : reader.GetDouble(5);
            var intervalDays = reader.IsDBNull(6) ? 0 : reader.GetDouble(6);
            var repetitions = reader.IsDBNull(7) ? 0 : reader.GetInt32(7);

            if (repetitions == 0 && level > 1)
            {
                repetitions = Math.Max(0, level - 1);
            }

            if (intervalDays == 0 && repetitions > 0)
            {
                intervalDays = InferIntervalDays(level);
            }

            updates.Add((
                id,
                modifiedAt,
                intervalDays,
                repetitions,
                easeFactor <= 0 ? 2.5 : easeFactor,
                repetitions > 0 ? "Good" : string.Empty,
                GetSubjectPath(subjectId),
                GetRootCategory(subjectId)));
        }

        reader.Close();

        foreach (var update in updates)
        {
            using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText =
                """
                UPDATE StudyItems
                SET ModifiedAt = $modifiedAt,
                    IntervalDays = $intervalDays,
                    RepetitionCount = $repetitions,
                    EaseFactor = $easeFactor,
                    SubjectPathCache = $subjectPathCache,
                    RootCategoryCache = $rootCategoryCache,
                    LastRating = CASE WHEN LastRating = '' THEN $lastRating ELSE LastRating END
                WHERE Id = $id;
                """;
            updateCommand.Parameters.AddWithValue("$id", update.Id);
            updateCommand.Parameters.AddWithValue("$modifiedAt", update.ModifiedAt);
            updateCommand.Parameters.AddWithValue("$intervalDays", update.IntervalDays);
            updateCommand.Parameters.AddWithValue("$repetitions", update.Repetitions);
            updateCommand.Parameters.AddWithValue("$easeFactor", update.EaseFactor);
            updateCommand.Parameters.AddWithValue("$subjectPathCache", update.SubjectPathCache);
            updateCommand.Parameters.AddWithValue("$rootCategoryCache", update.RootCategoryCache);
            updateCommand.Parameters.AddWithValue("$lastRating", update.LastRating);
            updateCommand.ExecuteNonQuery();
        }
    }

    private void NormalizeSefariaSubjectNames(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT Id, NodeType, SourceKey, Name
            FROM Subjects
            WHERE SourceSystem = 'Sefaria';
            """;

        using var reader = command.ExecuteReader();
        var updates = new List<(int Id, string Name)>();
        while (reader.Read())
        {
            var id = reader.GetInt32(0);
            var nodeType = reader.GetString(1);
            var sourceKey = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var currentName = reader.GetString(3);

            string? expectedName = nodeType switch
            {
                nameof(LibraryNodeType.Book) when !string.IsNullOrWhiteSpace(sourceKey)
                    => _sefariaApiService.GetPreferredBookHebrewTitle(sourceKey),
                _ => null
            };

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
            updateCommand.CommandText =
                """
                UPDATE Subjects
                SET Name = $name
                WHERE Id = $id;
                """;
            updateCommand.Parameters.AddWithValue("$id", update.Id);
            updateCommand.Parameters.AddWithValue("$name", update.Name);
            updateCommand.ExecuteNonQuery();
        }
    }

    public int AddSubjectFolder(int? parentId, string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            throw new ArgumentException("Folder name is required.", nameof(folderName));
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var normalizedName = folderName.Trim();
        var existingId = FindSubjectId(connection, transaction, parentId, normalizedName);
        if (existingId.HasValue)
        {
            transaction.Commit();
            RefreshSubjectCache();
            return existingId.Value;
        }

        var subjectId = InsertSubject(
            connection,
            transaction,
            parentId,
            normalizedName,
            LibraryNodeType.Generic,
            string.Empty,
            string.Empty,
            GetNextSortOrder(connection, transaction, parentId));

        transaction.Commit();
        RefreshSubjectCache();
        return subjectId;
    }

    private int EnsureSubjectPath(SqliteConnection connection, SqliteTransaction transaction, IEnumerable<string> segments)
    {
        int? parentId = null;

        foreach (var rawSegment in segments)
        {
            var segment = rawSegment.Trim();
            if (segment.Length == 0)
            {
                continue;
            }

            var existingId = FindSubjectId(connection, transaction, parentId, segment);
            if (existingId.HasValue)
            {
                parentId = existingId.Value;
            }
            else
            {
                parentId = InsertSubject(
                    connection,
                    transaction,
                    parentId,
                    segment,
                    LibraryNodeType.Generic,
                    string.Empty,
                    string.Empty,
                    GetNextSortOrder(connection, transaction, parentId));
            }
        }

        return parentId ?? throw new InvalidOperationException("Subject path could not be resolved.");
    }

    private int EnsureLibraryNode(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int? parentId,
        LibrarySeedNode node,
        int sortOrder)
    {
        var existingId = FindSubjectId(connection, transaction, parentId, node.Name);
        if (!existingId.HasValue &&
            !string.IsNullOrWhiteSpace(node.SourceSystem) &&
            !string.IsNullOrWhiteSpace(node.SourceKey))
        {
            existingId = FindSubjectIdBySource(connection, transaction, parentId, node.SourceSystem, node.SourceKey);
        }

        var nodeId = existingId ?? InsertSubject(
            connection,
            transaction,
            parentId,
            node.Name,
            node.NodeType,
            node.SourceSystem,
            node.SourceKey,
            sortOrder);

        if (existingId.HasValue)
        {
            UpdateSortOrder(connection, transaction, nodeId, sortOrder);
            UpdateSubjectMetadata(connection, transaction, nodeId, node);
        }

        var childSortOrder = 1;
        foreach (var child in node.Children)
        {
            EnsureLibraryNode(connection, transaction, nodeId, child, childSortOrder++);
        }

        return nodeId;
    }

    private static int? FindSubjectId(SqliteConnection connection, SqliteTransaction transaction, int? parentId, string name)
    {
        using var findCommand = connection.CreateCommand();
        findCommand.Transaction = transaction;
        findCommand.CommandText =
            """
            SELECT Id
            FROM Subjects
            WHERE ((ParentId IS NULL AND $parentId IS NULL) OR ParentId = $parentId)
              AND Name = $name
            LIMIT 1;
            """;
        findCommand.Parameters.AddWithValue("$parentId", parentId.HasValue ? parentId.Value : DBNull.Value);
        findCommand.Parameters.AddWithValue("$name", name);

        var existingId = findCommand.ExecuteScalar();
        return existingId is null ? null : Convert.ToInt32(existingId, CultureInfo.InvariantCulture);
    }

    private static int? FindSubjectIdBySource(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int? parentId,
        string sourceSystem,
        string sourceKey)
    {
        using var findCommand = connection.CreateCommand();
        findCommand.Transaction = transaction;
        findCommand.CommandText =
            """
            SELECT Id
            FROM Subjects
            WHERE ((ParentId IS NULL AND $parentId IS NULL) OR ParentId = $parentId)
              AND SourceSystem = $sourceSystem
              AND SourceKey = $sourceKey
            LIMIT 1;
            """;
        findCommand.Parameters.AddWithValue("$parentId", parentId.HasValue ? parentId.Value : DBNull.Value);
        findCommand.Parameters.AddWithValue("$sourceSystem", sourceSystem);
        findCommand.Parameters.AddWithValue("$sourceKey", sourceKey);

        var existingId = findCommand.ExecuteScalar();
        return existingId is null ? null : Convert.ToInt32(existingId, CultureInfo.InvariantCulture);
    }

    private static int InsertSubject(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int? parentId,
        string name,
        LibraryNodeType nodeType,
        string sourceSystem,
        string sourceKey,
        int sortOrder)
    {
        using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText =
            """
            INSERT INTO Subjects (ParentId, Name, NodeType, SourceSystem, SourceKey, SortOrder)
            VALUES ($parentId, $name, $nodeType, $sourceSystem, $sourceKey, $sortOrder);
            """;
        insertCommand.Parameters.AddWithValue("$parentId", parentId.HasValue ? parentId.Value : DBNull.Value);
        insertCommand.Parameters.AddWithValue("$name", name);
        insertCommand.Parameters.AddWithValue("$nodeType", nodeType.ToString());
        insertCommand.Parameters.AddWithValue("$sourceSystem", sourceSystem);
        insertCommand.Parameters.AddWithValue("$sourceKey", sourceKey);
        insertCommand.Parameters.AddWithValue("$sortOrder", sortOrder);
        insertCommand.ExecuteNonQuery();
        return GetLastInsertRowId(connection, transaction);
    }

    private static void UpdateSortOrder(SqliteConnection connection, SqliteTransaction transaction, int subjectId, int sortOrder)
    {
        using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText =
            """
            UPDATE Subjects
            SET SortOrder = $sortOrder
            WHERE Id = $subjectId AND SortOrder <> $sortOrder;
            """;
        updateCommand.Parameters.AddWithValue("$subjectId", subjectId);
        updateCommand.Parameters.AddWithValue("$sortOrder", sortOrder);
        updateCommand.ExecuteNonQuery();
    }

    private static int GetNextSortOrder(SqliteConnection connection, SqliteTransaction transaction, int? parentId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT COALESCE(MAX(SortOrder), 0) + 1
            FROM Subjects
            WHERE ((ParentId IS NULL AND $parentId IS NULL) OR ParentId = $parentId);
            """;
        command.Parameters.AddWithValue("$parentId", parentId.HasValue ? parentId.Value : DBNull.Value);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void UpdateSubjectMetadata(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int subjectId,
        LibrarySeedNode node)
    {
        using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText =
            """
            UPDATE Subjects
            SET Name = $name,
                NodeType = $nodeType,
                SourceSystem = $sourceSystem,
                SourceKey = $sourceKey
            WHERE Id = $subjectId
              AND (Name <> $name OR NodeType <> $nodeType OR SourceSystem <> $sourceSystem OR SourceKey <> $sourceKey);
            """;
        updateCommand.Parameters.AddWithValue("$subjectId", subjectId);
        updateCommand.Parameters.AddWithValue("$name", node.Name);
        updateCommand.Parameters.AddWithValue("$nodeType", node.NodeType.ToString());
        updateCommand.Parameters.AddWithValue("$sourceSystem", node.SourceSystem);
        updateCommand.Parameters.AddWithValue("$sourceKey", node.SourceKey);
        updateCommand.ExecuteNonQuery();
    }

    private static int GetSubjectsCount(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM Subjects;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static string? GetMetadataValue(SqliteConnection connection, SqliteTransaction? transaction, string key)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Value FROM AppMetadata WHERE Key = $key LIMIT 1;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    private static void SetMetadataValue(SqliteConnection connection, SqliteTransaction? transaction, string key, string value)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO AppMetadata (Key, Value)
            VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private void InsertStudyItem(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int subjectId,
        string topic,
        string question,
        string answer,
        DateTime now)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO StudyItems (
                SubjectId, Topic, Question, Answer, SubjectPathCache, RootCategoryCache, CreatedAt, ModifiedAt, DueDate, Level,
                TotalReviews, RepetitionCount, Lapses, EaseFactor, IntervalDays, LastRating, LastReviewedAt, ManualDifficulty
            )
            VALUES (
                $subjectId, $topic, $question, $answer, $subjectPathCache, $rootCategoryCache, $createdAt, $modifiedAt, $dueDate, 1,
                0, 0, 0, 2.5, 0, '', NULL, ''
            );
            """;
        command.Parameters.AddWithValue("$subjectId", subjectId);
        command.Parameters.AddWithValue("$topic", topic.Trim());
        command.Parameters.AddWithValue("$question", question.Trim());
        command.Parameters.AddWithValue("$answer", answer.Trim());
        command.Parameters.AddWithValue("$subjectPathCache", GetSubjectPath(subjectId));
        command.Parameters.AddWithValue("$rootCategoryCache", GetRootCategory(subjectId));
        command.Parameters.AddWithValue("$createdAt", FormatDate(now));
        command.Parameters.AddWithValue("$modifiedAt", FormatDate(now));
        command.Parameters.AddWithValue("$dueDate", FormatDate(now.Date));
        command.ExecuteNonQuery();
    }

    private static double InferIntervalDays(int level)
    {
        return level switch
        {
            <= 1 => 0,
            2 => 1,
            3 => 7,
            4 => 14,
            _ => 14 * Math.Pow(2, Math.Max(0, level - 4))
        };
    }

    private static void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
