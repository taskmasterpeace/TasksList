using System.Globalization;
using Microsoft.Data.Sqlite;
using TasksList.Core.Models;

namespace TasksList.Infrastructure.Storage;

public sealed class TasksListDatabase
{
    private readonly string _connectionString;

    public TasksListDatabase(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
            Pooling = false,
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        const string resourceName = "TasksList.Infrastructure.Storage.Migrations.001_initial.sql";
        await using var stream = typeof(TasksListDatabase).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded migration {resourceName}.");
        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveContextAsync(ContextRef context, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO contexts (id, kind, provider, stable_identity, display_name)
            VALUES ($id, $kind, $provider, $stableIdentity, $displayName)
            ON CONFLICT(id) DO UPDATE SET
                kind = excluded.kind,
                provider = excluded.provider,
                stable_identity = excluded.stable_identity,
                display_name = excluded.display_name;
            """;
        command.Parameters.AddWithValue("$id", context.Id.Value.ToString("D"));
        command.Parameters.AddWithValue("$kind", (int)context.Kind);
        command.Parameters.AddWithValue("$provider", context.Provider);
        command.Parameters.AddWithValue("$stableIdentity", context.StableIdentity);
        command.Parameters.AddWithValue("$displayName", context.DisplayName);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SavePlaceAsync(Place place, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO places (id, kind, name, parent_id, stable_identity)
            VALUES ($id, $kind, $name, $parentId, $stableIdentity)
            ON CONFLICT(id) DO UPDATE SET
                kind = excluded.kind,
                name = excluded.name,
                parent_id = excluded.parent_id,
                stable_identity = excluded.stable_identity;
            """;
        command.Parameters.AddWithValue("$id", place.Id.Value.ToString("D"));
        command.Parameters.AddWithValue("$kind", (int)place.Kind);
        command.Parameters.AddWithValue("$name", place.Name);
        command.Parameters.AddWithValue("$parentId", place.ParentId is { } parent
            ? parent.Value.ToString("D")
            : DBNull.Value);
        command.Parameters.AddWithValue("$stableIdentity", place.StableIdentity);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveNoteAsync(Note note, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO notes (id, title, markdown)
                VALUES ($id, $title, $markdown)
                ON CONFLICT(id) DO UPDATE SET title = excluded.title, markdown = excluded.markdown;
                """;
            command.Parameters.AddWithValue("$id", note.Id.Value.ToString("D"));
            command.Parameters.AddWithValue("$title", note.Title);
            command.Parameters.AddWithValue("$markdown", note.Markdown);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await DeleteOwnedRowsAsync(connection, transaction, "attachments", "note_id", note.Id.Value, cancellationToken);
        foreach (var attachment in note.Attachments)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO attachments (id, note_id, context_id, visibility)
                VALUES ($id, $noteId, $contextId, $visibility);
                """;
            command.Parameters.AddWithValue("$id", attachment.Id.Value.ToString("D"));
            command.Parameters.AddWithValue("$noteId", note.Id.Value.ToString("D"));
            command.Parameters.AddWithValue("$contextId", attachment.ContextId.Value.ToString("D"));
            command.Parameters.AddWithValue("$visibility", (int)attachment.Visibility);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Note?> GetNoteAsync(NoteId noteId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        string title;
        string markdown;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT title, markdown FROM notes WHERE id = $id;";
            command.Parameters.AddWithValue("$id", noteId.Value.ToString("D"));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            title = reader.GetString(0);
            markdown = reader.GetString(1);
        }

        var attachments = new List<Attachment>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT id, context_id, visibility
                FROM attachments
                WHERE note_id = $noteId
                ORDER BY rowid;
                """;
            command.Parameters.AddWithValue("$noteId", noteId.Value.ToString("D"));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                attachments.Add(new Attachment(
                    new AttachmentId(Guid.Parse(reader.GetString(0))),
                    new ContextId(Guid.Parse(reader.GetString(1))),
                    (AttachmentVisibility)reader.GetInt32(2)));
            }
        }

        return Note.Restore(noteId, title, markdown, attachments);
    }

    public async Task SaveCaptureAsync(Capture capture, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO captures (id, kind, source_context_id, preview_text, captured_at)
                VALUES ($id, $kind, $sourceContextId, $previewText, $capturedAt)
                ON CONFLICT(id) DO UPDATE SET
                    kind = excluded.kind,
                    source_context_id = excluded.source_context_id,
                    preview_text = excluded.preview_text,
                    captured_at = excluded.captured_at;
                """;
            command.Parameters.AddWithValue("$id", capture.Id.Value.ToString("D"));
            command.Parameters.AddWithValue("$kind", (int)capture.Kind);
            command.Parameters.AddWithValue("$sourceContextId", capture.SourceContextId.Value.ToString("D"));
            command.Parameters.AddWithValue("$previewText", capture.PreviewText);
            command.Parameters.AddWithValue("$capturedAt", capture.CapturedAt.ToString("O", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await DeleteOwnedRowsAsync(connection, transaction, "assignments", "capture_id", capture.Id.Value, cancellationToken);
        foreach (var assignment in capture.Assignments)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO assignments (id, capture_id, place_id, actor, filed_at)
                VALUES ($id, $captureId, $placeId, $actor, $filedAt);
                """;
            command.Parameters.AddWithValue("$id", assignment.Id.Value.ToString("D"));
            command.Parameters.AddWithValue("$captureId", capture.Id.Value.ToString("D"));
            command.Parameters.AddWithValue("$placeId", assignment.PlaceId.Value.ToString("D"));
            command.Parameters.AddWithValue("$actor", (int)assignment.Actor);
            command.Parameters.AddWithValue("$filedAt", assignment.FiledAt.ToString("O", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM captures_fts WHERE capture_id = $captureId;";
            command.Parameters.AddWithValue("$captureId", capture.Id.Value.ToString("D"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO captures_fts (capture_id, preview_text) VALUES ($captureId, $previewText);";
            command.Parameters.AddWithValue("$captureId", capture.Id.Value.ToString("D"));
            command.Parameters.AddWithValue("$previewText", capture.PreviewText);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Capture>> SearchCapturesAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var rows = new List<(CaptureId Id, CaptureKind Kind, ContextId Source, string Text, DateTimeOffset CapturedAt)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT c.id, c.kind, c.source_context_id, c.preview_text, c.captured_at
                FROM captures_fts f
                JOIN captures c ON c.id = f.capture_id
                WHERE captures_fts MATCH $query
                ORDER BY rank
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$query", query);
            command.Parameters.AddWithValue("$limit", limit);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add((
                    new CaptureId(Guid.Parse(reader.GetString(0))),
                    (CaptureKind)reader.GetInt32(1),
                    new ContextId(Guid.Parse(reader.GetString(2))),
                    reader.GetString(3),
                    DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture)));
            }
        }

        var captures = new List<Capture>();
        foreach (var row in rows)
        {
            var assignments = await LoadAssignmentsAsync(connection, row.Id, cancellationToken);
            captures.Add(Capture.Restore(row.Id, row.Kind, row.Source, row.Text, row.CapturedAt, assignments));
        }

        return captures;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task DeleteOwnedRowsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        string ownerColumn,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"DELETE FROM {table} WHERE {ownerColumn} = $ownerId;";
        command.Parameters.AddWithValue("$ownerId", ownerId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<Assignment>> LoadAssignmentsAsync(
        SqliteConnection connection,
        CaptureId captureId,
        CancellationToken cancellationToken)
    {
        var assignments = new List<Assignment>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, place_id, actor, filed_at
            FROM assignments
            WHERE capture_id = $captureId
            ORDER BY rowid;
            """;
        command.Parameters.AddWithValue("$captureId", captureId.Value.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            assignments.Add(new Assignment(
                new AssignmentId(Guid.Parse(reader.GetString(0))),
                new PlaceId(Guid.Parse(reader.GetString(1))),
                (AssignmentActor)reader.GetInt32(2),
                DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture)));
        }

        return assignments;
    }
}
