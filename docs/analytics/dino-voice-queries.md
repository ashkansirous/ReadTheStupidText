# Dino: which voice read a book, and how a pack download went (Slice 267)

Dataset `dino_usage` (production; the TestFlight twin is `dino_usage_beta`, never mixed). Columns as in `worker/CLAUDE.md`: `blob1` name, `blob2` book (the 32-hex package id, or `own`), `blob3` language, `blob9` detail, `blob16` session, `double1` sampling weight (count with `SUM(double1)`), `double2` seq.

Names and details (the Worker accepts exactly these; `worker/events.ts`, `AudioPackUsage` in `src/DinoReader.Core/AudioPackUsage.cs`):

- `read_voice`: one per read, about a book. Detail `premade` (every spoken piece had a clip), `partial` (some pieces had a clip and the rest fell back to the device voice; Slice 272's fault read as `premade` before Slice 273), or `device:no_pack` (no pack published or installed for that book and language: an own book, or none yet; not a fault), `device:pack_failed` (a download of that pack failed in this launch), `device:text_mismatch` (the pack is there but its pieces do not line up with the text), `device:clip_missing` (lined up, but no piece had a clip). `blob3` is the language the book was read in. `double3` = pieces read by clips, `double4` = pieces read by the device voice (-1 on builds before Slice 273).
- `fallback` (Slice 273): a feature fell back to something else. Detail `<area>:<reason>`, e.g. `premade_audio:text_mismatch`, `premade_audio:clip_missing`, `premade_audio:pack_failed` (`no_pack` is never sent as a fallback for premade audio: no pack published is normal). A `partial` read's reason is whichever of `text_mismatch` or `clip_missing` caused more of its misses (a tie says `text_mismatch`). `double3` = how many times that (area, reason, book, language) happened in the session: the first is sent at once with 1, and if it happened again one more row with the session total is sent at each usage flush (book left, app stop or sleep, start-up) when the count grew, not only at session end, so a session can have several rows. So **a session's count is `MAX(double3)` per `blob16` + area + reason + book + language, never `SUM(double3)`**; `SUM(double1)` counts rows. `double4` is -1 for a fallback.

`double3` and `double4` mean different things per event name (`read_voice`: pieces by clips and by the device voice; `fallback`: times this session and none), so always filter on `blob1`.
- `audio_pack_download`: one per download step, about a store book. Detail `started`, `done`, `failed:network`, `failed:size`, `failed:unzip`, `failed:clip`, `failed:missing` (the store has no such pack version, a 404), `failed:storage` (disk full or no access). A cancelled download sends no closing count, so `started` minus `done` minus `failed:*` includes cancelled and in-flight ones. A pack that failed is tried at most twice per launch. `blob3` is the pack's language.

## Which voice did session X use

```sql
SELECT double2 AS seq, blob1 AS name, blob2 AS book, blob3 AS language, blob9 AS detail, timestamp
FROM dino_usage
WHERE blob16 = '<32 hex session id>' AND blob1 IN ('read_voice', 'audio_pack_download', 'read_started')
ORDER BY double2
```

Order by `seq`, never by timestamp (the timestamp is the batch's send time).

## Share of reads on the device voice, by reason, per day

```sql
SELECT blob8 AS day, blob9 AS detail, SUM(double1) AS reads
FROM dino_usage
WHERE blob1 = 'read_voice'
GROUP BY day, detail ORDER BY day, reads DESC
```

(`read_voice` exists from the build that ships Slice 267, so older days have no rows. `blob6` is the app version if a split by build is needed.)

## Fallbacks per session, by reason

```sql
SELECT blob3 AS language, blob9 AS detail, blob16 AS session, blob2 AS book, MAX(double3) AS times
FROM dino_usage
WHERE blob1 = 'fallback'
GROUP BY language, detail, session, book
```

Sum `times` over that result for occurrences, or count its rows for sessions affected.

## Pack downloads that fail, by kind and language

```sql
SELECT blob3 AS language, blob9 AS detail, SUM(double1) AS downloads
FROM dino_usage
WHERE blob1 = 'audio_pack_download' AND blob9 LIKE 'failed:%'
GROUP BY language, detail ORDER BY downloads DESC
```

A `started` without a later `done` or `failed` in the same session is a download that was killed (the app closed or was stopped), which the next launch resumes.
