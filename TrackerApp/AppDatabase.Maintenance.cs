using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace TrackerApp;

public sealed partial class AppDatabase
{
    private readonly object _maintenanceSync = new();

    public string GetBackupsFolder()
    {
        var folder = Path.Combine(Path.GetDirectoryName(_databasePath)!, "backups");
        Directory.CreateDirectory(folder);
        return folder;
    }

    public string CreateAutomaticBackupSnapshot(string reason)
    {
        using var connection = OpenConnection();
        return CreateAutomaticBackupSnapshot(connection, reason);
    }

    public void BackupDatabase(string filePath)
    {
        using var connection = OpenConnection();
        BackupDatabase(connection, filePath);
    }

    public BackupRestoreReport RestoreDatabase(string backupPath)
    {
        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException("קובץ הגיבוי שנבחר לא נמצא.", backupPath);
        }

        lock (_maintenanceSync)
        {
            var executedAt = DateTime.Now;
            var rollbackBackupPath = CreateAutomaticBackupSnapshot("restore-rollback");
            var tempValidationPath = Path.Combine(Path.GetTempPath(), $"tracker-restore-{Guid.NewGuid():N}.db");

            try
            {
                SqliteConnection.ClearAllPools();
                File.Copy(backupPath, tempValidationPath, true);
                ValidateSqliteFile(tempValidationPath);

                File.Copy(backupPath, _databasePath, true);
                Initialize();

                return new BackupRestoreReport
                {
                    ExecutedAt = executedAt,
                    SourcePath = _databasePath,
                    BackupPath = backupPath,
                    RestoreSourcePath = backupPath,
                    RollbackBackupPath = rollbackBackupPath,
                    BackupVerified = true,
                    RestoreVerified = true,
                    Message = "השחזור הושלם בהצלחה והמסד נפתח מחדש ללא שגיאות."
                };
            }
            catch
            {
                try
                {
                    SqliteConnection.ClearAllPools();
                    File.Copy(rollbackBackupPath, _databasePath, true);
                    Initialize();
                }
                catch
                {
                }

                throw;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempValidationPath))
                    {
                        File.Delete(tempValidationPath);
                    }
                }
                catch
                {
                }
            }
        }
    }

    public DatabaseValidationReport ValidateAndRepairDatabase(bool autoRepair)
    {
        lock (_maintenanceSync)
        {
            var safetyBackupPath = CreateAutomaticBackupSnapshot("validation-repair");
            using var connection = OpenConnection();
            using var transaction = connection.BeginTransaction();

            var issues = new List<ValidationIssueModel>();
            var repairActions = new List<string>();
            var remainingIssues = new List<string>();

            var duplicateGroupsBefore = CountDuplicateSiblingGroups(connection, transaction);
            var orphanSubjectsFixed = FixOrphanSubjects(connection, transaction, issues, repairActions, autoRepair);
            var brokenStudyLinksFixed = FixBrokenStudyItemSubjectLinks(connection, transaction, issues, repairActions, remainingIssues, autoRepair);
            var brokenTagLinksFixed = FixBrokenJoinLinks(
                connection,
                transaction,
                "StudyItemTags",
                "NOT EXISTS(SELECT 1 FROM StudyItems i WHERE i.Id = StudyItemTags.StudyItemId) OR NOT EXISTS(SELECT 1 FROM Tags t WHERE t.Id = StudyItemTags.TagId)",
                "נוקו שיוכי תגיות שבורים.",
                "תגיות",
                issues,
                repairActions,
                autoRepair);

            var brokenReviewHistoryFixed = FixBrokenJoinLinks(
                connection,
                transaction,
                "ReviewHistory",
                "NOT EXISTS(SELECT 1 FROM StudyItems i WHERE i.Id = ReviewHistory.StudyItemId)",
                "נוקתה היסטוריית חזרה ללא יחידת לימוד מקורית.",
                "היסטוריית חזרה",
                issues,
                repairActions,
                autoRepair);

            var brokenFlagLinksFixed = FixBrokenJoinLinks(
                connection,
                transaction,
                "ReviewItemFlags",
                "NOT EXISTS(SELECT 1 FROM StudyItems i WHERE i.Id = ReviewItemFlags.StudyItemId)",
                "נוקו דגלי עיון חוזר שבורים.",
                "דגלי חזרה",
                issues,
                repairActions,
                autoRepair);

            var brokenPresetLinksFixed = FixBrokenPresetLinks(connection, transaction, issues, repairActions, autoRepair);
            var brokenPausedSessionsFixed = FixBrokenPausedSessions(connection, transaction, issues, repairActions, autoRepair);

            var auditSummary = autoRepair
                ? AuditAndNormalizeLibrary(connection, transaction)
                : GetLibraryAuditSummary();

            var invalidSourceKeysNormalized = auditSummary.FixedIssues.Count(issue => issue.Contains("מפתחות", StringComparison.Ordinal));
            var duplicateGroupsAfter = autoRepair ? CountDuplicateSiblingGroups(connection, transaction) : duplicateGroupsBefore;

            transaction.Commit();
            _subjects = LoadSubjects(connection);
            _libraryAuditSummary = auditSummary;

            return new DatabaseValidationReport
            {
                GeneratedAt = DateTime.Now,
                DatabasePath = _databasePath,
                SchemaVersion = GetSchemaVersion(),
                SafetyBackupPath = safetyBackupPath,
                DuplicateSiblingGroupsBefore = duplicateGroupsBefore,
                DuplicateSiblingGroupsAfter = duplicateGroupsAfter,
                OrphanSubjectsFixed = orphanSubjectsFixed,
                BrokenStudyItemLinksFixed = brokenStudyLinksFixed,
                BrokenTagLinksFixed = brokenTagLinksFixed,
                BrokenReviewHistoryLinksFixed = brokenReviewHistoryFixed,
                BrokenFlagLinksFixed = brokenFlagLinksFixed,
                BrokenPresetLinksFixed = brokenPresetLinksFixed,
                BrokenPausedSessionsFixed = brokenPausedSessionsFixed,
                InvalidSourceKeysNormalized = invalidSourceKeysNormalized,
                Issues = issues,
                RepairActions = repairActions,
                RemainingIssues = remainingIssues
            };
        }
    }

    public string BuildValidationMarkdownReport(DatabaseValidationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# דוח בדיקה ותיקון למסד");
        builder.AppendLine();
        builder.AppendLine($"- זמן: {report.GeneratedAt:dd/MM/yyyy HH:mm:ss}");
        builder.AppendLine($"- מסד: `{report.DatabasePath}`");
        builder.AppendLine($"- גרסת סכימה: `{report.SchemaVersion}`");
        builder.AppendLine($"- גיבוי בטיחות: `{report.SafetyBackupPath}`");
        builder.AppendLine();
        builder.AppendLine("## ממצאים עיקריים");
        builder.AppendLine($"- כפילויות בין אחים לפני תיקון: `{report.DuplicateSiblingGroupsBefore}`");
        builder.AppendLine($"- כפילויות בין אחים אחרי תיקון: `{report.DuplicateSiblingGroupsAfter}`");
        builder.AppendLine($"- orphan nodes שתוקנו: `{report.OrphanSubjectsFixed}`");
        builder.AppendLine($"- קישורי יחידות לימוד שבורים שתוקנו: `{report.BrokenStudyItemLinksFixed}`");
        builder.AppendLine($"- שיוכי תגיות שבורים שנוקו: `{report.BrokenTagLinksFixed}`");
        builder.AppendLine($"- שורות היסטוריית חזרה שבורות שנוקו: `{report.BrokenReviewHistoryLinksFixed}`");
        builder.AppendLine($"- דגלי עיון חוזר שבורים שנוקו: `{report.BrokenFlagLinksFixed}`");
        builder.AppendLine($"- presets שתוקנו: `{report.BrokenPresetLinksFixed}`");
        builder.AppendLine($"- סשנים מושהים שתוקנו: `{report.BrokenPausedSessionsFixed}`");
        builder.AppendLine($"- נרמולי source keys: `{report.InvalidSourceKeysNormalized}`");
        builder.AppendLine();
        builder.AppendLine("## פעולות תיקון");
        if (report.RepairActions.Count == 0)
        {
            builder.AppendLine("- לא נדרשו פעולות תיקון אוטומטיות.");
        }
        else
        {
            foreach (var action in report.RepairActions)
            {
                builder.AppendLine($"- {action}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## ממצאים מפורטים");
        if (report.Issues.Count == 0)
        {
            builder.AppendLine("- לא נמצאו בעיות במסד.");
        }
        else
        {
            foreach (var issue in report.Issues)
            {
                builder.AppendLine($"- [{issue.Area}] {issue.Message} | חומרה: {issue.Severity} | תוקן: {(issue.WasFixed ? "כן" : "לא")}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## מה לא הושלם");
        if (report.RemainingIssues.Count == 0)
        {
            builder.AppendLine("- לא נשארו בעיות פתוחות לאחר הריצה.");
        }
        else
        {
            foreach (var issue in report.RemainingIssues)
            {
                builder.AppendLine($"- {issue}");
            }
        }

        return builder.ToString();
    }

    public string BuildBackupRestoreMarkdownReport(BackupRestoreReport report)
    {
        return $"""
                # דוח גיבוי ושחזור

                - זמן: {report.ExecutedAt:dd/MM/yyyy HH:mm:ss}
                - מסד פעיל: `{report.SourcePath}`
                - קובץ גיבוי: `{report.BackupPath}`
                - קובץ שחזור: `{report.RestoreSourcePath}`
                - גיבוי rollback: `{report.RollbackBackupPath}`
                - אימות גיבוי: `{(report.BackupVerified ? "עבר" : "נכשל")}`
                - אימות שחזור: `{(report.RestoreVerified ? "עבר" : "נכשל")}`

                ## סיכום
                {report.Message}
                """;
    }

    public UserDataResetReport ResetUserData()
    {
        lock (_maintenanceSync)
        {
            using var connection = OpenConnection();
            var executedAt = DateTime.Now;
            var safetyBackupPath = CreateAutomaticBackupSnapshot(connection, "reset-user-data");
            using var transaction = connection.BeginTransaction();

            var before = GetUserDataResetCounts(connection, transaction);
            var preservedRoots = GetRootNames(connection, transaction);

            ExecuteNonQuery(connection, transaction, "DELETE FROM StudyItemTags;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM ReviewHistory;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM ReviewItemFlags;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM StudyUnitReviewHistory;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM StudyUnitProgress;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM SavedReviewSessions;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM ReviewPresets;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM StudyItems;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM Tags;");
            ExecuteNonQuery(
                connection,
                transaction,
                "DELETE FROM sqlite_sequence WHERE name IN ('StudyItems', 'ReviewHistory', 'Tags', 'SavedReviewSessions', 'ReviewPresets', 'StudyUnitReviewHistory');");

            var after = GetUserDataResetCounts(connection, transaction);
            transaction.Commit();

            RefreshSubjectCache();

            return new UserDataResetReport
            {
                ExecutedAt = executedAt,
                DatabasePath = _databasePath,
                SafetyBackupPath = safetyBackupPath,
                Before = before,
                After = after,
                PreservedSubjectCount = _subjects.Count,
                PreservedRootCount = preservedRoots.Count,
                PreservedRoots = preservedRoots,
                ClearedAreas =
                [
                    "יחידות לימוד",
                    "תגיות ושיוכי תגיות",
                    "היסטוריית חזרות",
                    "פריסטים של חזרה",
                    "סשנים מושהים",
                    "דגלי עיון חוזר",
                    "מעקב לימוד לפי יחידה",
                    "היסטוריית יחידות"
                ],
                Message = "נתוני המשתמש אופסו בהצלחה. הספרייה המובנית, הסכמה והמיגרציות נשמרו ללא שינוי."
            };
        }
    }

    public string BuildUserDataResetMarkdownReport(UserDataResetReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# דוח איפוס נתוני משתמש");
        builder.AppendLine();
        builder.AppendLine($"- זמן: {report.ExecutedAt:dd/MM/yyyy HH:mm:ss}");
        builder.AppendLine($"- מסד פעיל: `{report.DatabasePath}`");
        builder.AppendLine($"- גיבוי בטיחות: `{report.SafetyBackupPath}`");
        builder.AppendLine();
        builder.AppendLine("## מה אופס");
        foreach (var area in report.ClearedAreas)
        {
            builder.AppendLine($"- {area}");
        }

        builder.AppendLine();
        builder.AppendLine("## מה נשמר");
        builder.AppendLine($"- צמתי ספרייה שנשמרו: `{report.PreservedSubjectCount}`");
        builder.AppendLine($"- שורשים קנוניים שנשמרו: `{report.PreservedRootCount}`");
        builder.AppendLine($"- רשימת שורשים: {string.Join(", ", report.PreservedRoots)}");
        builder.AppendLine("- הסכמה, המיגרציות והעץ המובנה נשמרו.");
        builder.AppendLine();
        builder.AppendLine("## ספירות לפני ואחרי");
        builder.AppendLine($"- יחידות לימוד: `{report.Before.StudyItemCount}` -> `{report.After.StudyItemCount}`");
        builder.AppendLine($"- תגיות: `{report.Before.TagCount}` -> `{report.After.TagCount}`");
        builder.AppendLine($"- שיוכי תגיות: `{report.Before.StudyItemTagCount}` -> `{report.After.StudyItemTagCount}`");
        builder.AppendLine($"- היסטוריית חזרות: `{report.Before.ReviewHistoryCount}` -> `{report.After.ReviewHistoryCount}`");
        builder.AppendLine($"- presets: `{report.Before.ReviewPresetCount}` -> `{report.After.ReviewPresetCount}`");
        builder.AppendLine($"- סשנים מושהים: `{report.Before.SavedReviewSessionCount}` -> `{report.After.SavedReviewSessionCount}`");
        builder.AppendLine($"- דגלי עיון חוזר: `{report.Before.ReviewItemFlagCount}` -> `{report.After.ReviewItemFlagCount}`");
        builder.AppendLine($"- מעקב יחידות: `{report.Before.StudyUnitProgressCount}` -> `{report.After.StudyUnitProgressCount}`");
        builder.AppendLine($"- היסטוריית יחידות: `{report.Before.StudyUnitReviewHistoryCount}` -> `{report.After.StudyUnitReviewHistoryCount}`");
        builder.AppendLine();
        builder.AppendLine("## סיכום");
        builder.AppendLine(report.Message);
        return builder.ToString();
    }

    private string CreateAutomaticBackupSnapshot(SqliteConnection connection, string reason)
    {
        var sanitizedReason = string.Concat(reason.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'));
        var backupPath = Path.Combine(
            GetBackupsFolder(),
            $"tracker-{DateTime.Now:yyyyMMdd-HHmmss}-{sanitizedReason}.db");
        BackupDatabase(connection, backupPath);
        return backupPath;
    }

    private static void BackupDatabase(SqliteConnection connection, string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var escapedPath = filePath.Replace("'", "''", StringComparison.Ordinal);
        using var command = connection.CreateCommand();
        command.CommandText = $"VACUUM INTO '{escapedPath}';";
        command.ExecuteNonQuery();
        ValidateSqliteFile(filePath);
    }

    private static void ValidateSqliteFile(string filePath)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = filePath }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? string.Empty;
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"בדיקת integrity_check למסד נכשלה: {result}");
        }
    }

    private int FixOrphanSubjects(
        SqliteConnection connection,
        SqliteTransaction transaction,
        List<ValidationIssueModel> issues,
        List<string> actions,
        bool autoRepair)
    {
        using var selectCommand = connection.CreateCommand();
        selectCommand.Transaction = transaction;
        selectCommand.CommandText =
            """
            SELECT s.Id
            FROM Subjects s
            LEFT JOIN Subjects parent ON parent.Id = s.ParentId
            WHERE s.ParentId IS NOT NULL
              AND parent.Id IS NULL;
            """;

        using var reader = selectCommand.ExecuteReader();
        var orphanIds = new List<int>();
        while (reader.Read())
        {
            orphanIds.Add(reader.GetInt32(0));
        }

        foreach (var orphanId in orphanIds)
        {
            issues.Add(new ValidationIssueModel
            {
                Area = "ספרייה",
                Severity = "אזהרה",
                Message = $"נמצא צומת ספרייה יתום (Id={orphanId}).",
                WasFixed = autoRepair
            });
        }

        if (!autoRepair || orphanIds.Count == 0)
        {
            return 0;
        }

        using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText = $"UPDATE Subjects SET ParentId = NULL WHERE Id IN ({string.Join(",", orphanIds)});";
        updateCommand.ExecuteNonQuery();
        actions.Add($"הועברו {orphanIds.Count} צמתי ספרייה יתומים לרמת שורש.");
        return orphanIds.Count;
    }

    private int FixBrokenStudyItemSubjectLinks(
        SqliteConnection connection,
        SqliteTransaction transaction,
        List<ValidationIssueModel> issues,
        List<string> actions,
        List<string> remainingIssues,
        bool autoRepair)
    {
        using var selectCommand = connection.CreateCommand();
        selectCommand.Transaction = transaction;
        selectCommand.CommandText =
            """
            SELECT i.Id, i.SubjectPathCache
            FROM StudyItems i
            LEFT JOIN Subjects s ON s.Id = i.SubjectId
            WHERE s.Id IS NULL;
            """;

        using var reader = selectCommand.ExecuteReader();
        var brokenItems = new List<(int Id, string Path)>();
        while (reader.Read())
        {
            brokenItems.Add((reader.GetInt32(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1)));
        }

        var fixedCount = 0;
        foreach (var brokenItem in brokenItems)
        {
            var repaired = false;
            if (autoRepair && !string.IsNullOrWhiteSpace(brokenItem.Path))
            {
                var subjectId = TryResolveSubjectIdByPath(brokenItem.Path);
                if (subjectId.HasValue)
                {
                    using var updateCommand = connection.CreateCommand();
                    updateCommand.Transaction = transaction;
                    updateCommand.CommandText =
                        """
                        UPDATE StudyItems
                        SET SubjectId = $subjectId,
                            SubjectPathCache = $subjectPath,
                            RootCategoryCache = $rootCategory
                        WHERE Id = $itemId;
                        """;
                    updateCommand.Parameters.AddWithValue("$itemId", brokenItem.Id);
                    updateCommand.Parameters.AddWithValue("$subjectId", subjectId.Value);
                    updateCommand.Parameters.AddWithValue("$subjectPath", GetSubjectPath(subjectId.Value));
                    updateCommand.Parameters.AddWithValue("$rootCategory", GetRootCategory(subjectId.Value));
                    updateCommand.ExecuteNonQuery();
                    repaired = true;
                    fixedCount++;
                }
            }

            issues.Add(new ValidationIssueModel
            {
                Area = "יחידות לימוד",
                Severity = repaired ? "אזהרה" : "שגיאה",
                Message = repaired
                    ? $"יחידת לימוד {brokenItem.Id} שויכה מחדש לצומת ספרייה תקף."
                    : $"יחידת לימוד {brokenItem.Id} איבדה קישור לספרייה ולא ניתן היה לשחזר אותו אוטומטית.",
                WasFixed = repaired
            });

            if (!repaired)
            {
                remainingIssues.Add($"יחידת לימוד {brokenItem.Id} דורשת שיוך ידני מחדש לספרייה.");
            }
        }

        if (fixedCount > 0)
        {
            actions.Add($"שוחזרו {fixedCount} קישורי יחידות לימוד לצמתים קיימים לפי הנתיב השמור.");
        }

        return fixedCount;
    }

    private static int FixBrokenJoinLinks(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        string whereClause,
        string successMessage,
        string areaName,
        List<ValidationIssueModel> issues,
        List<string> actions,
        bool autoRepair)
    {
        using var countCommand = connection.CreateCommand();
        countCommand.Transaction = transaction;
        countCommand.CommandText = $"SELECT COUNT(*) FROM {tableName} WHERE {whereClause};";
        var count = Convert.ToInt32(countCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (count == 0)
        {
            return 0;
        }

        issues.Add(new ValidationIssueModel
        {
            Area = areaName,
            Severity = "אזהרה",
            Message = $"נמצאו {count} רשומות שבורות בטבלה {tableName}.",
            WasFixed = autoRepair
        });

        if (!autoRepair)
        {
            return 0;
        }

        using var deleteCommand = connection.CreateCommand();
        deleteCommand.Transaction = transaction;
        deleteCommand.CommandText = $"DELETE FROM {tableName} WHERE {whereClause};";
        deleteCommand.ExecuteNonQuery();
        actions.Add($"{successMessage} ({count} רשומות).");
        return count;
    }

    private static int FixBrokenPresetLinks(
        SqliteConnection connection,
        SqliteTransaction transaction,
        List<ValidationIssueModel> issues,
        List<string> actions,
        bool autoRepair)
    {
        using var countCommand = connection.CreateCommand();
        countCommand.Transaction = transaction;
        countCommand.CommandText =
            """
            SELECT COUNT(*)
            FROM ReviewPresets rp
            LEFT JOIN Subjects s ON s.Id = rp.SubjectId
            LEFT JOIN Tags t ON t.Id = rp.TagId
            WHERE (rp.SubjectId IS NOT NULL AND s.Id IS NULL)
               OR (rp.TagId IS NOT NULL AND t.Id IS NULL);
            """;
        var count = Convert.ToInt32(countCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (count == 0)
        {
            return 0;
        }

        issues.Add(new ValidationIssueModel
        {
            Area = "פריסטים",
            Severity = "אזהרה",
            Message = $"נמצאו {count} פריסטים עם קישור שבור לצומת או לתגית.",
            WasFixed = autoRepair
        });

        if (!autoRepair)
        {
            return 0;
        }

        using var updateCommand = connection.CreateCommand();
        updateCommand.Transaction = transaction;
        updateCommand.CommandText =
            """
            UPDATE ReviewPresets
            SET SubjectId = CASE
                    WHEN SubjectId IS NOT NULL
                     AND NOT EXISTS(SELECT 1 FROM Subjects s WHERE s.Id = ReviewPresets.SubjectId)
                    THEN NULL ELSE SubjectId END,
                RestrictToSubject = CASE
                    WHEN SubjectId IS NOT NULL
                     AND NOT EXISTS(SELECT 1 FROM Subjects s WHERE s.Id = ReviewPresets.SubjectId)
                    THEN 0 ELSE RestrictToSubject END,
                TagId = CASE
                    WHEN TagId IS NOT NULL
                     AND NOT EXISTS(SELECT 1 FROM Tags t WHERE t.Id = ReviewPresets.TagId)
                    THEN NULL ELSE TagId END
            WHERE (SubjectId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Subjects s WHERE s.Id = ReviewPresets.SubjectId))
               OR (TagId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Tags t WHERE t.Id = ReviewPresets.TagId));
            """;
        updateCommand.ExecuteNonQuery();
        actions.Add($"נוקו הפניות שבורות מתוך {count} פריסטים.");
        return count;
    }

    private static int FixBrokenPausedSessions(
        SqliteConnection connection,
        SqliteTransaction transaction,
        List<ValidationIssueModel> issues,
        List<string> actions,
        bool autoRepair)
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
        var sessions = new List<(int Id, string Payload)>();
        while (reader.Read())
        {
            sessions.Add((reader.GetInt32(0), reader.GetString(1)));
        }

        var fixedCount = 0;
        foreach (var session in sessions)
        {
            PausedReviewSessionState? state;
            try
            {
                state = JsonSerializer.Deserialize<PausedReviewSessionState>(session.Payload);
            }
            catch
            {
                state = null;
            }

            if (state is null)
            {
                issues.Add(new ValidationIssueModel
                {
                    Area = "סשנים מושהים",
                    Severity = "אזהרה",
                    Message = $"סשן מושהה {session.Id} מכיל payload פגום.",
                    WasFixed = autoRepair
                });

                if (autoRepair)
                {
                    using var deleteCommand = connection.CreateCommand();
                    deleteCommand.Transaction = transaction;
                    deleteCommand.CommandText = "DELETE FROM SavedReviewSessions WHERE Id = $id;";
                    deleteCommand.Parameters.AddWithValue("$id", session.Id);
                    deleteCommand.ExecuteNonQuery();
                    fixedCount++;
                }

                continue;
            }

            var validPendingIds = state.PendingItemIds.Where(id => StudyItemExists(connection, transaction, id)).ToList();
            var validFailedIds = state.FailedItemIds.Where(id => StudyItemExists(connection, transaction, id)).ToList();
            var validReviewLaterIds = state.ReviewLaterItemIds.Where(id => StudyItemExists(connection, transaction, id)).ToList();
            var validSkippedIds = state.SkippedItemIds.Where(id => StudyItemExists(connection, transaction, id)).ToList();

            if (validPendingIds.Count == state.PendingItemIds.Count &&
                validFailedIds.Count == state.FailedItemIds.Count &&
                validReviewLaterIds.Count == state.ReviewLaterItemIds.Count &&
                validSkippedIds.Count == state.SkippedItemIds.Count)
            {
                continue;
            }

            issues.Add(new ValidationIssueModel
            {
                Area = "סשנים מושהים",
                Severity = "אזהרה",
                Message = $"סשן מושהה {session.Id} הכיל יחידות לימוד שכבר אינן קיימות.",
                WasFixed = autoRepair
            });

            if (!autoRepair)
            {
                continue;
            }

            if (validPendingIds.Count == 0)
            {
                using var deleteCommand = connection.CreateCommand();
                deleteCommand.Transaction = transaction;
                deleteCommand.CommandText = "DELETE FROM SavedReviewSessions WHERE Id = $id;";
                deleteCommand.Parameters.AddWithValue("$id", session.Id);
                deleteCommand.ExecuteNonQuery();
            }
            else
            {
                var updatedState = new PausedReviewSessionState
                {
                    Title = state.Title,
                    ReturnMode = state.ReturnMode,
                    OrderMode = state.OrderMode,
                    SelectedSubjectId = state.SelectedSubjectId,
                    TotalCount = state.CompletedCount + validPendingIds.Count,
                    CompletedCount = state.CompletedCount,
                    FailedCount = state.FailedCount,
                    HighRatingCount = state.HighRatingCount,
                    LowRatingCount = state.LowRatingCount,
                    PendingItemIds = validPendingIds,
                    FailedItemIds = validFailedIds,
                    ReviewLaterItemIds = validReviewLaterIds,
                    SkippedItemIds = validSkippedIds,
                    UpdatedAt = DateTime.Now
                };

                using var updateCommand = connection.CreateCommand();
                updateCommand.Transaction = transaction;
                updateCommand.CommandText = "UPDATE SavedReviewSessions SET Payload = $payload, UpdatedAt = $updatedAt WHERE Id = $id;";
                updateCommand.Parameters.AddWithValue("$id", session.Id);
                updateCommand.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(updatedState));
                updateCommand.Parameters.AddWithValue("$updatedAt", FormatDate(DateTime.Now));
                updateCommand.ExecuteNonQuery();
            }

            fixedCount++;
        }

        if (fixedCount > 0)
        {
            actions.Add($"תוקנו {fixedCount} סשנים מושהים עם payload שבור או עם יחידות לימוד חסרות.");
        }

        return fixedCount;
    }

    private static bool StudyItemExists(SqliteConnection connection, SqliteTransaction transaction, int itemId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM StudyItems WHERE Id = $id);";
        command.Parameters.AddWithValue("$id", itemId);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
    }

    private static UserDataResetCounts GetUserDataResetCounts(SqliteConnection connection, SqliteTransaction transaction)
    {
        return new UserDataResetCounts
        {
            StudyItemCount = CountRows(connection, transaction, "StudyItems"),
            TagCount = CountRows(connection, transaction, "Tags"),
            StudyItemTagCount = CountRows(connection, transaction, "StudyItemTags"),
            ReviewHistoryCount = CountRows(connection, transaction, "ReviewHistory"),
            ReviewPresetCount = CountRows(connection, transaction, "ReviewPresets"),
            SavedReviewSessionCount = CountRows(connection, transaction, "SavedReviewSessions"),
            ReviewItemFlagCount = CountRows(connection, transaction, "ReviewItemFlags"),
            StudyUnitProgressCount = CountRows(connection, transaction, "StudyUnitProgress"),
            StudyUnitReviewHistoryCount = CountRows(connection, transaction, "StudyUnitReviewHistory")
        };
    }

    private static int CountRows(SqliteConnection connection, SqliteTransaction transaction, string tableName)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT COUNT(*) FROM {tableName};";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private int? TryResolveSubjectIdByPath(string path)
    {
        var normalized = path.Replace(" / ", " > ", StringComparison.Ordinal);
        var subject = _subjects.FirstOrDefault(candidate => string.Equals(GetSubjectPath(candidate.Id), normalized, StringComparison.Ordinal));
        return subject?.Id;
    }
}
