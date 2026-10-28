# Slice 177 — Dino: slow speech for the first reads after the app starts

*Planned.* Batch 38 (test gaps found 2026-10-28), Dino Reader (Android and iOS). After Slice 175 (it needs the
`ENGINE_READY` and `CHUNK_SYNTH` markers).

**Touches:** `src/DinoReader.App/**` (its reading/speech start-up), `src/ReadTheStupidText.Mobile.Shared/**` (the
shared neural readers and pack start-up, if the fix is there), `tools/phone/**` (a `-Release` run mode),
`docs/plan/slices/177-dino-cold-start-synthesis.md`

## Why

On the user's Pixel 10 Pro (2026-10-28, Debug build), the first Dino reads after a fresh install are slow on every
chunk, not only the first: at 2x, first audio 5.0-5.9 s and chunk 2 ready after about 5.4 s, against 0.9-1.2 s
first audio and 40-60 ms for chunk 2 once warm. A 4.9 s silence between chunks was flagged in one cold run. RTST on
the same phone starts in about 0.25 s. A child pressing Play waits five seconds, then hears a pause mid-sentence.

Slice 17 warmed the Windows neural engine at start-up; check whether the phone readers do the same.
The cause is unknown: it may be the Debug build (no AOT, interpreter), the model being loaded per chunk, the engine
warming its threads, or the pack still unpacking after `PACK_READY`.

## Work

- [ ] **Measure first, change nothing:** `audio-timing.ps1 -Release` builds a Release (AOT) Dino with the `.dbg`
  package id, so timing matches what users get, with the Slice 175 markers. Three cold runs and three warm runs at
  1x and 2x. If Release is fast cold, the slice ends here with the numbers recorded (Debug-only, no user impact).
- [ ] If Release is slow cold: find the phase from the markers (engine creation, first synthesis, every synthesis)
  and fix it by construction, e.g. create the engine once when the pack is ready and keep it, and synthesise the
  next chunk while the current one plays. Android and iOS share the reader, so both get the fix.
- [ ] Set the `audio-timing.ps1` cold-start limit from the fixed numbers so a regression is flagged.

## Verification

- Phone, Release build: cold first audio and chunk gaps within about 1.5x the warm numbers, at 1x and 2x, three
  runs each; the numbers recorded here.
- iOS: compiles; timing can't be run (no Mac), so it waits on TestFlight with the user's ear.
</content>
</invoke>
