using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TasksList.Core.Models;
using TasksList.Core.Notes;

namespace TasksList.Infrastructure.Storage;

public sealed class TasksListDatabase
{
    private readonly string _connectionString;
    private static readonly JsonSerializerOptions PresentationJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

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
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS schema_migrations (
                    version TEXT PRIMARY KEY,
                    applied_at TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var resourcePrefix = "TasksList.Infrastructure.Storage.Migrations.";
        var migrations = typeof(TasksListDatabase).Assembly
            .GetManifestResourceNames()
            .Where(name => name.StartsWith(resourcePrefix, StringComparison.Ordinal) &&
                           name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        foreach (var resourceName in migrations)
        {
            var version = resourceName[resourcePrefix.Length..^4];
            if (await MigrationAppliedAsync(connection, version, cancellationToken))
            {
                continue;
            }

            await using var stream = typeof(TasksListDatabase).Assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Missing embedded migration {resourceName}.");
            using var reader = new StreamReader(stream);
            var sql = await reader.ReadToEndAsync(cancellationToken);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT INTO schema_migrations (version, applied_at)
                    VALUES ($version, $appliedAt);
                    """;
                command.Parameters.AddWithValue("$version", version);
                command.Parameters.AddWithValue(
                    "$appliedAt",
                    DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    public async Task SaveNotePresentationAsync(
        NotePresentation presentation,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO note_presentations (
                note_id,
                payload_json,
                hidden_at,
                deleted_at,
                wake_at,
                reminder_at,
                created_at,
                modified_at)
            VALUES (
                $noteId,
                $payloadJson,
                $hiddenAt,
                $deletedAt,
                $wakeAt,
                $reminderAt,
                $createdAt,
                $modifiedAt)
            ON CONFLICT(note_id) DO UPDATE SET
                payload_json = excluded.payload_json,
                hidden_at = excluded.hidden_at,
                deleted_at = excluded.deleted_at,
                wake_at = excluded.wake_at,
                reminder_at = excluded.reminder_at,
                created_at = excluded.created_at,
                modified_at = excluded.modified_at;
            """;
        command.Parameters.AddWithValue("$noteId", presentation.NoteId.Value.ToString("D"));
        command.Parameters.AddWithValue(
            "$payloadJson",
            JsonSerializer.Serialize(presentation, PresentationJsonOptions));
        command.Parameters.AddWithValue("$hiddenAt", DbTimestamp(presentation.HiddenAt));
        command.Parameters.AddWithValue("$deletedAt", DbTimestamp(presentation.DeletedAt));
        command.Parameters.AddWithValue("$wakeAt", DbTimestamp(presentation.WakeAt));
        command.Parameters.AddWithValue("$reminderAt", DbTimestamp(presentation.ReminderAt));
        command.Parameters.AddWithValue("$createdAt", DbTimestamp(presentation.CreatedAt));
        command.Parameters.AddWithValue("$modifiedAt", DbTimestamp(presentation.ModifiedAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<NotePresentation> GetNotePresentationAsync(
        NoteId noteId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT payload_json
            FROM note_presentations
            WHERE note_id = $noteId;
            """;
        command.Parameters.AddWithValue("$noteId", noteId.Value.ToString("D"));
        var payload = await command.ExecuteScalarAsync(cancellationToken) as string;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return NotePresentation.Default(noteId);
        }

        try
        {
            var presentation = JsonSerializer.Deserialize<NotePresentation>(
                payload,
                PresentationJsonOptions);
            return presentation is not null && presentation.NoteId == noteId
                ? presentation
                : NotePresentation.Default(noteId);
        }
        catch (JsonException)
        {
            return NotePresentation.Default(noteId);
        }
    }

    public async Task SaveNamedStyleAsync(
        NamedNoteStyle style,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        if (style.IsDefault)
        {
            await using var clearDefault = connection.CreateCommand();
            clearDefault.Transaction = transaction;
            clearDefault.CommandText = "UPDATE named_styles SET is_default = 0 WHERE is_default <> 0;";
            await clearDefault.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO named_styles (id, name, payload_json, is_default, modified_at)
            VALUES ($id, $name, $payloadJson, $isDefault, $modifiedAt)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                payload_json = excluded.payload_json,
                is_default = excluded.is_default,
                modified_at = excluded.modified_at;
            """;
        command.Parameters.AddWithValue("$id", style.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", style.Name.Trim());
        command.Parameters.AddWithValue(
            "$payloadJson",
            JsonSerializer.Serialize(style.Style, PresentationJsonOptions));
        command.Parameters.AddWithValue("$isDefault", style.IsDefault ? 1 : 0);
        command.Parameters.AddWithValue("$modifiedAt", DbTimestamp(style.ModifiedAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NamedNoteStyle>> ListNamedStylesAsync(
        CancellationToken cancellationToken = default)
    {
        var styles = new List<NamedNoteStyle>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, payload_json, is_default, modified_at
            FROM named_styles
            ORDER BY name COLLATE NOCASE, id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            NoteStyle? style;
            try
            {
                style = JsonSerializer.Deserialize<NoteStyle>(
                    reader.GetString(2),
                    PresentationJsonOptions);
            }
            catch (JsonException)
            {
                style = null;
            }

            if (style is null)
            {
                continue;
            }

            styles.Add(new NamedNoteStyle(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                style,
                reader.GetInt32(3) != 0,
                DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture)));
        }

        return styles;
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

    public async Task<ContextRef?> GetContextAsync(ContextId contextId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT kind, provider, stable_identity, display_name
            FROM contexts
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", contextId.Value.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ContextRef(
            contextId,
            (ContextKind)reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3));
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

    public async Task<IReadOnlyList<Place>> ListPlacesAsync(CancellationToken cancellationToken = default)
    {
        var places = new List<Place>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, kind, name, parent_id, stable_identity
            FROM places
            ORDER BY name COLLATE NOCASE, rowid;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            places.Add(new Place(
                new PlaceId(Guid.Parse(reader.GetString(0))),
                (PlaceKind)reader.GetInt32(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : new PlaceId(Guid.Parse(reader.GetString(3))),
                reader.GetString(4)));
        }

        return places;
    }

    public async Task SaveBrowserSessionAsync(
        Place sessionPlace,
        IReadOnlyCollection<SavedTab> tabs,
        CancellationToken cancellationToken = default)
    {
        if (sessionPlace.Kind != PlaceKind.BrowserSession)
        {
            throw new ArgumentException("The Place must be a browser session.", nameof(sessionPlace));
        }

        await SavePlaceAsync(sessionPlace, cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await DeleteOwnedRowsAsync(connection, transaction, "saved_tabs", "session_place_id", sessionPlace.Id.Value, cancellationToken);
        foreach (var tab in tabs.OrderBy(tab => tab.WindowIndex).ThenBy(tab => tab.TabIndex))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO saved_tabs (id, session_place_id, url, title, window_index, tab_index)
                VALUES ($id, $sessionPlaceId, $url, $title, $windowIndex, $tabIndex);
                """;
            command.Parameters.AddWithValue("$id", tab.Id.Value.ToString("D"));
            command.Parameters.AddWithValue("$sessionPlaceId", tab.SessionPlaceId.Value.ToString("D"));
            command.Parameters.AddWithValue("$url", tab.Url);
            command.Parameters.AddWithValue("$title", tab.Title);
            command.Parameters.AddWithValue("$windowIndex", tab.WindowIndex);
            command.Parameters.AddWithValue("$tabIndex", tab.TabIndex);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SavedTab>> ListSavedTabsAsync(
        PlaceId sessionPlaceId,
        CancellationToken cancellationToken = default)
    {
        var tabs = new List<SavedTab>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, url, title, window_index, tab_index
            FROM saved_tabs
            WHERE session_place_id = $sessionPlaceId
            ORDER BY window_index, tab_index, rowid;
            """;
        command.Parameters.AddWithValue("$sessionPlaceId", sessionPlaceId.Value.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tabs.Add(new SavedTab(
                new SavedTabId(Guid.Parse(reader.GetString(0))),
                sessionPlaceId,
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4)));
        }

        return tabs;
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

    public async Task<IReadOnlyList<Note>> ListNotesAsync(CancellationToken cancellationToken = default)
    {
        var ids = new List<NoteId>();
        await using (var connection = await OpenAsync(cancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id FROM notes ORDER BY title COLLATE NOCASE, id;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(new NoteId(Guid.Parse(reader.GetString(0))));
            }
        }

        var notes = new List<Note>(ids.Count);
        foreach (var id in ids)
        {
            if (await GetNoteAsync(id, cancellationToken) is { } note)
            {
                notes.Add(note);
            }
        }

        return notes;
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

        await DeleteOwnedRowsAsync(connection, transaction, "capture_representations", "capture_id", capture.Id.Value, cancellationToken);
        foreach (var representation in capture.TextRepresentations)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO capture_representations (capture_id, media_type, content)
                VALUES ($captureId, $mediaType, $content);
                """;
            command.Parameters.AddWithValue("$captureId", capture.Id.Value.ToString("D"));
            command.Parameters.AddWithValue("$mediaType", representation.Key);
            command.Parameters.AddWithValue("$content", representation.Value);
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
            var representations = await LoadRepresentationsAsync(connection, row.Id, cancellationToken);
            captures.Add(Capture.Restore(row.Id, row.Kind, row.Source, row.Text, row.CapturedAt, assignments, representations));
        }

        return captures;
    }

    public async Task<IReadOnlyList<Capture>> ListCapturesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var rows = new List<(CaptureId Id, CaptureKind Kind, ContextId Source, string Text, DateTimeOffset CapturedAt)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT id, kind, source_context_id, preview_text, captured_at
                FROM captures
                ORDER BY captured_at DESC, rowid DESC;
                """;
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

        var captures = new List<Capture>(rows.Count);
        foreach (var row in rows)
        {
            var assignments = await LoadAssignmentsAsync(connection, row.Id, cancellationToken);
            var representations = await LoadRepresentationsAsync(connection, row.Id, cancellationToken);
            captures.Add(Capture.Restore(row.Id, row.Kind, row.Source, row.Text, row.CapturedAt, assignments, representations));
        }

        return captures;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<bool> MigrationAppliedAsync(
        SqliteConnection connection,
        string version,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM schema_migrations WHERE version = $version LIMIT 1;";
        command.Parameters.AddWithValue("$version", version);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static object DbTimestamp(DateTimeOffset? value) =>
        value is { } timestamp
            ? timestamp.ToString("O", CultureInfo.InvariantCulture)
            : DBNull.Value;

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

    private static async Task<IReadOnlyDictionary<string, string>> LoadRepresentationsAsync(
        SqliteConnection connection,
        CaptureId captureId,
        CancellationToken cancellationToken)
    {
        var representations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT media_type, content
            FROM capture_representations
            WHERE capture_id = $captureId;
            """;
        command.Parameters.AddWithValue("$captureId", captureId.Value.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            representations[reader.GetString(0)] = reader.GetString(1);
        }

        return representations;
    }
}
