### Slice 185 — Confirm and close three old gaps (fix only what still fails)

*Built 2026-10-29 (PR #545). No app code changed; all three gaps no longer fail on the x64 Debug emulator. Dino first run not yet checked on an AOT Release x64 build.*

**Touches:** `src/DinoReader.App/SpokenLanguage.cs`, `src/ReadTheStupidText.Mobile/LanguagePage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReviewPage.xaml.cs`, `.maestro/*`, `docs/plan/slices/185-close-three-old-gaps.md`.

#### Why

Three gaps sit in *Outstanding work* with no owner. A code check on 2026-10-28 (against `main`) found:

1. **Dino first run on the x64 emulator may crash** (Slice 162: Debug x86_64 SIGSEGV in Reflection.Emit via `SpokenLanguage.Use` when the voice pack starts; the `skip-first-run` hook leaves the pack start out, so no flow covers it). Still open; likely Debug-x64-only.
2. **RTST first-run "Continue does not move on"** (Slice 161): the code is a deliberate two-step (`LanguagePage.OnContinueClicked`, `LanguagePage.xaml.cs:94-110`: first tap starts the pack and relabels the button "Start reading", second tap goes on). Probably not a bug; confirm the relabel happens.
3. **A 36 MB text file crashed ReviewPage with OutOfMemoryError** (Slice 138): `EditorTextLimit.Cap` (300,000 characters) and the `Review_TextCut` banner now guard it (`ReviewPage.xaml.cs:163-164`). Probably fixed; confirm.

#### Work

- [x] (Debug only; AOT Release not run) A Maestro flow (or hook action) for a real Dino first run with the voice pack start, on the x64 emulator, Debug and the AOT build. If it crashes, find the `Reflection.Emit` use under `SpokenLanguage.Use` and remove it (or move it off the first-run path), then keep the flow as a permanent check.
- [x] Emulator: RTST language page: tap Continue, the button relabels to "Start reading" while the pack downloads, second tap opens the main page. Keep it as a flow. If the relabel doesn't happen, fix it; if it does, close the gap as by design.
- [x] Emulator: open a 36 MB text file in RTST: no crash, the "text was cut" banner shows. Check every path that sets the editor text uses `EditorTextLimit.Cap`. Keep it as a flow.
- [x] Record each result here. (Removing the three lines from *Outstanding work* is left to the index pass.)

#### Verification

The three flows above pass on the emulator (`run-flows.ps1 -App auto`); any fix gets a unit test where the logic allows.

#### Results (2026-10-28, x64 emulator, Debug, headless)

1. **Dino first run: no crash.** New flow `.maestro/dino/first-run-pack-start.yaml` (clearState, Continue starts the pack through `SpokenLanguage.Use`, download panel shows) passed; the app was still alive 15 s later and logcat had no SIGSEGV. The Slice 162 crash does not reproduce on the current build. The AOT (Release) x64 build was not run. No code change.
2. **RTST Continue: by design.** New flow `.maestro/rtst/language-continue-two-step.yaml` passed: first tap relabels the button "Start reading", second tap opens the reader. Gap closed.
3. **36 MB text file: already fixed.** New flow `.maestro/rtst/needs-file/open-huge-text-file.yaml` (file picker, Downloads, `big36.txt`) passed: no crash, review page opens with the "only the first 300,000 characters" note. Every editor-text path in `ReviewPage` (`ApplyRequest`, paste) goes through `EditorTextLimit.Cap`. The flow needs the file pushed first (`adb push` to `/sdcard/Download/big36.txt`). It lives in `.maestro/rtst/needs-file/` (tag `needs-file`), so default `run-flows.ps1` runs skip it.

Not yet verified: the Dino first-run pack start on the AOT Release x64 build (only Debug was run).

*Changed while building:* the 36 MB flow is excluded from default runs by its `needs-file/` subfolder; run it by hand with `maestro test` after the `adb push`. The Dino flow uses a 12 s real wait and asserts `FirstRun(Download|Ready)`.

*From review:* two passes (`reviews/185-review1.md`, `reviews/185-review2.md`). Skipped as test style: text-based taps, English picker labels, the Android-only picker flow.

Still owed (outside Touches): the index pass removes the three lines these gaps covered from *Outstanding work* in `plan.md`.

#### For CLAUDE.md

- Maestro flows that need a pushed file live under `.maestro/<app>/needs-file/`, so `run-flows.ps1` skips them; run them by hand after `adb push`. Goes in `tools/CLAUDE.md`. (Slice 185: docs/plan/slices/185-close-three-old-gaps.md)

#### Not yet verified

- Dino first-run pack start on the AOT Release x64 build (only Debug was run).
