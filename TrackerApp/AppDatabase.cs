using System.Globalization;
using Microsoft.Data.Sqlite;

namespace TrackerApp;

public sealed partial class AppDatabase : IDisposable
{
    private readonly string _connectionString;
    private readonly string _databasePath;
    private readonly SefariaTanakhService _sefariaApiService;
    private readonly SefariaMishnahService _sefariaMishnahService;
    private readonly SefariaRambamService _sefariaRambamService;
    private readonly SefariaTalmudService _sefariaTalmudService;
    private readonly SefariaTurService _sefariaTurService;
    private readonly SefariaShulchanArukhService _sefariaShulchanArukhService;
    private List<SubjectNodeModel> _subjects = new();
    private LibraryAuditSummary _libraryAuditSummary = new();

    public AppDatabase()
    {
        var dataFolder = ResolveDataFolder();
        _databasePath = Path.Combine(dataFolder, "tracker.db");
        _sefariaApiService = new SefariaTanakhService(Path.Combine(dataFolder, "sefaria-cache"));
        _sefariaMishnahService = new SefariaMishnahService(Path.Combine(dataFolder, "sefaria-cache-mishnah"));
        _sefariaRambamService = new SefariaRambamService(Path.Combine(dataFolder, "sefaria-cache-rambam"));
        _sefariaTalmudService = new SefariaTalmudService(Path.Combine(dataFolder, "sefaria-cache-talmud"));
        _sefariaTurService = new SefariaTurService(Path.Combine(dataFolder, "sefaria-cache-tur"));
        _sefariaShulchanArukhService = new SefariaShulchanArukhService(Path.Combine(dataFolder, "sefaria-cache-shulchan-arukh"));
        _connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath }.ToString();
    }

    public string DatabasePath => _databasePath;

    public void Initialize()
    {
        try
        {
            InitializeCore();
        }
        catch (Exception exception) when (CanRecoverByRecreatingDatabase(exception))
        {
            RecreateDatabaseFile(exception);
            InitializeCore();
        }
    }

    private void InitializeCore()
    {
        using var connection = OpenConnection();
        ApplySchemaMigrations(connection);

        using var transaction = connection.BeginTransaction();
        var hasSubjects = GetSubjectsCount(connection, transaction) > 0;
        _libraryAuditSummary = AuditAndNormalizeLibrary(connection, transaction);
        if (!hasSubjects)
        {
            SeedSampleCardsIfEmpty(connection, transaction);
        }
        SetMetadataValue(connection, transaction, LibrarySeedVersionKey, LibrarySeedFactory.SeedVersion);
        _subjects = LoadSubjects(connection);
        NormalizeExistingItems(connection, transaction);
        transaction.Commit();

        _subjects = LoadSubjects(connection);
    }

    private static bool CanRecoverByRecreatingDatabase(Exception exception)
    {
        return exception is SqliteException or IOException or UnauthorizedAccessException;
    }

    private static string ResolveDataFolder()
    {
        var preferredFolder = Environment.GetEnvironmentVariable("TRACKERAPP_DATA_DIR");
        if (TryEnsureWritableFolder(preferredFolder, out var resolvedPreferred))
        {
            return resolvedPreferred;
        }

        var localAppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrackerApp");
        if (TryEnsureWritableFolder(localAppDataFolder, out var resolvedLocal))
        {
            return resolvedLocal;
        }

        var fallbackFolder = Path.Combine(AppContext.BaseDirectory, "AppData");
        if (TryEnsureWritableFolder(fallbackFolder, out var resolvedFallback))
        {
            return resolvedFallback;
        }

        throw new InvalidOperationException("Failed to locate a writable data folder for TrackerApp.");
    }

    private static bool TryEnsureWritableFolder(string? folderPath, out string resolvedFolder)
    {
        resolvedFolder = string.Empty;
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(folderPath);

            var probePath = Path.Combine(folderPath, ".write-test");
            File.WriteAllText(probePath, DateTime.Now.ToString("O", CultureInfo.InvariantCulture));
            File.Delete(probePath);

            resolvedFolder = folderPath;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void RecreateDatabaseFile(Exception originalException)
    {
        try
        {
            SqliteConnection.ClearAllPools();
        }
        catch
        {
        }

        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch (Exception deleteException)
        {
            throw new InvalidOperationException(
                $"Failed to recreate the SQLite database at '{_databasePath}'.",
                deleteException);
        }
    }

    public IReadOnlyList<SubjectNodeModel> GetSubjects() => _subjects;

    public IReadOnlyList<SefariaRequestLogEntry> GetSefariaRequestLog() =>
        _sefariaApiService.GetRequestLog()
            .Concat(_sefariaMishnahService.GetRequestLog())
            .Concat(_sefariaRambamService.GetRequestLog())
            .Concat(_sefariaTalmudService.GetRequestLog())
            .Concat(_sefariaTurService.GetRequestLog())
            .Concat(_sefariaShulchanArukhService.GetRequestLog())
            .ToArray();

    public LibraryAuditSummary GetLibraryAuditSummary()
    {
        return new LibraryAuditSummary
        {
            ExistingRootsBefore = _libraryAuditSummary.ExistingRootsBefore.ToList(),
            AddedBranches = _libraryAuditSummary.AddedBranches.ToList(),
            FixedIssues = _libraryAuditSummary.FixedIssues.ToList(),
            CompletedBranches = _libraryAuditSummary.CompletedBranches.ToList(),
            RemainingLimitations = _libraryAuditSummary.RemainingLimitations.ToList(),
            RenamedNodes = _libraryAuditSummary.RenamedNodes,
            DuplicateGroupsResolved = _libraryAuditSummary.DuplicateGroupsResolved,
            MergedSubjects = _libraryAuditSummary.MergedSubjects,
            ReassignedStudyItems = _libraryAuditSummary.ReassignedStudyItems,
            ReassignedPresets = _libraryAuditSummary.ReassignedPresets
        };
    }

    public IReadOnlyList<SubjectNodeModel> GetLeafSubjects()
    {
        var parents = _subjects.Where(subject => subject.ParentId.HasValue)
            .Select(subject => subject.ParentId!.Value)
            .ToHashSet();

        return _subjects
            .Where(subject => !parents.Contains(subject.Id))
            .OrderBy(subject => GetSubjectPath(subject.Id))
            .Select(subject => new SubjectNodeModel
            {
                Id = subject.Id,
                ParentId = subject.ParentId,
                Name = subject.Name,
                DisplayPath = GetSubjectPath(subject.Id),
                SortOrder = subject.SortOrder,
                NodeType = subject.NodeType,
                SourceSystem = subject.SourceSystem,
                SourceKey = subject.SourceKey
            })
            .ToList();
    }

    public IReadOnlyList<SubjectNodeModel> GetAssignableSubjects()
    {
        return _subjects
            .OrderBy(subject => GetSubjectPath(subject.Id))
            .Select(subject => new SubjectNodeModel
            {
                Id = subject.Id,
                ParentId = subject.ParentId,
                Name = subject.Name,
                DisplayPath = GetSubjectPath(subject.Id),
                SortOrder = subject.SortOrder,
                NodeType = subject.NodeType,
                SourceSystem = subject.SourceSystem,
                SourceKey = subject.SourceKey
            })
            .ToList();
    }

    public string GetSubjectPath(int subjectId)
    {
        var lookup = _subjects.ToDictionary(subject => subject.Id);
        if (!lookup.TryGetValue(subjectId, out var current))
        {
            return string.Empty;
        }

        var path = new Stack<string>();
        while (true)
        {
            path.Push(current.Name);
            if (!current.ParentId.HasValue || !lookup.TryGetValue(current.ParentId.Value, out current))
            {
                break;
            }
        }

        return string.Join(" > ", path);
    }

    public StudyItemModel? GetStudyItem(int itemId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, SubjectId, Topic, Question, Answer, CreatedAt, ModifiedAt, DueDate, Level,
                   TotalReviews, RepetitionCount, Lapses, EaseFactor, IntervalDays, LastRating, LastReviewedAt, ManualDifficulty
            FROM StudyItems
            WHERE Id = $itemId;
            """;
        command.Parameters.AddWithValue("$itemId", itemId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var item = MapStudyItem(reader);
        reader.Close();
        AttachTagsToItems(connection, [item]);
        return item;
    }

    public IReadOnlyList<StudyItemModel> GetStudyItemsForSubject(int? subjectId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
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
            FROM StudyItems
            WHERE $subjectId IS NULL OR SubjectId IN (SELECT Id FROM SubjectTree)
            ORDER BY date(DueDate) ASC, Topic ASC;
            """;
        command.Parameters.AddWithValue("$subjectId", subjectId.HasValue ? subjectId.Value : DBNull.Value);

        return LoadStudyItems(connection, command);
    }

    public int AddStudyItem(int subjectId, string topic, string question, string answer, StudyDifficulty? manualDifficulty = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
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
        command.Parameters.AddWithValue("$topic", topic.Trim());
        command.Parameters.AddWithValue("$question", question.Trim());
        command.Parameters.AddWithValue("$answer", answer.Trim());
        command.Parameters.AddWithValue("$subjectPathCache", GetSubjectPath(subjectId));
        command.Parameters.AddWithValue("$rootCategoryCache", GetRootCategory(subjectId));
        command.Parameters.AddWithValue("$createdAt", FormatDate(now));
        command.Parameters.AddWithValue("$modifiedAt", FormatDate(now));
        command.Parameters.AddWithValue("$dueDate", FormatDate(now.Date));
        command.Parameters.AddWithValue("$manualDifficulty", manualDifficulty.HasValue ? manualDifficulty.Value.ToString() : string.Empty);
        command.ExecuteNonQuery();
        return GetLastInsertRowId(connection, null);
    }

    public void UpdateStudyItem(int itemId, int subjectId, string topic, string question, string answer, StudyDifficulty? manualDifficulty = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE StudyItems
            SET SubjectId = $subjectId,
                Topic = $topic,
                Question = $question,
                Answer = $answer,
                SubjectPathCache = $subjectPathCache,
                RootCategoryCache = $rootCategoryCache,
                ManualDifficulty = $manualDifficulty,
                ModifiedAt = $modifiedAt
            WHERE Id = $itemId;
            """;
        command.Parameters.AddWithValue("$itemId", itemId);
        command.Parameters.AddWithValue("$subjectId", subjectId);
        command.Parameters.AddWithValue("$topic", topic.Trim());
        command.Parameters.AddWithValue("$question", question.Trim());
        command.Parameters.AddWithValue("$answer", answer.Trim());
        command.Parameters.AddWithValue("$subjectPathCache", GetSubjectPath(subjectId));
        command.Parameters.AddWithValue("$rootCategoryCache", GetRootCategory(subjectId));
        command.Parameters.AddWithValue("$manualDifficulty", manualDifficulty.HasValue ? manualDifficulty.Value.ToString() : string.Empty);
        command.Parameters.AddWithValue("$modifiedAt", FormatDate(DateTime.Now));
        command.ExecuteNonQuery();
    }

    public void DeleteStudyItem(int itemId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var deleteHistory = connection.CreateCommand())
        {
            deleteHistory.Transaction = transaction;
            deleteHistory.CommandText = "DELETE FROM ReviewHistory WHERE StudyItemId = $itemId;";
            deleteHistory.Parameters.AddWithValue("$itemId", itemId);
            deleteHistory.ExecuteNonQuery();
        }

        using (var deleteItem = connection.CreateCommand())
        {
            deleteItem.Transaction = transaction;
            deleteItem.CommandText = "DELETE FROM StudyItems WHERE Id = $itemId;";
            deleteItem.Parameters.AddWithValue("$itemId", itemId);
            deleteItem.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public StudyItemModel RateStudyItem(int itemId, ReviewRating rating)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var currentItem = LoadStudyItemForUpdate(connection, transaction, itemId);
        var reviewedAt = DateTime.Now;
        var result = SpacedRepetitionCalculator.ApplySm2(
            currentItem.RepetitionCount,
            currentItem.Lapses,
            currentItem.EaseFactor,
            currentItem.IntervalDays,
            rating,
            reviewedAt);

        using (var updateCommand = connection.CreateCommand())
        {
            updateCommand.Transaction = transaction;
            updateCommand.CommandText =
                """
                UPDATE StudyItems
                SET DueDate = $dueDate,
                    Level = $level,
                    TotalReviews = TotalReviews + 1,
                    RepetitionCount = $repetitionCount,
                    Lapses = $lapses,
                    EaseFactor = $easeFactor,
                    IntervalDays = $intervalDays,
                    LastRating = $lastRating,
                    LastReviewedAt = $lastReviewedAt,
                    ModifiedAt = $modifiedAt
                WHERE Id = $itemId;
                """;
            updateCommand.Parameters.AddWithValue("$itemId", itemId);
            updateCommand.Parameters.AddWithValue("$dueDate", FormatDate(result.NextDueDate));
            updateCommand.Parameters.AddWithValue("$level", result.NextLevel);
            updateCommand.Parameters.AddWithValue("$repetitionCount", result.NextRepetitionCount);
            updateCommand.Parameters.AddWithValue("$lapses", result.NextLapses);
            updateCommand.Parameters.AddWithValue("$easeFactor", result.NextEaseFactor);
            updateCommand.Parameters.AddWithValue("$intervalDays", result.NextIntervalDays);
            updateCommand.Parameters.AddWithValue("$lastRating", rating.ToString());
            updateCommand.Parameters.AddWithValue("$lastReviewedAt", FormatDate(reviewedAt));
            updateCommand.Parameters.AddWithValue("$modifiedAt", FormatDate(reviewedAt));
            updateCommand.ExecuteNonQuery();
        }

        using (var clearFlagCommand = connection.CreateCommand())
        {
            clearFlagCommand.Transaction = transaction;
            clearFlagCommand.CommandText = "DELETE FROM ReviewItemFlags WHERE StudyItemId = $itemId;";
            clearFlagCommand.Parameters.AddWithValue("$itemId", itemId);
            clearFlagCommand.ExecuteNonQuery();
        }
        InsertReviewHistory(connection, transaction, itemId, currentItem, result, rating, reviewedAt);
        transaction.Commit();

        return new StudyItemModel
        {
            Id = currentItem.Id,
            SubjectId = currentItem.SubjectId,
            SubjectPath = currentItem.SubjectPath,
            RootCategory = currentItem.RootCategory,
            Topic = currentItem.Topic,
            Question = currentItem.Question,
            Answer = currentItem.Answer,
            CreatedAt = currentItem.CreatedAt,
            ModifiedAt = reviewedAt,
            DueDate = result.NextDueDate,
            Level = result.NextLevel,
            TotalReviews = currentItem.TotalReviews + 1,
            RepetitionCount = result.NextRepetitionCount,
            Lapses = result.NextLapses,
            EaseFactor = result.NextEaseFactor,
            IntervalDays = result.NextIntervalDays,
            LastRating = rating.ToString(),
            LastReviewedAt = reviewedAt
        };
    }

    public void Dispose()
    {
        _sefariaApiService.Dispose();
        _sefariaMishnahService.Dispose();
        _sefariaRambamService.Dispose();
        _sefariaTalmudService.Dispose();
        _sefariaTurService.Dispose();
        _sefariaShulchanArukhService.Dispose();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();

        return connection;
    }
}
