namespace TrackerApp;

public enum LibraryNodeType
{
    Generic = 0,
    Category = 1,
    Section = 2,
    Book = 3,
    Chapter = 4,
    Verse = 5,
    Page = 6,
    Mishnah = 7,
    Halakhah = 8
}

public sealed class SubjectNodeModel
{
    public int Id { get; init; }
    public int? ParentId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DisplayPath { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public bool HasChildren { get; init; }
    public LibraryNodeType NodeType { get; init; } = LibraryNodeType.Generic;
    public string SourceSystem { get; init; } = string.Empty;
    public string SourceKey { get; init; } = string.Empty;
    public StudyUnitStatus UnitStatus { get; init; } = StudyUnitStatus.NotStudied;
    public DateTime? UnitNextReviewDate { get; init; }
    public int UnitCurrentStage { get; init; }
    public int UnitCompletedReviews { get; init; }
    public string UnitStatusText { get; init; } = string.Empty;
}

public sealed class StudyItemModel
{
    public int Id { get; init; }
    public int SubjectId { get; init; }
    public string SubjectPath { get; init; } = string.Empty;
    public string RootCategory { get; init; } = string.Empty;
    public string Topic { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime ModifiedAt { get; init; }
    public DateTime DueDate { get; init; }
    public int Level { get; init; }
    public int TotalReviews { get; init; }
    public int RepetitionCount { get; init; }
    public int Lapses { get; init; }
    public double EaseFactor { get; init; }
    public double IntervalDays { get; init; }
    public string LastRating { get; init; } = string.Empty;
    public DateTime? LastReviewedAt { get; init; }
    public string ManualDifficulty { get; init; } = string.Empty;
    public List<TagModel> Tags { get; init; } = new();
    public bool IsDue => DueDate.Date <= DateTime.Today;
    public bool IsMastered => IntervalDays >= 30 || RepetitionCount >= 5;
    public StudyDifficulty Difficulty => StudyDifficultyClassifier.Resolve(ManualDifficulty, EaseFactor, Lapses, LastRating);
}

public enum ReviewRating
{
    Again = 0,
    Hard = 1,
    Good = 2,
    Easy = 3,
    Perfect = 4
}

public enum ReviewSessionOrderMode
{
    Default = 0,
    Random = 1,
    HardFirst = 2,
    FailedFirst = 3,
    NewFirst = 4
}

public sealed class ReviewResult
{
    public int NextLevel { get; init; }
    public int NextRepetitionCount { get; init; }
    public int NextLapses { get; init; }
    public double NextEaseFactor { get; init; }
    public double NextIntervalDays { get; init; }
    public DateTime NextDueDate { get; init; }
    public bool IsSuccessful { get; init; }
}

public sealed class SubjectCountModel
{
    public string Name { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class ReviewTimelinePoint
{
    public DateTime Day { get; init; }
    public double AverageScore { get; init; }
    public int ReviewCount { get; init; }
}

public sealed class HeatmapDayModel
{
    public DateTime Day { get; init; }
    public int ReviewCount { get; init; }
}

public sealed class TagModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int UsageCount { get; init; }

    public override string ToString() => Name;
}

public enum StudyDifficulty
{
    Any = 0,
    Easy = 1,
    Medium = 2,
    Hard = 3
}

public sealed class StudySearchQuery
{
    public string SearchText { get; init; } = string.Empty;
    public string Book { get; init; } = string.Empty;
    public string Chapter { get; init; } = string.Empty;
    public string Verse { get; init; } = string.Empty;
    public int? SubjectId { get; init; }
    public int? TagId { get; init; }
    public bool LimitToSelectedNode { get; init; }
    public bool FailedRecentlyOnly { get; init; }
    public StudyDifficulty Difficulty { get; init; } = StudyDifficulty.Any;
}

public sealed class StudyReviewFilter
{
    public int? SubjectId { get; init; }
    public int? TagId { get; init; }
    public bool RestrictToSelectedNode { get; init; }
    public bool DueOnly { get; init; } = true;
    public bool FailedRecentlyOnly { get; init; }
    public StudyDifficulty Difficulty { get; init; } = StudyDifficulty.Any;
}

public sealed class DashboardStats
{
    public int TotalItems { get; init; }
    public int DueToday { get; init; }
    public int CompletedToday { get; init; }
    public int MasteredItems { get; init; }
    public double RetentionRate { get; init; }
    public List<SubjectCountModel> ItemsBySubject { get; init; } = new();
    public List<SubjectCountModel> DueBySubject { get; init; } = new();
    public List<SubjectCountModel> MasteredByCategory { get; init; } = new();
    public List<ReviewTimelinePoint> ReviewTimeline { get; init; } = new();
    public List<HeatmapDayModel> Heatmap { get; init; } = new();
}

public enum SmartQueueKind
{
    Daily = 0,
    Overdue = 1,
    FailedRecently = 2,
    Hard = 3,
    New = 4,
    ReviewLater = 5,
    SelectedNode = 6,
    Tag = 7
}

public sealed class DailyDashboardModel
{
    public int DueTodayCount { get; init; }
    public int OverdueCount { get; init; }
    public int NewCount { get; init; }
    public int FailedRecentlyCount { get; init; }
    public int ReviewLaterCount { get; init; }
    public bool HasPausedSession { get; init; }
    public int UnitsStudiedTodayCount { get; init; }
    public int UnitsWaitingCount { get; init; }
    public int UnitsDueCount { get; init; }
    public int UnitsFailedCount { get; init; }
    public int UnitsCompletedCycleCount { get; init; }
    public int UnitsForSelectedNodeCount { get; init; }
}

public sealed class ReviewPresetModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int? SubjectId { get; init; }
    public string SubjectPath { get; init; } = string.Empty;
    public bool RestrictToSubject { get; init; }
    public int? TagId { get; init; }
    public string TagName { get; init; } = string.Empty;
    public bool DueOnly { get; init; }
    public bool FailedRecentlyOnly { get; init; }
    public StudyDifficulty Difficulty { get; init; } = StudyDifficulty.Any;
    public ReviewSessionOrderMode OrderMode { get; init; } = ReviewSessionOrderMode.Default;

