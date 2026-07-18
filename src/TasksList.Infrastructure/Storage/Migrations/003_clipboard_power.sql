ALTER TABLE captures ADD COLUMN title TEXT NOT NULL DEFAULT '';
ALTER TABLE captures ADD COLUMN is_favorite INTEGER NOT NULL DEFAULT 0;
ALTER TABLE captures ADD COLUMN used_at TEXT NULL;
ALTER TABLE captures ADD COLUMN deleted_at TEXT NULL;
ALTER TABLE captures ADD COLUMN modified_at TEXT NULL;
ALTER TABLE captures ADD COLUMN size_bytes INTEGER NOT NULL DEFAULT 0;
ALTER TABLE captures ADD COLUMN source_url TEXT NULL;
ALTER TABLE captures ADD COLUMN duplicate_hash TEXT NOT NULL DEFAULT '';

UPDATE captures SET modified_at = captured_at WHERE modified_at IS NULL;

CREATE INDEX IF NOT EXISTS idx_captures_duplicate_hash
    ON captures(duplicate_hash) WHERE duplicate_hash <> '';
CREATE INDEX IF NOT EXISTS idx_captures_favorite_captured
    ON captures(is_favorite, captured_at DESC);
CREATE INDEX IF NOT EXISTS idx_captures_deleted_at
    ON captures(deleted_at);

CREATE TABLE IF NOT EXISTS capture_note_assignments (
    capture_id TEXT NOT NULL REFERENCES captures(id) ON DELETE CASCADE,
    note_id TEXT NOT NULL REFERENCES notes(id) ON DELETE CASCADE,
    assigned_at TEXT NOT NULL,
    PRIMARY KEY(capture_id, note_id)
);
