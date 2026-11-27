-- Slice 438: the Sonos homes linked through the Dino app (worker/sonos-link.ts). D1 database `dino-stats`, binding STATS.
-- Apply BY HAND, once, BEFORE the Worker that reads it is deployed (docs/DEPLOYING.md, "The Sonos home link"):
--   npx wrangler d1 execute dino-stats --remote --file worker/schema/sonos-homes.sql
-- Safe to run twice. Nothing here is about a child or a listener: a hashed home id, the story language the grown-up chose, hashes of the
-- tokens Sonos holds, the hash of the subscription proof key and the proof itself (Slice 439) and two times.

-- One row per linked Sonos home. `home_hash` is SHA-256 of the household id Sonos sends (the id itself is never stored); `token_hash` and
-- `key_hash` are SHA-256 of the authToken and privateKey given to Sonos; `proof_hash` is SHA-256 of the subscription proof key the app sent
-- (null for a home linked without a subscription); `proof` is the subscription proof itself as the app sent it (`<store>:<productId>:<token>`, Slice 439: the Worker asks the store with it
-- when a paid book is browsed or played; plain, reachable only by the Worker, and never sent to Sonos, a speaker or the app); `created_at`, `last_seen` are epoch milliseconds. A home idle for 400 days is deleted by the next link.
CREATE TABLE IF NOT EXISTS sonos_homes (
  home_hash      TEXT PRIMARY KEY,
  story_language TEXT NOT NULL,
  token_hash     TEXT NOT NULL,
  key_hash       TEXT NOT NULL,
  proof_hash     TEXT,
  proof          TEXT,
  created_at     INTEGER NOT NULL,
  last_seen      INTEGER NOT NULL
) WITHOUT ROWID;

-- One subscription links one home: a new home with the same proof replaces the old (Slice 415 decision 5).
CREATE INDEX IF NOT EXISTS sonos_homes_proof ON sonos_homes (proof_hash);

-- Slice 439: the paid-book media route finds the home by the Sonos auth token the player sends (a hash lookup, not the primary key). Optional: without it the lookup scans the table.
CREATE INDEX IF NOT EXISTS sonos_homes_token ON sonos_homes (token_hash);

-- A link that Sonos has asked for and the Dino app has not finished: one row per link code (hashed), kept for an hour and deleted when used.
-- `callback` is the Sonos URL the app opens afterwards; `story_language` stays null until the app confirms.
CREATE TABLE IF NOT EXISTS sonos_pending_links (
  code_hash      TEXT PRIMARY KEY,
  home_hash      TEXT NOT NULL,
  callback       TEXT NOT NULL,
  story_language TEXT,
  proof_hash     TEXT,
  proof          TEXT,
  expires_at     INTEGER NOT NULL
) WITHOUT ROWID;