    public override string ToString() => Name;
}

public sealed class WeakSpotModel
{
    public string Kind { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public double LowRatingPercent { get; init; }
    public int FailureCount { get; init; }
    public int OverdueCount { get; init; }
    public int ItemCount { get; init; }
}

public sealed class ReviewHistoryModel
{
    public DateTime ReviewedAt { get; init; }
    public ReviewRating Rating { get; init; }
    public bool WasSuccessful { get; init; }
}

public sealed class ReviewSessionProgress
{
    public int TotalCount { get; init; }
    public int CompletedCount { get; init; }
    public int FailedCount { get; init; }
    public int RemainingCount { get; init; }
}

public sealed class ReviewSessionSummary
{
    public string Title { get; init; } = string.Empty;
    public int TotalCount { get; init; }
    public int CompletedCount { get; init; }
    public int FailedCount { get; init; }
    public int HighRatingCount { get; init; }
    public int LowRatingCount { get; init; }
    public int RemainingCount { get; init; }
    public int ReviewLaterCount { get; init; }
    public int SkippedCount { get; init; }
    public List<int> FailedItemIds { get; init; } = new();
    public string AdditionalSummaryText { get; init; } = string.Empty;
}

public sealed class PausedReviewSessionState
{
    public string Title { get; init; } = string.Empty;
    public string ReturnMode { get; init; } = string.Empty;
    public ReviewSessionOrderMode OrderMode { get; init; } = ReviewSessionOrderMode.Default;
    public int? SelectedSubjectId { get; init; }
    public int TotalCount { get; init; }
    public int CompletedCount { get; init; }
    public int FailedCount { get; init; }
    public int HighRatingCount { get; init; }
    public int LowRatingCount { get; init; }
    public List<int> PendingItemIds { get; init; } = new();
    public List<int> FailedItemIds { get; init; } = new();
    public List<int> ReviewLaterItemIds { get; init; } = new();
    public List<int> SkippedItemIds { get; init; } = new();
    public bool IsUnitReviewSession { get; init; }
    public int? UnitSubjectId { get; init; }
    public List<ReviewRating> UnitRatings { get; init; } = new();
    public DateTime UpdatedAt { get; init; }
}

public enum StudyUnitStatus
{
    NotStudied = 0,
    WaitingReview = 1,
    DueReview = 2,
    ReviewCompleted = 3,
    EarlyReviewNeeded = 4
}

public enum StudyUnitResult
{
    Weak = 0,
    Medium = 1,
    Good = 2,
    Excellent = 3
}

public sealed class StudyUnitProgressModel
{
    public int SubjectId { get; init; }
    public string SubjectPath { get; init; } = string.Empty;
    public bool IsMarkedLearned { get; init; }
    public DateTime? FirstStudiedAt { get; init; }
    public DateTime? LastStudiedAt { get; init; }
    public int StudyCount { get; init; }
    public int CompletedReviewCount { get; init; }
    public DateTime? NextReviewDate { get; init; }
    public StudyUnitStatus Status { get; init; } = StudyUnitStatus.NotStudied;
    public StudyUnitResult? LastResult { get; init; }
    public double LastScore { get; init; }
    public int CurrentStage { get; init; }
    public DateTime? LastReviewAt { get; init; }
    public string LastResultSummary { get; init; } = string.Empty;
    public int LinkedCardCount { get; init; }
    public bool IsFirstStudy => StudyCount == 1 && CompletedReviewCount == 0;
    public bool IsCycleCompleted => Status == StudyUnitStatus.ReviewCompleted && !NextReviewDate.HasValue;
}

public sealed class StudyUnitReviewHistoryModel
{
    public int Id { get; init; }
    public int SubjectId { get; init; }
    public DateTime ReviewedAt { get; init; }
    public int StageBefore { get; init; }
    public int StageAfter { get; init; }
    public StudyUnitResult Result { get; init; }
    public double Score { get; init; }
    public int TotalCards { get; init; }
    public int SuccessfulCards { get; init; }
    public int FailedCards { get; init; }
    public DateTime? NextReviewDate { get; init; }
    public string Summary { get; init; } = string.Empty;
}

public sealed class StudyUnitReviewCompletion
{
    public StudyUnitProgressModel Progress { get; init; } = new();
    public StudyUnitResult Result { get; init; }
    public double Score { get; init; }
    public int StageBefore { get; init; }
    public int StageAfter { get; init; }
    public int TotalCards { get; init; }
    public int SuccessfulCards { get; init; }
    public int FailedCards { get; init; }
    public string Summary { get; init; } = string.Empty;
}

public sealed class PrintableScheduleItem
{
    public string SubjectPath { get; init; } = string.Empty;
    public string Topic { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
    public DateTime DueDate { get; init; }
}

public sealed record SefariaBookInfo(string EnglishTitle, string HebrewTitle);

public sealed record TanakhBookShape(string EnglishTitle, string HebrewTitle, IReadOnlyList<int> VerseCountsByChapter);

public sealed record MishnahSederInfo(string EnglishCategory, string HebrewTitle, int Order);

public sealed record MishnahTractateInfo(
    string EnglishTitle,
    string HebrewTitle,
    string EnglishSeder,
    string HebrewSeder,
    int Order);

public sealed record MishnahTractateShape(
    string EnglishTitle,
    string HebrewTitle,
    IReadOnlyList<int> MishnahCountsByChapter);

public sealed record RambamSeferInfo(
    string EnglishCategory,
    string HebrewTitle,
    int Order);

public sealed record RambamHalakhotInfo(
    string EnglishTitle,
    string HebrewTitle,
    string EnglishSefer,
    string HebrewSefer,
    int Order);

public sealed record RambamHalakhotShape(
    string EnglishTitle,
    string HebrewTitle,
    IReadOnlyList<int> HalakhotByChapter);

public sealed record HalakhicSectionInfo(
    string EnglishTitle,
    string HebrewTitle,
    int Order);

public sealed record HalakhicSectionShape(
    string EnglishTitle,
    string HebrewTitle,
    IReadOnlyList<int> UnitCountsBySiman);

public sealed record TalmudCollectionInfo(string CollectionKey, string HebrewTitle, int Order);

public sealed record TalmudSederInfo(
    string CollectionKey,
    string EnglishCategory,
    string HebrewTitle,
    int Order);

public sealed record TalmudTractateInfo(
    string CollectionKey,
    string EnglishTitle,
    string HebrewTitle,
    string EnglishSeder,
    string HebrewSeder,
    int Order);

public sealed record YerushalmiTractateShape(
    string EnglishTitle,
    string HebrewTitle,
    IReadOnlyList<int> HalakhotByChapter);

public sealed class LibraryAuditSummary
{
    public List<string> ExistingRootsBefore { get; init; } = new();
    public List<string> AddedBranches { get; init; } = new();
    public List<string> FixedIssues { get; init; } = new();
    public List<string> CompletedBranches { get; init; } = new();
    public List<string> RemainingLimitations { get; init; } = new();
    public int RenamedNodes { get; init; }
    public int DuplicateGroupsResolved { get; init; }
    public int MergedSubjects { get; init; }
    public int ReassignedStudyItems { get; init; }
    public int ReassignedPresets { get; init; }
}

public sealed class LibraryVerificationSummary
{
    public LibraryAuditSummary Audit { get; init; } = new();
    public int RootCount { get; init; }
    public int LoadedSubjectCount { get; init; }
    public int TanakhSectionCount { get; init; }
    public int TanakhBookCount { get; init; }
    public int TanakhChapterCount { get; init; }
    public int TanakhVerseCount { get; init; }
    public int TalmudCollectionCount { get; init; }
    public int TalmudSederCount { get; init; }
    public int TalmudTractateCount { get; init; }
    public int TalmudChapterCount { get; init; }
    public int TalmudPageCount { get; init; }
    public int BavliSederCount { get; init; }
    public int BavliTractateCount { get; init; }
    public int BavliChapterCount { get; init; }
    public int BavliPageCount { get; init; }
    public int YerushalmiSederCount { get; init; }
    public int YerushalmiTractateCount { get; init; }
    public int YerushalmiChapterCount { get; init; }
    public int YerushalmiHalakhahCount { get; init; }
    public int MishnahSederCount { get; init; }
    public int MishnahTractateCount { get; init; }
    public int MishnahChapterCount { get; init; }
    public int MishnahUnitCount { get; init; }
    public int RambamSeferCount { get; init; }
    public int RambamHalakhotCount { get; init; }
    public int RambamChapterCount { get; init; }
    public int RambamUnitCount { get; init; }
    public int TurSectionCount { get; init; }
    public int TurSimanCount { get; init; }
    public int ShulchanArukhSectionCount { get; init; }
    public int ShulchanArukhSimanCount { get; init; }
    public int ShulchanArukhSeifCount { get; init; }
    public int DuplicateSiblingGroupCount { get; init; }
}

public static class StudyDifficultyClassifier
{
    public static StudyDifficulty Resolve(string manualDifficulty, double easeFactor, int lapses, string lastRating)
    {
        if (Enum.TryParse<StudyDifficulty>(manualDifficulty, true, out var parsed) && parsed != StudyDifficulty.Any)
        {
            return parsed;
        }

        return Classify(easeFactor, lapses, lastRating);
    }

