using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace TrackerApp;

public sealed partial class AppDatabase
{
    private const string PausedReviewSessionKind = "PausedReview";

    public void SavePausedReviewSession(PausedReviewSessionState state)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM SavedReviewSessions WHERE SessionKind = $kind;";
            deleteCommand.Parameters.AddWithValue("$kind", PausedReviewSessionKind);
            deleteCommand.ExecuteNonQuery();
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO SavedReviewSessions (SessionKind, Payload, UpdatedAt)
            VALUES ($kind, $payload, $updatedAt);
            """;
        command.Parameters.AddWithValue("$kind", PausedReviewSessionKind);
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(state));
        command.Parameters.AddWithValue("$updatedAt", FormatDate(state.UpdatedAt));
        command.ExecuteNonQuery();

        transaction.Commit();
    }

    public PausedReviewSessionState? GetPausedReviewSession()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Payload
            FROM SavedReviewSessions
            WHERE SessionKind = $kind
            ORDER BY UpdatedAt DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$kind", PausedReviewSessionKind);

        var payload = command.ExecuteScalar() as string;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        return JsonSerializer.Deserialize<PausedReviewSessionState>(payload);
    }

    public bool HasPausedReviewSession()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM SavedReviewSessions WHERE SessionKind = $kind);";
        command.Parameters.AddWithValue("$kind", PausedReviewSessionKind);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    public void ClearPausedReviewSession()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM SavedReviewSessions WHERE SessionKind = $kind;";
        command.Parameters.AddWithValue("$kind", PausedReviewSessionKind);
        command.ExecuteNonQuery();
    }
}
