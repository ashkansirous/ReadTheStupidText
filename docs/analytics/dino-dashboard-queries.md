# Dino dashboard: the rollup SQL (Slice 287)

The hourly rollup (`worker/stats-rollup.ts`, `metricSql`) runs one Analytics Engine query per metric over exactly the run's UTC days (an `IN` list) and writes the rows into D1 `daily_stats(day, book, platform, metric, value)`. Dataset `dino_usage` only (production; `dino_usage_beta` is never read). Every value is `SUM(_sample_interval * double1 [* double5])`: `_sample_interval` is Analytics Engine's own downsampling (Cloudflare's docs count with `SUM(_sample_interval)`; `tools/dino/dino-usage-report.ps1` and `rtst-usage-report.ps1` combine it with the app's `double1` the same way), `double1` is the app's own sampling weight. Both are needed: AE may downsample a busy day, and the app samples too. Each run replaces today and yesterday (UTC), plus at most 10 of the oldest days in [today-89, today-2] that have no `rollup_days` row (a first backfill takes a few hourly runs; nothing older than today-89 is read or touched). Each day is replaced, not upserted: one D1 batch deletes the day's rows, inserts the fresh ones and marks the day in `rollup_days`, so a key AE stops returning goes too and data and mark land together. A day whose AE query fails, answers non-200 or malformed, or whose total of any metric is lower than D1's (AE only appends inside its retention) is logged and skipped, so it is retried next run. Columns as in `worker/CLAUDE.md`: `blob1` name, `blob2` book (32-hex store id or `own`), `blob7` platform, `blob9` detail, `blob12` form factor, `double5` played seconds (-1 for none).

The platform key: `blob7` `ios` becomes `ipad` when `blob12` is `tablet`, else `iphone` (phone, other, empty); `android`, `windows`, `sonos` and `alexa` pass through. Rows are timed by `timestamp` (the batch's send time), in UTC.

Shape, with `<metric filter>` and `<value>` from the table below (replace the dates):

```sql
SELECT formatDateTime(timestamp, '%Y-%m-%d') AS day, blob2 AS book, blob7 AS platform, blob12 AS form, <value> AS value
FROM dino_usage
WHERE <metric filter> AND timestamp >= toDateTime('2026-11-07 00:00:00') AND timestamp < toDateTime('2026-11-09 00:00:00')
GROUP BY day, book, platform, form
FORMAT JSON
```

| metric | filter | value |
| --- | --- | --- |
| `downloads` | `blob1 = 'store_download'` | `SUM(_sample_interval * double1)` |
| `listens` | `blob1 = 'book_listened'` | `SUM(_sample_interval * double1)` |
| `seconds` | `(blob1 IN ('read_paused', 'read_stopped') AND double5 >= 0) OR (blob1 = 'book_listened' AND blob9 = 'reported' AND double5 >= 0)` | `SUM(_sample_interval * double1 * double5)` |
| `seconds_estimated` | `blob1 = 'book_listened' AND blob9 = 'estimated' AND double5 >= 0` | `SUM(_sample_interval * double1 * double5)` |

No double counting in `seconds`: the app's `read_paused` and `read_stopped` rows each carry only the whole seconds heard since the previous paused/stopped row of the reading (`src/Dino/DinoReader.App/ListenTally.cs:113-116`, `TakeSeconds`, which advances `_reportedMilliseconds`; contract in `docs/plan/slices/288-dino-listens-and-listening-time.md:33-34`), not a running total, and the app's `book_listened` carries no seconds (only Sonos's `reported`/`estimated` rows do), so the sum over pause and stop rows is the played time.

Cross-check for the live verification: yesterday's `store_download` count per platform from this query (summing the `ios` rows over all forms) equals the dashboard's Book downloads for that day and platform, once the cron has run after midnight UTC. D1 side: `SELECT platform, SUM(value) FROM daily_stats WHERE day = '<day>' AND metric = 'downloads' GROUP BY platform`.
