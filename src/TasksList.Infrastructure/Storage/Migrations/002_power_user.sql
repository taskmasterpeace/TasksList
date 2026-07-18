CREATE TABLE IF NOT EXISTS note_presentations (
    note_id TEXT PRIMARY KEY REFERENCES notes(id) ON DELETE CASCADE,
    payload_json TEXT NOT NULL,
    hidden_at TEXT NULL,
    deleted_at TEXT NULL,
    wake_at TEXT NULL,
    reminder_at TEXT NULL,
    created_at TEXT NOT NULL,
    modified_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_note_presentations_deleted_at
    ON note_presentations(deleted_at);

CREATE INDEX IF NOT EXISTS idx_note_presentations_wake_at
    ON note_presentations(wake_at);

CREATE INDEX IF NOT EXISTS idx_note_presentations_reminder_at
    ON note_presentations(reminder_at);

CREATE TABLE IF NOT EXISTS named_styles (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL UNIQUE COLLATE NOCASE,
    payload_json TEXT NOT NULL,
    is_default INTEGER NOT NULL DEFAULT 0,
    modified_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS note_reminders (
    note_id TEXT PRIMARY KEY REFERENCES notes(id) ON DELETE CASCADE,
    due_at TEXT NOT NULL,
    attention INTEGER NOT NULL,
    acknowledged_at TEXT NULL
);
