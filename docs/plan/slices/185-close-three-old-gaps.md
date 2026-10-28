### Slice 185 — Confirm and close three old gaps (fix only what still fails)

*Planned.*

**Touches:** `src/DinoReader.App/SpokenLanguage.cs`, `src/ReadTheStupidText.Mobile/LanguagePage.xaml.cs`, `src/ReadTheStupidText.Mobile/ReviewPage.xaml.cs`, `.maestro/*`, `docs/plan/slices/185-close-three-old-gaps.md`.

#### Why

Three gaps sit in *Outstanding work* with no owner. A code check on 2026-10-28 (against `main`) found:

1. **Dino first run on the x64 emulator may crash** (Slice 162: Debug x86_64 SIGSEGV in Reflection.Emit via `SpokenLanguage.Use` when the voice pack starts; the `skip-first-run` hook leaves the pack start out, so no flow covers it). Still open; likely Debug-x64-only.
2. **RTST first-run "Continue does not move on"** (Slice 161): the code is a deliberate two-step (`LanguagePage.OnContinueClicked`, `LanguagePage.xaml.cs:94-110`: first tap starts the pack and relabels the button "Start reading", second tap goes on). Probably not a bug; confirm the relabel happens.
3. **A 36 MB text file crashed ReviewPage with OutOfMemoryError** (Slice 138): `EditorTextLimit.Cap` (300,000 characters) and the `Review_TextCut` banner now guard it (`ReviewPage.xaml.cs:163-164`). Probably fixed; confirm.

#### Work

- [ ] A Maestro flow (or hook action) for a real Dino first run with the voice pack start, on the x64 emulator, Debug and the AOT build. If it crashes, find the `Reflection.Emit` use under `SpokenLanguage.Use` and remove it (or move it off the first-run path), then keep the flow as a permanent check.
- [ ] Emulator: RTST language page: tap Continue, the button relabels to "Start reading" while the pack downloads, second tap opens the main page. Keep it as a flow. If the relabel doesn't happen, fix it; if it does, close the gap as by design.
- [ ] Emulator: open a 36 MB text file in RTST: no crash, the "text was cut" banner shows. Check every path that sets the editor text uses `EditorTextLimit.Cap`. Keep it as a flow.
- [ ] Record each result here and remove the three lines from *Outstanding work*.

#### Verification

The three flows above pass on the emulator (`run-flows.ps1 -App auto`); any fix gets a unit test where the logic allows.