    public static StudyDifficulty Classify(double easeFactor, int lapses, string lastRating)
    {
        if (lapses >= 2 || easeFactor < 2.2 || lastRating is "Again" or "Hard")
        {
            return StudyDifficulty.Hard;
        }

        if (lapses >= 1 || easeFactor < 2.45)
        {
            return StudyDifficulty.Medium;
        }

        return StudyDifficulty.Easy;
    }
}

public sealed class CsvImportPreviewRow
{
    public int RowNumber { get; init; }
    public string Topic { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
    public string SubjectPath { get; init; } = string.Empty;
    public string Difficulty { get; init; } = string.Empty;
    public string Tags { get; init; } = string.Empty;
    public bool IsValid { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class CsvImportPreviewResult
{
    public List<CsvImportPreviewRow> Rows { get; init; } = new();
    public int AcceptedCount { get; init; }
    public int RejectedCount { get; init; }
}

public enum CsvFieldType
{
    Ignore = 0,
    Topic = 1,
    Question = 2,
    Answer = 3,
    Book = 4,
    Chapter = 5,
    Verse = 6,
    Difficulty = 7,
    Tags = 8
}

public sealed class CsvImportMapping
{
    public Dictionary<int, CsvFieldType> ColumnMappings { get; init; } = new();
    public int? FallbackSubjectId { get; init; }
    public bool HasHeaderRow { get; init; } = true;
}

public sealed class SchemaMigrationInfo
{
    public int Version { get; init; }
    public string Description { get; init; } = string.Empty;
}

public sealed class SchemaMigrationSummary
{
    public int PreviousVersion { get; init; }
    public int CurrentVersion { get; init; }
    public string SafetyBackupPath { get; init; } = string.Empty;
    public List<SchemaMigrationInfo> AppliedMigrations { get; init; } = new();
}

public sealed class ValidationIssueModel
{
    public string Area { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public bool WasFixed { get; init; }
}

public sealed class DatabaseValidationReport
{
    public DateTime GeneratedAt { get; init; }
    public string DatabasePath { get; init; } = string.Empty;
    public int SchemaVersion { get; init; }
    public string SafetyBackupPath { get; init; } = string.Empty;
    public int DuplicateSiblingGroupsBefore { get; init; }
    public int DuplicateSiblingGroupsAfter { get; init; }
    public int OrphanSubjectsFixed { get; init; }
    public int BrokenStudyItemLinksFixed { get; init; }
    public int BrokenTagLinksFixed { get; init; }
    public int BrokenReviewHistoryLinksFixed { get; init; }
    public int BrokenFlagLinksFixed { get; init; }
    public int BrokenPresetLinksFixed { get; init; }
    public int BrokenPausedSessionsFixed { get; init; }
    public int InvalidSourceKeysNormalized { get; init; }
    public List<ValidationIssueModel> Issues { get; init; } = new();
    public List<string> RepairActions { get; init; } = new();
    public List<string> RemainingIssues { get; init; } = new();
}

public sealed class BackupRestoreReport
{
    public DateTime ExecutedAt { get; init; }
    public string SourcePath { get; init; } = string.Empty;
    public string BackupPath { get; init; } = string.Empty;
    public string RestoreSourcePath { get; init; } = string.Empty;
    public string RollbackBackupPath { get; init; } = string.Empty;
    public bool BackupVerified { get; init; }
    public bool RestoreVerified { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class UserDataResetCounts
{
    public int StudyItemCount { get; init; }
    public int TagCount { get; init; }
    public int StudyItemTagCount { get; init; }
    public int ReviewHistoryCount { get; init; }
    public int ReviewPresetCount { get; init; }
    public int SavedReviewSessionCount { get; init; }
    public int ReviewItemFlagCount { get; init; }
    public int StudyUnitProgressCount { get; init; }
    public int StudyUnitReviewHistoryCount { get; init; }
}

public sealed class UserDataResetReport
{
    public DateTime ExecutedAt { get; init; }
    public string DatabasePath { get; init; } = string.Empty;
    public string SafetyBackupPath { get; init; } = string.Empty;
    public UserDataResetCounts Before { get; init; } = new();
    public UserDataResetCounts After { get; init; } = new();
    public int PreservedSubjectCount { get; init; }
    public int PreservedRootCount { get; init; }
    public List<string> PreservedRoots { get; init; } = new();
    public List<string> ClearedAreas { get; init; } = new();
    public string Message { get; init; } = string.Empty;
}

public sealed class PerformanceSample
{
    public string Name { get; init; } = string.Empty;
    public double Milliseconds { get; init; }
    public string Notes { get; init; } = string.Empty;
}
