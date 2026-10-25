# 2026-10-25 — Architecture check: Read The Stupid Text vs Dino Reader

The user asked how much code the two products share, whether one repo was right, and whether a change
to one product ships the other. Measured on `main` at 449a37b.

## Sharing (non-blank C#/XAML lines, tests excluded)

- Dino only: DinoReader.App 15.3k, DinoReader.Core 8.0k (about 50% of app code).
- RTST only: ReadTheStupidText.App 3.4k, Mobile 5.1k (about 17%).
- Shared: Domain 0.5k, Application 7.7k, Documents 0.3k, Infrastructure 4.3k, Mobile.Shared 7.8k
  (about 33%). About 75-80% of it is used by both; RTST-only parts are the tray code (Infrastructure
  Input/Startup, Application Input/Startup/Activity); Dino-only parts are about 3% (voice flavours).
- Verdict: about two-thirds of the code differs, but the shared third is the engine (speech readers,
  voice packs, chunking/timing, OCR, translation). One repo stays (Decision 133).

## Shipping coupling found

- A Dino-only change ships no RTST app, but bumps the shared `v<x.y.z>`, cuts a release that
  carries forward RTST files (`release.yml` 283-315) and runs RTST tests (`release.yml` 163-167).
  Dino Windows is unsigned and always builds, so every Dino change cuts a release. (Slices 128, 130)
- Tray-only Infrastructure edits rebuild Dino Windows (`ship-targets.mjs` Infrastructure group). (Slice 128)
- Release audit, last 40 runs: RTST was submitted to the Microsoft Store 4 times (cb96b65, dbc83f4,
  7846b1c, 43e2ef1), every time from Dino work editing shared engine files (`NeuralSentenceSource`,
  `SherpaEngineFactory`, `SupertonicSpeechReader`); 2 more runs (7ae749c, b360349) uploaded RTST to
  Play/TestFlight from Dino edits in Application/Domain (`PitchShifter`, `VoiceFlavour`). The rule
  shipped correctly (the binary changed); the leak is Dino features inside the shared engine. (Slice 129)
- 43e2ef1 failed in `testflight-submit-dino`: release flagged signed Dino iOS with no `.ipa`. (Slice 127)
- `store-submit.yml` falls back to the RTST Store id when `product_id` is empty. (Slice 127)
- Mobile.Shared's namespace equals the RTST phone app's (`ReadTheStupidText.Mobile`). (Slice 131)
- Harmless: a `site/dino/` change redeploys the whole Worker (idempotent).
- Correct: platform code sits under `Platforms/`; Dino's Android UI change ships only Dino; no RTST
  Mobile code compiles into Dino.
