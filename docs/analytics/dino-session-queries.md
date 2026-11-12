# Dino: who and when on every row, one session in time order, the first-launch funnel (Slice 351)

Dataset `dino_usage` (production; the TestFlight twin is `dino_usage_beta`, never mixed). Columns as in `worker/CLAUDE.md`: `blob1` name, `blob2` book, `blob3` language, `blob4` source, `blob6` version, `blob7` platform, `blob9` detail, `blob10-blob15` country and device buckets, `blob16` session, `double1` sampling weight (count with `SUM(double1)`), `double2` seq.

New in Slice 351, on every count from the first build that ships it (older builds store `none` / -1; filter `blob18 <> 'none'` for rows that have them):

- `blob17` `uiLang`: the language of the app's screens (one of the 15 app languages; `blob3` is the language the book was read in, which can differ).
- `blob18` `installAge`: `first` (the first launch of this install), `d0` (a later launch the same day), `d1_7`, `d8_30`, `d31_plus`. Worked out on the device from a first-launch day that is never sent.
- `blob19` launch count: `1`, `2_5`, `6_20`, `21_plus`.
- `double6` `sinceLaunch`: whole seconds since this launch started (0 to 86400, -1 when none).
- `blob20` is the screen the count was made on (Slice 352, below).

The row timestamp is the batch's send time, so a session's time order is `double6`, then `double2` (seq), never `timestamp`.

## One session as a timeline

```sql
SELECT double6 AS seconds_since_launch, double2 AS seq, blob1 AS name, blob2 AS book, blob3 AS language, blob9 AS detail, blob17 AS ui_lang, blob18 AS install_age, blob19 AS launch_no
FROM dino_usage
WHERE blob16 = '<32 hex session>'
ORDER BY double6, double2
```

Weighted view across sessions, e.g. how long after launch the first book opens, by install age:

```sql
SELECT blob18 AS install_age, quantileWeighted(0.5)(double6, double1) AS median_seconds_to_first_open, SUM(double1) AS opens
FROM dino_usage
WHERE blob1 = 'book_opened' AND double2 <= 40 AND double6 >= 0 AND blob18 <> 'none'
GROUP BY install_age ORDER BY install_age
```

## First-launch funnel

Launches that were an install's first, then what those sessions did (count sessions, not rows: `uniq(blob16)` is the number of sessions; weight by `double1` when a rate below 1 is in force, because a session is sampled whole):

```sql
SELECT blob1 AS name, blob7 AS platform, SUM(double1) AS rows, uniq(blob16) AS sessions
FROM dino_usage
WHERE blob18 = 'first' AND blob1 IN ('app_opened', 'intro_started', 'intro_ended', 'store_download', 'book_opened', 'read_started', 'book_listened')
  AND timestamp > NOW() - INTERVAL '14' DAY
GROUP BY name, platform ORDER BY name, platform
```

Read it as: `app_opened` sessions with `installAge = first` are new installs that were opened; the next lines say how many got as far as downloading (`store_download`) and listening (`book_listened`). Compare `store_download` rows with `book_listened` rows by `blob19` (launch count) to see whether people who download a book come back to listen to it.

## Audio failures, by cause and book

Fallback reasons for pre-made audio are split by cause (Slice 351, see `dino-voice-queries.md`). A fallback now names its book, so the silent read can be tied to one:

```sql
SELECT blob2 AS book, blob3 AS language, blob9 AS detail, blob7 AS platform, blob6 AS version, MAX(double3) AS times
FROM dino_usage
WHERE blob1 = 'fallback' AND blob9 LIKE 'premade_audio:%' AND blob9 <> 'premade_audio:text_mismatch'
GROUP BY book, language, detail, platform, version, blob16
ORDER BY times DESC
```

## Screens, taps, settings and where a read started (Slice 352)

From the first build that ships Slice 352. `blob20` is the screen on every row (`none` for older builds). New event names (`blob1`), all with book `none`: `screen` (`blob9` = `<screen>:<action>`, actions `shown|next|back|skip|close`), `tap` (`<screen>:<control>`), `setting_changed` (`<setting>:<value>`), `app_backgrounded` and `app_resumed` (`blob9` = the screen). `book_opened` and `read_started` carry the origin in `blob9`: `wizard`, `shelf`, `corner` (`resume` and `next_book` are accepted but not sent yet). Screens: `intro_1`..`intro_4`, `first_run`, `first_books`, `shelf`, `reading`, `corner_books`, `corner_store`, `corner_settings`, `gate`, `story_languages`, `language`, `reading_speed`, `animal_sounds`, `record_intro`, `record`, `backup`, `usage_counts`, `about`, `report_problem`, `update`, `close_question`, `drop_language`.

Where launches stop in the wizard (sessions that reached each page, and what the last action on it was). The first query counts the sessions that showed each page:

```sql
SELECT blob9 AS screen_action, uniq(blob16) AS sessions, SUM(double1) AS rows
FROM dino_usage
WHERE blob1 = 'screen' AND blob9 LIKE 'intro_%:shown' AND timestamp > NOW() - INTERVAL '14' DAY
GROUP BY screen_action ORDER BY screen_action
```

How each wizard page ended (next, skip, close, back):

```sql
SELECT blob9 AS screen_action, uniq(blob16) AS sessions
FROM dino_usage
WHERE blob1 = 'screen' AND blob9 LIKE 'intro_%' AND blob9 NOT LIKE '%:shown' AND timestamp > NOW() - INTERVAL '14' DAY
GROUP BY screen_action ORDER BY screen_action
```

Where reads start (a listen, by origin; `book_listened` itself has no origin, so join on the session: sessions with a `read_started` of each origin, and sessions with a `book_listened`):

```sql
SELECT blob9 AS origin, uniq(blob16) AS sessions_that_started_a_read
FROM dino_usage
WHERE blob1 = 'read_started' AND blob9 <> '' AND timestamp > NOW() - INTERVAL '14' DAY
GROUP BY origin ORDER BY sessions_that_started_a_read DESC
```

A session's last screen before the app went to the background (a session that never came back is still sent, because the queue is flushed when the window stops):

```sql
SELECT blob16 AS session, blob9 AS last_screen, double6 AS seconds_since_launch
FROM dino_usage
WHERE blob1 = 'app_backgrounded' AND timestamp > NOW() - INTERVAL '2' DAY
ORDER BY double6
```

The timeline of one launch, with the screen and the button presses in time order: `tools/usage-sessions/usage-sessions.mjs session <32 hex>`; a day's summary (launches, where they stopped in the wizard, how many reached a read, how many listened 30 s): `tools/usage-sessions/usage-sessions.mjs day <yyyy-mm-dd>`. Both read the token from `D:\secrets` at run time and print nothing secret.
