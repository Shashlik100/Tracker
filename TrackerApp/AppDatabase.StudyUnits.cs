using Microsoft.Data.Sqlite;

namespace TrackerApp;

public sealed partial class AppDatabase
{
    private static readonly int[] StudyUnitIntervalsDays = [7, 14, 30, 60];

    public StudyUnitProgressModel? GetStudyUnitProgress(int subjectId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT sup.SubjectId,
                   sup.IsMarkedLearned,
                   sup.FirstStudiedAt,
                   sup.LastStudiedAt,
                   sup.StudyCount,
                   sup.CompletedReviewCount,
                   sup.NextReviewDate,
                   sup.Status,
                   sup.LastResult,
                   sup.LastScore,
                   sup.CurrentStage,
                   sup.LastReviewAt,
                   sup.LastResultSummary,
                   (SELECT COUNT(*) FROM StudyItems si WHERE si.SubjectId = sup.SubjectId) AS LinkedCardCount
            FROM StudyUnitProgress sup
            WHERE sup.SubjectId = $subjectId;
            """;
        command.Parameters.AddWithValue("$subjectId", subjectId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return FinalizeStudyUnitProgress(MapStudyUnitProgress(reader, subjectId));
    }

    public StudyUnitProgressModel MarkSubjectAsLearned(int subjectId, bool restartCycle = false)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var now = DateTime.Now;
        var current = GetStudyUnitProgress(connection, transaction, subjectId);

        var firstStudiedAt = current?.FirstStudiedAt ?? now;
        var lastStudiedAt = now;
        var studyCount = (current?.StudyCount ?? 0) + 1;
        var completedReviewCount = restartCycle ? 0 : current?.CompletedReviewCount ?? 0;
        var currentStage = restartCycle || current is null || !current.IsMarkedLearned || current.IsCycleCompleted
            ? 1
            : Math.Max(1, current.CurrentStage);
        var nextReviewDate = now.Date.AddDays(StudyUnitIntervalsDays[currentStage - 1]);

        UpsertStudyUnitProgress(
            connection,
            transaction,
            subjectId,
            isMarkedLearned: true,
            firstStudiedAt: firstStudiedAt,
            lastStudiedAt: lastStudiedAt,
            studyCount: studyCount,
            completedReviewCount: completedReviewCount,
            nextReviewDate: nextReviewDate,
            status: StudyUnitStatus.WaitingReview,
            lastResult: current?.LastResult,
            lastScore: current?.LastScore ?? 0,
            currentStage: currentStage,
            lastReviewAt: current?.LastReviewAt,
            lastResultSummary: current?.LastResultSummary ?? string.Empty,
            updatedAt: now);

        transaction.Commit();
        return GetStudyUnitProgress(subjectId)!;
    }

    public StudyUnitProgressModel ResetStudyUnitCycle(int subjectId)
    {
        return MarkSubjectAsLearned(subjectId, restartCycle: true);
    }

    public StudyUnitReviewCompletion CompleteStudyUnitReview(int subjectId, IReadOnlyList<ReviewRating> ratings)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var current = GetStudyUnitProgress(connection, transaction, subjectId)
            ?? throw new InvalidOperationException("אין רשומת התקדמות ליחידה זו.");
        if (ratings.Count == 0)
        {
            throw new InvalidOperationException("לא ניתן לסכם חזרת יחידה ללא דירוגי יחידות התרגול.");
        }

        var now = DateTime.Now;
        var stageBefore = Math.Max(1, current.CurrentStage);
        var score = ratings.Average(MapUnitRatingScore);
        var failedCards = ratings.Count(rating => rating == ReviewRating.Again);
        var successfulCards = ratings.Count - failedCards;
        var result = ResolveStudyUnitResult(score);

        var stageAfter = stageBefore;
        StudyUnitStatus status;
        DateTime? nextReviewDate;
        switch (result)
        {
            case StudyUnitResult.Weak:
                status = StudyUnitStatus.EarlyReviewNeeded;
                nextReviewDate = now.Date.AddDays(3);
                break;
            case StudyUnitResult.Medium:
                status = StudyUnitStatus.WaitingReview;
                nextReviewDate = now.Date.AddDays(Math.Max(4, StudyUnitIntervalsDays[stageBefore - 1] / 2));
                break;
            default:
                if (stageBefore >= StudyUnitIntervalsDays.Length)
                {
                    status = StudyUnitStatus.ReviewCompleted;
                    nextReviewDate = null;
                }
                else
                {
                    stageAfter = stageBefore + 1;
                    status = StudyUnitStatus.WaitingReview;
                    nextReviewDate = now.Date.AddDays(StudyUnitIntervalsDays[stageAfter - 1]);
                }
                break;
        }

        var summary =
            $"תוצאת היחידה: {TranslateStudyUnitResult(result)} | ציון: {score:0.##} | " +
            $"יחידות תרגול חזקות: {successfulCards} | יחידות תרגול חלשות: {failedCards}";

        UpsertStudyUnitProgress(
            connection,
            transaction,
            subjectId,
            isMarkedLearned: true,
            firstStudiedAt: current.FirstStudiedAt ?? now,
            lastStudiedAt: current.LastStudiedAt ?? current.FirstStudiedAt ?? now,
            studyCount: Math.Max(1, current.StudyCount),
            completedReviewCount: current.CompletedReviewCount + 1,
            nextReviewDate: nextReviewDate,
            status: status,
            lastResult: result,
            lastScore: score,
            currentStage: stageAfter,
            lastReviewAt: now,
            lastResultSummary: summary,
            updatedAt: now);

        using (var historyCommand = connection.CreateCommand())
        {
            historyCommand.Transaction = transaction;
            historyCommand.CommandText =
                """
                INSERT INTO StudyUnitReviewHistory (
                    SubjectId, ReviewedAt, StageBefore, StageAfter, Result, Score,
                    TotalCards, SuccessfulCards, FailedCards, NextReviewDate, Summary
                )
                VALUES (
                    $subjectId, $reviewedAt, $stageBefore, $stageAfter, $result, $score,
                    $totalCards, $successfulCards, $failedCards, $nextReviewDate, $summary
                );
                """;
            historyCommand.Parameters.AddWithValue("$subjectId", subjectId);
            historyCommand.Parameters.AddWithValue("$reviewedAt", FormatDate(now));
            historyCommand.Parameters.AddWithValue("$stageBefore", stageBefore);
            historyCommand.Parameters.AddWithValue("$stageAfter", stageAfter);
            historyCommand.Parameters.AddWithValue("$result", result.ToString());
            historyCommand.Parameters.AddWithValue("$score", score);
            historyCommand.Parameters.AddWithValue("$totalCards", ratings.Count);
            historyCommand.Parameters.AddWithValue("$successfulCards", successfulCards);
            historyCommand.Parameters.AddWithValue("$failedCards", failedCards);
            historyCommand.Parameters.AddWithValue("$nextReviewDate", nextReviewDate.HasValue ? FormatDate(nextReviewDate.Value) : DBNull.Value);
            historyCommand.Parameters.AddWithValue("$summary", summary);
            historyCommand.ExecuteNonQuery();
        }

        transaction.Commit();

        return new StudyUnitReviewCompletion
        {
            Progress = GetStudyUnitProgress(subjectId)!,
            Result = result,
            Score = score,
            StageBefore = stageBefore,
            StageAfter = stageAfter,
            TotalCards = ratings.Count,
            SuccessfulCards = successfulCards,
            FailedCards = failedCards,
            Summary = summary
        };
    }

    public IReadOnlyList<StudyUnitProgressModel> GetStudyUnitsDueForReview(int? subjectId = null)
    {
        return QueryStudyUnitProgressList(
            subjectId,
            """
            AND sup.IsMarkedLearned = 1
            AND sup.NextReviewDate IS NOT NULL
            AND date(sup.NextReviewDate) <= date('now', 'localtime')
            AND sup.Status <> 'ReviewCompleted'
            """);
    }

    public IReadOnlyList<StudyUnitProgressModel> GetStudyUnitsFailedRecently(int? subjectId = null)
    {
        return QueryStudyUnitProgressList(
            subjectId,
            """
            AND sup.IsMarkedLearned = 1
            AND (
                sup.Status = 'EarlyReviewNeeded'
                OR sup.LastResult = 'Weak'
                OR (
                    sup.LastReviewAt IS NOT NULL
                    AND date(sup.LastReviewAt) >= date('now', '-21 day')
                    AND sup.LastResult IN ('Weak', 'Medium')
                )
            )
            """);
    }

    public int GetTrackedUnitCount(int? subjectId = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = BuildUnitTreeCte(subjectId) +
            """
            SELECT COUNT(*)
            FROM StudyUnitProgress sup
            WHERE sup.IsMarkedLearned = 1
              AND ($subjectId IS NULL OR sup.SubjectId IN (SELECT Id FROM SubjectTree));
            """;
        command.Parameters.AddWithValue("$subjectId", subjectId.HasValue ? subjectId.Value : DBNull.Value);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public Dictionary<int, StudyUnitProgressModel> GetStudyUnitProgressLookup(IEnumerable<int> subjectIds)
    {
        var ids = subjectIds.Distinct().OrderBy(id => id).ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        var parameterNames = new List<string>(ids.Length);
        for (var index = 0; index < ids.Length; index++)
        {
            var parameterName = $"$subject{index}";
            parameterNames.Add(parameterName);
            command.Parameters.AddWithValue(parameterName, ids[index]);
        }

        command.CommandText =
            $"""
             SELECT sup.SubjectId,
                    sup.IsMarkedLearned,
                    sup.FirstStudiedAt,
                    sup.LastStudiedAt,
                    sup.StudyCount,
                    sup.CompletedReviewCount,
                    sup.NextReviewDate,
                    sup.Status,
                    sup.LastResult,
                    sup.LastScore,
                    sup.CurrentStage,
                    sup.LastReviewAt,
                    sup.LastResultSummary,
                    (SELECT COUNT(*) FROM StudyItems si WHERE si.SubjectId = sup.SubjectId) AS LinkedCardCount
             FROM StudyUnitProgress sup
             WHERE sup.SubjectId IN ({string.Join(", ", parameterNames)});
             """;

        using var reader = command.ExecuteReader();
        var lookup = new Dictionary<int, StudyUnitProgressModel>();
        while (reader.Read())
        {
            var subjectId = reader.GetInt32(0);
            lookup[subjectId] = FinalizeStudyUnitProgress(MapStudyUnitProgress(reader, subjectId));
        }

        return lookup;
    }

    public void SetStudyUnitDueDateForVerification(int subjectId, DateTime nextReviewDate, StudyUnitStatus status)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var current = GetStudyUnitProgress(connection, transaction, subjectId);
        var now = DateTime.Now;

        UpsertStudyUnitProgress(
            connection,
            transaction,
            subjectId,
            isMarkedLearned: true,
            firstStudiedAt: current?.FirstStudiedAt ?? now,
            lastStudiedAt: current?.LastStudiedAt ?? now,
            studyCount: Math.Max(1, current?.StudyCount ?? 1),
            completedReviewCount: current?.CompletedReviewCount ?? 0,
            nextReviewDate: nextReviewDate,
            status: status,
            lastResult: current?.LastResult,
            lastScore: current?.LastScore ?? 0,
            currentStage: Math.Max(1, current?.CurrentStage ?? 1),
            lastReviewAt: current?.LastReviewAt,
            lastResultSummary: current?.LastResultSummary ?? string.Empty,
            updatedAt: now);

        transaction.Commit();
    }

    private IReadOnlyList<StudyUnitProgressModel> QueryStudyUnitProgressList(int? subjectId, string predicateSql)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = BuildUnitTreeCte(subjectId) +
            $"""
             SELECT sup.SubjectId,
                    sup.IsMarkedLearned,
                    sup.FirstStudiedAt,
                    sup.LastStudiedAt,
                    sup.StudyCount,
                    sup.CompletedReviewCount,
                    sup.NextReviewDate,
                    sup.Status,
                    sup.LastResult,
                    sup.LastScore,
                    sup.CurrentStage,
                    sup.LastReviewAt,
                    sup.LastResultSummary,
                    (SELECT COUNT(*) FROM StudyItems si WHERE si.SubjectId = sup.SubjectId) AS LinkedCardCount
             FROM StudyUnitProgress sup
             WHERE 1 = 1
             {predicateSql}
               AND ($subjectId IS NULL OR sup.SubjectId IN (SELECT Id FROM SubjectTree))
             ORDER BY COALESCE(sup.NextReviewDate, sup.LastStudiedAt, sup.UpdatedAt) ASC;
             """;
        command.Parameters.AddWithValue("$subjectId", subjectId.HasValue ? subjectId.Value : DBNull.Value);

        using var reader = command.ExecuteReader();
        var items = new List<StudyUnitProgressModel>();
        while (reader.Read())
        {
            var currentSubjectId = reader.GetInt32(0);
            items.Add(FinalizeStudyUnitProgress(MapStudyUnitProgress(reader, currentSubjectId)));
        }

        return items;
    }

    private static string BuildUnitTreeCte(int? subjectId)
    {
        return subjectId.HasValue
            ? """
              WITH RECURSIVE SubjectTree AS (
                  SELECT Id FROM Subjects WHERE Id = $subjectId
                  UNION ALL
                  SELECT s.Id
                  FROM Subjects s
                  INNER JOIN SubjectTree st ON s.ParentId = st.Id
              )
              """
            : """
              WITH RECURSIVE SubjectTree AS (
                  SELECT NULL AS Id
              )
              """;
    }

    private StudyUnitProgressModel? GetStudyUnitProgress(SqliteConnection connection, SqliteTransaction transaction, int subjectId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT sup.SubjectId,
                   sup.IsMarkedLearned,
                   sup.FirstStudiedAt,
                   sup.LastStudiedAt,
                   sup.StudyCount,
                   sup.CompletedReviewCount,
                   sup.NextReviewDate,
                   sup.Status,
                   sup.LastResult,
                   sup.LastScore,
                   sup.CurrentStage,
                   sup.LastReviewAt,
                   sup.LastResultSummary,
                   (SELECT COUNT(*) FROM StudyItems si WHERE si.SubjectId = sup.SubjectId) AS LinkedCardCount
            FROM StudyUnitProgress sup
            WHERE sup.SubjectId = $subjectId;
            """;
        command.Parameters.AddWithValue("$subjectId", subjectId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return FinalizeStudyUnitProgress(MapStudyUnitProgress(reader, subjectId));
    }

    private StudyUnitProgressModel MapStudyUnitProgress(SqliteDataReader reader, int subjectId)
    {
        return new StudyUnitProgressModel
        {
            SubjectId = subjectId,
            SubjectPath = GetSubjectPath(subjectId),
            IsMarkedLearned = reader.GetInt32(1) == 1,
            FirstStudiedAt = reader.IsDBNull(2) ? null : ParseDate(reader.GetString(2)),
            LastStudiedAt = reader.IsDBNull(3) ? null : ParseDate(reader.GetString(3)),
            StudyCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
            CompletedReviewCount = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
            NextReviewDate = reader.IsDBNull(6) ? null : ParseDate(reader.GetString(6)),
            Status = ParseStudyUnitStatus(reader.IsDBNull(7) ? string.Empty : reader.GetString(7)),
            LastResult = ParseStudyUnitResult(reader.IsDBNull(8) ? string.Empty : reader.GetString(8)),
            LastScore = reader.IsDBNull(9) ? 0 : reader.GetDouble(9),
            CurrentStage = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
            LastReviewAt = reader.IsDBNull(11) ? null : ParseDate(reader.GetString(11)),
            LastResultSummary = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
            LinkedCardCount = reader.IsDBNull(13) ? 0 : reader.GetInt32(13)
        };
    }

    private StudyUnitProgressModel FinalizeStudyUnitProgress(StudyUnitProgressModel progress)
    {
        var status = progress.Status;
        if (status != StudyUnitStatus.NotStudied &&
            status != StudyUnitStatus.ReviewCompleted &&
            progress.NextReviewDate.HasValue &&
            progress.NextReviewDate.Value.Date <= DateTime.Today)
        {
            status = progress.Status == StudyUnitStatus.EarlyReviewNeeded
                ? StudyUnitStatus.EarlyReviewNeeded
                : StudyUnitStatus.DueReview;
        }

        return new StudyUnitProgressModel
        {
            SubjectId = progress.SubjectId,
            SubjectPath = progress.SubjectPath,
            IsMarkedLearned = progress.IsMarkedLearned,
            FirstStudiedAt = progress.FirstStudiedAt,
            LastStudiedAt = progress.LastStudiedAt,
            StudyCount = progress.StudyCount,
            CompletedReviewCount = progress.CompletedReviewCount,
            NextReviewDate = progress.NextReviewDate,
            Status = status,
            LastResult = progress.LastResult,
            LastScore = progress.LastScore,
            CurrentStage = progress.CurrentStage,
            LastReviewAt = progress.LastReviewAt,
            LastResultSummary = progress.LastResultSummary,
            LinkedCardCount = progress.LinkedCardCount
        };
    }

    private void UpsertStudyUnitProgress(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int subjectId,
        bool isMarkedLearned,
        DateTime? firstStudiedAt,
        DateTime? lastStudiedAt,
        int studyCount,
        int completedReviewCount,
        DateTime? nextReviewDate,
        StudyUnitStatus status,
        StudyUnitResult? lastResult,
        double lastScore,
        int currentStage,
        DateTime? lastReviewAt,
        string lastResultSummary,
        DateTime updatedAt)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO StudyUnitProgress (
                SubjectId, IsMarkedLearned, FirstStudiedAt, LastStudiedAt, StudyCount,
                CompletedReviewCount, NextReviewDate, Status, LastResult, LastScore,
                CurrentStage, LastReviewAt, LastResultSummary, UpdatedAt
            )
            VALUES (
                $subjectId, $isMarkedLearned, $firstStudiedAt, $lastStudiedAt, $studyCount,
                $completedReviewCount, $nextReviewDate, $status, $lastResult, $lastScore,
                $currentStage, $lastReviewAt, $lastResultSummary, $updatedAt
            )
            ON CONFLICT(SubjectId) DO UPDATE SET
                IsMarkedLearned = excluded.IsMarkedLearned,
                FirstStudiedAt = excluded.FirstStudiedAt,
                LastStudiedAt = excluded.LastStudiedAt,
                StudyCount = excluded.StudyCount,
                CompletedReviewCount = excluded.CompletedReviewCount,
                NextReviewDate = excluded.NextReviewDate,
                Status = excluded.Status,
                LastResult = excluded.LastResult,
                LastScore = excluded.LastScore,
                CurrentStage = excluded.CurrentStage,
                LastReviewAt = excluded.LastReviewAt,
                LastResultSummary = excluded.LastResultSummary,
                UpdatedAt = excluded.UpdatedAt;
            """;
        command.Parameters.AddWithValue("$subjectId", subjectId);
        command.Parameters.AddWithValue("$isMarkedLearned", isMarkedLearned ? 1 : 0);
        command.Parameters.AddWithValue("$firstStudiedAt", firstStudiedAt.HasValue ? FormatDate(firstStudiedAt.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$lastStudiedAt", lastStudiedAt.HasValue ? FormatDate(lastStudiedAt.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$studyCount", studyCount);
        command.Parameters.AddWithValue("$completedReviewCount", completedReviewCount);
        command.Parameters.AddWithValue("$nextReviewDate", nextReviewDate.HasValue ? FormatDate(nextReviewDate.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$status", status.ToString());
        command.Parameters.AddWithValue("$lastResult", lastResult?.ToString() ?? string.Empty);
        command.Parameters.AddWithValue("$lastScore", lastScore);
        command.Parameters.AddWithValue("$currentStage", currentStage);
        command.Parameters.AddWithValue("$lastReviewAt", lastReviewAt.HasValue ? FormatDate(lastReviewAt.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$lastResultSummary", lastResultSummary);
        command.Parameters.AddWithValue("$updatedAt", FormatDate(updatedAt));
        command.ExecuteNonQuery();
    }

    private static double MapUnitRatingScore(ReviewRating rating)
    {
        return rating switch
        {
            ReviewRating.Again => 0,
            ReviewRating.Hard => 1,
            ReviewRating.Good => 2,
            ReviewRating.Easy => 3,
            ReviewRating.Perfect => 4,
            _ => 0
        };
    }

    private static StudyUnitResult ResolveStudyUnitResult(double score)
    {
        return score switch
        {
            < 1.5 => StudyUnitResult.Weak,
            < 2.5 => StudyUnitResult.Medium,
            < 3.5 => StudyUnitResult.Good,
            _ => StudyUnitResult.Excellent
        };
    }

    private static StudyUnitStatus ParseStudyUnitStatus(string value)
    {
        return Enum.TryParse<StudyUnitStatus>(value, out var parsed)
            ? parsed
            : StudyUnitStatus.NotStudied;
    }

    private static StudyUnitResult? ParseStudyUnitResult(string value)
    {
        return Enum.TryParse<StudyUnitResult>(value, out var parsed)
            ? parsed
            : null;
    }

    public static string TranslateStudyUnitStatus(StudyUnitProgressModel? progress)
    {
        if (progress is null || !progress.IsMarkedLearned)
        {
            return "לא נלמד";
        }

        if (progress.IsFirstStudy)
        {
            return progress.Status == StudyUnitStatus.DueReview
                ? "הגיע זמן חזרה ראשונה"
                : "נלמד פעם ראשונה";
        }

        return progress.Status switch
        {
            StudyUnitStatus.WaitingReview => "ממתין לחזרה",
            StudyUnitStatus.DueReview => "הגיע זמן חזרה",
            StudyUnitStatus.ReviewCompleted => "הושלם מחזור",
            StudyUnitStatus.EarlyReviewNeeded => "צריך חזרה מוקדמת",
            _ => "לא נלמד"
        };
    }

    public static string TranslateStudyUnitResult(StudyUnitResult? result)
    {
        return result switch
        {
            StudyUnitResult.Weak => "חלש",
            StudyUnitResult.Medium => "בינוני",
            StudyUnitResult.Good => "טוב",
            StudyUnitResult.Excellent => "מצוין",
            _ => "ללא"
        };
    }
}
