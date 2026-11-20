### Slice 421 — Dino: each app session also goes to Ninjalog as one trace, so journeys, event flow and funnels are queries
**Product:** Dino

*Planned 2026-11-20; building.*

**Touches:** `worker/ninjalog-spans.ts` (new), `worker/ninjalog-spans.test.mjs` (new), `worker/ninjalog.ts`, `worker/ninjalog.test.mjs`, `worker/CLAUDE.md`, `docs/analytics/ninjalog-queries.md`, `docs/analytics/ninjalog-dashboard.json`

## Why

On 2026-11-20 the owner asked for a behaviour-flow view (a Google Analytics "Behavior Flow" screenshot: sessions moving
from event to event, with drop-offs). Ninjalog (Slice 419) has no flow or funnel chart: its query reference
(`app.ninjalog.dev/docs`, read 2026-11-20) has tables and time charts only, and no command that links an event to the next
one. The Ninjalog author told the owner to send OpenTelemetry spans instead. `app.ninjalog.dev/docs/traces` (read
2026-11-20): Ninjalog takes OTLP/HTTP spans on `POST https://app.ninjalog.dev/v1/traces` (protobuf or JSON, the same
Bearer ingest token), one event per span in index `spans` unless the **resource** carries an `#index` attribute; every span
gets `trace_id`, `span_id`, `parent_span_id`, `root`, `span.name`, `duration_ms`, `start_ns`/`end_ns`; spans whose trace or
span id is missing or all zeros are refused. `spantree` adds `depth`, `span.path`, `parent.name`, `root.*`, `child_count`,
and Search draws one trace as a waterfall.

Dino sends no OpenTelemetry today (`git grep -i opentelemetry origin/main -- src` finds nothing in Dino; `AppTrace` writes
the phone's own log only). It does not need to: every Dino usage event already carries `session` (32 hex characters, one
per cold start) and `seq` (1, 2, 3 ... within the session), so the Worker can link each event to the one before it with no
app change and no stored state.

## Decisions

- Owner, 2026-11-20: "do it" (after being told the spans are the same events re-sent by the server in the linked format).
- Decided by this session: built in the Worker, no app change (the data is already in every event); same data, same
  processor, so the privacy page and `PRIVACY.md` only change if a sentence there becomes untrue (check, item 4).

## Work

- [ ] 1. **Spans from the counts the tee already gets.** In `worker/ninjalog-spans.ts`, for each forwarded production count
  whose `session` is 32 lowercase hex (not all zeros) and whose `seq` is a whole number ≥ 1, build one OTLP span:
  - `traceId` = the session; `spanId` = the seq as 16 hex characters, zero-padded (unique inside the trace, never zero);
    `parentSpanId` = seq − 1 the same way, left out for seq 1 (the session's root).
  - name: `<screen>` for a `screen` event, `tap <detail>` for a `tap`, else the event name (`read_started`,
    `book_finished`, ...).
  - start time: the batch's receive time minus (the largest `sinceLaunch` of that session in this batch − this event's
    `sinceLaunch`) seconds; end = the start of the session's next event in the same batch, else the same as the start.
    Events without `sinceLaunch` use the receive time.
  - kind internal, status unset; attributes: the same named fields the log document of Slice 419 carries
    (`ninjalogDocument`), flat, without `timestamp`.
  - resource attributes: `service.name` = `dino`, `#index` = `dino_spans`.
  Counts with no session (Sonos, Alexa) or no usable seq make no span.
- [ ] 2. **Send them.** The tee in `worker/ninjalog.ts` posts the spans as OTLP JSON to `https://app.ninjalog.dev/v1/traces`
  in the same deferred work as the log post, same token, same 3 s timeout, same never-throw rule; an empty span list sends
  nothing. Same switch (`NINJALOG_ENABLED` + `NINJALOG_TOKEN`); `dino_usage_beta` is still never forwarded. Check the OTLP
  JSON shape (`resourceSpans` → `scopeSpans` → `spans`, ids as hex strings, times as Unix-nanosecond strings) with
  context7 before writing it.
- [ ] 3. **Tests** in `worker/ninjalog-spans.test.mjs`: ids and parents (seq 1 root, seq 5 → parent 4), names, start/end
  from `sinceLaunch`, counts without a session or with seq −1 skipped, the resource `#index`, the post goes to
  `/v1/traces` with the token, off when the switch is off, a failing post never throws. All Worker tests stay green.
- [ ] 4. **Privacy text.** Read the Ninjalog lines in `site/dino/privacy.html` and `PRIVACY.md`; change them only if a
  sentence becomes untrue (they say the server passes on the same fields plus the receive time).
- [ ] 5. **Queries and dashboard.** Add to `docs/analytics/ninjalog-queries.md` (each run in Ninjalog Search once spans
  arrive, item 7):
  - event flow (step to step): `index=dino_spans | spantree | stats count by parent.name, span.name | sort -count`
  - where sessions end: `index=dino_spans | spantree | where child_count=0 | stats count by span.name | sort -count`
  - screen journeys: the screen spans only, by session (`stats values(span.name) by trace_id` ordered by seq).
  - one session: `trace_id=<session> | spantree | sort start_ns` (the waterfall).
  Add an "Event flow" and an "Where sessions end" table widget to `docs/analytics/ninjalog-dashboard.json`.
- [ ] 6. **Docs:** `worker/CLAUDE.md` gets one rule line: Dino counts also go to Ninjalog as spans (one trace per session,
  parent = the previous seq), link to this file.
- [ ] 7. **Live (after merge and deploy):** the first `index=dino_spans` spans show up; one session opens as a waterfall;
  the event-flow query returns rows; re-upload the dashboard JSON (Dashboards page file input) and check the two new
  widgets have rows.

## Known limits

- A lost or sampled-out event breaks the chain: its next event points at a span that never arrived, so `spantree` may
  start a new branch there. Counts are still right per event; paths after a gap are cut.
- Times are estimates from `sinceLaunch` (whole seconds) inside one batch, not the phone's clock.

## Verification

- `node --test worker/*.test.mjs` and `npm run typecheck` green.
- Item 7 read live in Ninjalog (Chrome, the owner's signed-in project).
