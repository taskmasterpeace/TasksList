PRAGMA journal_mode = WAL;
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS contexts (
    id TEXT PRIMARY KEY,
    kind INTEGER NOT NULL,
    provider TEXT NOT NULL,
    stable_identity TEXT NOT NULL,
    display_name TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS notes (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL,
    markdown TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS attachments (
    id TEXT PRIMARY KEY,
    note_id TEXT NOT NULL REFERENCES notes(id) ON DELETE CASCADE,
    context_id TEXT NOT NULL REFERENCES contexts(id),
    visibility INTEGER NOT NULL,
    UNIQUE(note_id, context_id)
);

CREATE TABLE IF NOT EXISTS places (
    id TEXT PRIMARY KEY,
    kind INTEGER NOT NULL,
    name TEXT NOT NULL,
    parent_id TEXT NULL REFERENCES places(id) ON DELETE CASCADE,
    stable_identity TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS captures (
    id TEXT PRIMARY KEY,
    kind INTEGER NOT NULL,
    source_context_id TEXT NOT NULL REFERENCES contexts(id),
    preview_text TEXT NOT NULL,
    captured_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS assignments (
    id TEXT PRIMARY KEY,
    capture_id TEXT NOT NULL REFERENCES captures(id) ON DELETE CASCADE,
    place_id TEXT NOT NULL REFERENCES places(id) ON DELETE CASCADE,
    actor INTEGER NOT NULL,
    filed_at TEXT NOT NULL,
    UNIQUE(capture_id, place_id)
);

CREATE TABLE IF NOT EXISTS capture_representations (
    capture_id TEXT NOT NULL REFERENCES captures(id) ON DELETE CASCADE,
    media_type TEXT NOT NULL,
    content TEXT NOT NULL,
    PRIMARY KEY(capture_id, media_type)
);

CREATE TABLE IF NOT EXISTS saved_tabs (
    id TEXT PRIMARY KEY,
    session_place_id TEXT NOT NULL REFERENCES places(id) ON DELETE CASCADE,
    url TEXT NOT NULL,
    title TEXT NOT NULL,
    window_index INTEGER NOT NULL,
    tab_index INTEGER NOT NULL
);

CREATE VIRTUAL TABLE IF NOT EXISTS captures_fts USING fts5(
    capture_id UNINDEXED,
    preview_text,
    tokenize = 'unicode61'
);
