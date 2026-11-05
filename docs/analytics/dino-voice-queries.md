# Dino: which voice read a book, and how a pack download went (Slice 267)

Dataset `dino_usage` (production; the TestFlight twin is `dino_usage_beta`, never mixed). Columns as in `worker/CLAUDE.md`: `blob1` name, `blob2` book (the 32-hex package id, or `own`), `blob3` language, `blob9` detail, `blob16` session, `double1` sampling weight (count with `SUM(double1)`), `double2` seq.

Names and details (the Worker accepts exactly these; `worker/events.ts`, `AudioPackUsage` in `src/DinoReader.Core/AudioPackUsage.cs`):

- `read_voice`: one per read, about a book. Detail `premade` (at least one clip played) or `device:no_pack`, `device:pack_failed` (a download of that pack failed in this launch), `device:text_mismatch` (the pack is there but its pieces do not line up with the text), `device:clip_missing` (lined up, but no piece had a clip). `blob3` is the language the book was read in.
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

## Pack downloads that fail, by kind and language

```sql
SELECT blob3 AS language, blob9 AS detail, SUM(double1) AS downloads
FROM dino_usage
WHERE blob1 = 'audio_pack_download' AND blob9 LIKE 'failed:%'
GROUP BY language, detail ORDER BY downloads DESC
```

A `started` without a later `done` or `failed` in the same session is a download that was killed (the app closed or was stopped), which the next launch resumes.
