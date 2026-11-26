-- Slice 287: the Dino usage dashboard's permanent daily numbers (D1 database `dino-stats`, binding STATS).
-- Apply with: npx wrangler d1 execute dino-stats --remote --file worker/schema/stats.sql (docs/DEPLOYING.md).
-- Safe to run twice.

-- One row per UTC day x book x platform x metric. `book` is a store id (32 hex digits) or `own`;
-- `platform` is android, iphone, ipad, windows, sonos or alexa; `metric` is downloads, listens, seconds or
-- seconds_estimated. `value` is the sampling-weighted sum (SUM(double1)), so it can have a fraction.
CREATE TABLE IF NOT EXISTS daily_stats (
  day      TEXT NOT NULL,
  book     TEXT NOT NULL,
  platform TEXT NOT NULL,
  metric   TEXT NOT NULL,
  value    REAL NOT NULL,
  PRIMARY KEY (day, book, platform, metric)
) WITHOUT ROWID;

-- Single facts about the rollup, e.g. `last_run_at` (ISO time of the last run that wrote today).
CREATE TABLE IF NOT EXISTS meta (
  key   TEXT PRIMARY KEY,
  value TEXT NOT NULL
) WITHOUT ROWID;

-- One row per UTC day whose rollup was written successfully (the batch that replaces the day's daily_stats rows writes it too).
-- A day without a row is retried; rows are never deleted.
CREATE TABLE IF NOT EXISTS rollup_days (
  day       TEXT PRIMARY KEY,
  rolled_at TEXT NOT NULL
) WITHOUT ROWID;

-- Slice 457: a child's favourite books on the Alexa skill (worker/alexa-favourites.ts). One row per Alexa account x book; `user_id` is
-- Alexa's opaque account id and nothing else about the person is kept. Rows are read oldest first (rowid), which is the order they play in.
-- Apply BEFORE the Worker that reads it is deployed (the same wrangler d1 execute command as above).
CREATE TABLE IF NOT EXISTS alexa_favourites (
  user_id TEXT NOT NULL,
  book    TEXT NOT NULL,
  PRIMARY KEY (user_id, book)
);

-- Slice 457 decision 15: when the Alexa skill last told an account about the subscription books (worker/alexa-subscription-told.ts), so the
-- line is said at most once a week. `told_at` is epoch milliseconds. Apply BEFORE the Worker that reads it is deployed; while the table is
-- missing the skill says the line every time.
CREATE TABLE IF NOT EXISTS alexa_subscription_told (
  user_id TEXT PRIMARY KEY,
  told_at INTEGER NOT NULL
) WITHOUT ROWID;
