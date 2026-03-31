using System.Globalization;
using Microsoft.Data.Sqlite;

namespace TrackerApp;

public sealed partial class AppDatabase
{
    private const string SchemaVersionKey = "SchemaVersion";
    private const int CurrentSchemaVersion = 4;

    private SchemaMigrationSummary _lastMigrationSummary = new();

    public int GetSchemaVersion()
    {
        using var connection = OpenConnection();
        EnsureMetadataTable(connection);
        return GetSchemaVersion(connection, null);
    }

    public SchemaMigrationSummary GetLastMigrationSummary()
    {
        return new SchemaMigrationSummary
        {
            PreviousVersion = _lastMigrationSummary.PreviousVersion,
            CurrentVersion = _lastMigrationSummary.CurrentVersion,
            SafetyBackupPath = _lastMigrationSummary.SafetyBackupPath,
            AppliedMigrations = _lastMigrationSummary.AppliedMigrations
                .Select(migration => new SchemaMigrationInfo
                {
                    Version = migration.Version,
                    Description = migration.Description
                })
                .ToList()
        };
    }

    private void ApplySchemaMigrations(SqliteConnection connection)
    {
        EnsureMetadataTable(connection);
        var currentVersion = GetSchemaVersion(connection, null);
        var previousVersion = currentVersion;
        var applied = new List<SchemaMigrationInfo>();
        var safetyBackupPath = string.Empty;

        if (currentVersion < CurrentSchemaVersion && File.Exists(_databasePath) && new FileInfo(_databasePath).Length > 0)
        {
            safetyBackupPath = CreateAutomaticBackupSnapshot(connection, $"migration-v{currentVersion}-to-v{CurrentSchemaVersion}");
        }

        foreach (var migration in GetMigrations().Where(migration => migration.Version > currentVersion).OrderBy(migration => migration.Version))
        {
            using var transaction = connection.BeginTransaction();
            migration.Apply(connection, transaction);
            SetSchemaVersion(connection, transaction, migration.Version);
            transaction.Commit();

            applied.Add(new SchemaMigrationInfo
            {
                Version = migration.Version,
                Description = migration.Description
            });
            currentVersion = migration.Version;
        }

        _lastMigrationSummary = new SchemaMigrationSummary
        {
            PreviousVersion = previousVersion,
            CurrentVersion = currentVersion,
            SafetyBackupPath = safetyBackupPath,
            AppliedMigrations = applied
        };
    }

    private IEnumerable<DatabaseMigration> GetMigrations()
    {
        yield return new DatabaseMigration(
            1,
            "יצירת טבלאות הבסיס של המוצר",
            (connection, transaction) => CreateBaseSchema(connection, transaction));

        yield return new DatabaseMigration(
            2,
            "שדרוג סכמת הליבה, עמודות SM-2 ואינדקסים בסיסיים",
            (connection, transaction) => UpgradeSchema(connection, transaction));

        yield return new DatabaseMigration(
            3,
            "אינדקסים לביצועים, טבלת מטא-דאטה לתחזוקה, ונתוני גרסה",
            (connection, transaction) =>
            {
                ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StudyItems_SubjectId_DueDate ON StudyItems(SubjectId, DueDate);");
                ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StudyItems_ModifiedAt ON StudyItems(ModifiedAt);");
                ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_StudyItems_RootCategoryCache ON StudyItems(RootCategoryCache);");
                ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_ReviewHistory_ItemSuccessReviewedAt ON ReviewHistory(StudyItemId, WasSuccessful, ReviewedAt);");
                ExecuteNonQuery(connection, transaction, "CREATE INDEX IF NOT EXISTS IX_Subjects_SourceSystem_SourceKey ON Subjects(SourceSystem, SourceKey);");
                SetMetadataValue(connection, transaction, "ReleaseChannel", "stable-local");
            });
        yield return new DatabaseMigration(
            4,
            "שכבת מעקב לימוד לפי יחידה, היסטוריית חזרות ליחידות ואינדקסים תומכים",
            (connection, transaction) =>
            {
                UpgradeSchema(connection, transaction);
                SetMetadataValue(connection, transaction, "StudyUnitTracking", "enabled");
            });
    }

    private static void EnsureMetadataTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS AppMetadata (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private static int GetSchemaVersion(SqliteConnection connection, SqliteTransaction? transaction)
    {
        var value = GetMetadataValue(connection, transaction, SchemaVersionKey);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version)
            ? version
            : 0;
    }

    private static void SetSchemaVersion(SqliteConnection connection, SqliteTransaction transaction, int version)
    {
        SetMetadataValue(connection, transaction, SchemaVersionKey, version.ToString(CultureInfo.InvariantCulture));
    }

    private sealed record DatabaseMigration(
        int Version,
        string Description,
        Action<SqliteConnection, SqliteTransaction> Apply);
}
