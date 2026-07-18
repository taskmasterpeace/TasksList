CREATE TABLE IF NOT EXISTS interactive_timer_states (
    note_id TEXT NOT NULL REFERENCES notes(id) ON DELETE CASCADE,
    block_index INTEGER NOT NULL,
    duration_seconds INTEGER NOT NULL,
    remaining_seconds INTEGER NOT NULL,
    is_running INTEGER NOT NULL,
    ends_at TEXT NULL,
    modified_at TEXT NOT NULL,
    PRIMARY KEY(note_id, block_index)
);
