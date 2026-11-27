### Slice 432 — Dino: drop the unused PDF and Word libraries from the Android and iOS apps
**Product:** Dino

*Plan-cleanup check 2026-11-25 (origin/main 1a8bf725): verdict keep open, nothing built yet. Checked: both csproj ProjectReferences to Sirous.Reading.Documents are still there (DinoReader.App.csproj:198, Mobile.Shared.csproj:70); no Dino or Mobile.Shared source names a Documents type (only those two csproj files match); the three workflow path filters and ship-targets.mjs:107 still list Documents; Dino CLAUDE.md:23 still says it shares Documents; no PR for this work. All five work items are own work and no wait is inside the slice (item 3 uses the next release the merge cuts, a next step, not a wait).*

*Planned 2026-11-23.* *Built 2026-11-27 (night 2026-11-27-0154, wave 1), PR #1389, not merged. Items 1, 2, 4, 5 done; item 3 waits for the release IPA. Emulator checks for the Dino first run, open book, premade audio and record page showing words passed; RTST opening a PDF and a full record-and-save flow, and the TestFlight launch, are not yet verified.*

**Touches:** `src/Dino/DinoReader.App/DinoReader.App.csproj`, `src/Shared/Sirous.Reading.Mobile.Shared/Sirous.Reading.Mobile.Shared.csproj`, `src/Dino/DinoReader.App/CLAUDE.md`, `.github/scripts/ship-targets.mjs`, `.github/scripts/ship-targets.test.mjs`, `.github/workflows/dinoreader-android.yml`, `.github/workflows/dinoreader-ios.yml`, `.github/workflows/dinoreader.yml`, `docs/plan/slices/432-dino-drop-pdf-word-libraries.md`

**Why.** The owner saw Dino Reader at about 20 MB on Google Play and 84 MB on the App Store (2026-11-23) and asked which
packages cause it. Measured from the `dino-v1.136.5` release assets: the iOS app is 84.6 MB installed (55.7 MB
precompiled ReadyToRun code, 19.2 MB IL `.dll` files, 6.3 MB runtime frameworks); the APK is 20.6 MB compressed. The
largest libraries in the iOS app are **PdfPig** (`UglyToad.PdfPig.dll` 3.77 MB + `UglyToad.PdfPig.Fonts.dll` 0.92 MB) and
**DocumentFormat.OpenXml** (3.00 MB + `DocumentFormat.OpenXml.Framework.dll` 0.20 MB), each also compiled into the
ReadyToRun framework. Dino never uses them: they come only from the `Sirous.Reading.Documents` project, which
`DinoReader.App.csproj` (line 198) and `Sirous.Reading.Mobile.Shared.csproj` (line 70) reference, and no source file in
`src/Dino`, `src/Shared/Sirous.Reading.Mobile.Shared` or `tests/Dino` names a `Sirous.Reading.Documents` type (checked
2026-11-23 with `git grep` on `origin/main`). Slice 305 removed the speech engines and ML Kit from Dino but left this
reference, and never measured the IPA.

**Decisions (owner, 2026-11-23: "I just feel like doing 1. plan it. leave the rest"):**
- Only the PDF/Word libraries are removed. Full trimming (`TrimMode=full`), Native AOT, the Mono runtime and font
  subsetting are out of this slice and not planned.
- Android and iOS both lose them (same reference, same change). The Dino **Windows head is unchanged**: Mobile.Shared's
  Windows target references `Sirous.Reading.Infrastructure`, which needs Documents for the RTST tray (decided by this
  session).
- RTST keeps PDF and Word reading on every platform: `ReadTheStupidText.Mobile.csproj` (line 159) references Documents
  itself and must still ship both libraries.

**Work:**
- [x] 1. Remove the `Sirous.Reading.Documents` ProjectReference from `DinoReader.App.csproj`, and from
      `Sirous.Reading.Mobile.Shared.csproj` (Mobile.Shared uses no Documents type; Windows keeps it through
      Infrastructure). Build Dino Android, Dino iOS (`-p:IncludeIos=true` compile), Dino Windows x64, RTST Android and RTST
      iOS with 0 errors.
- [x] 2. (Built 2026-11-27: unsigned-keystore Release `-p:AndroidPackageFormat=apk` publish gives a debug-signed `-Signed.apk` of 18,319,747 bytes vs 20,838,787 before (-2,519,040); its `libassembly-store.so` names no PdfPig or OpenXml, while the RTST APK's store (42,775,428 bytes) still names both.) Android: publish the Dino Release APK and list its assemblies: no `UglyToad.PdfPig*`, `DocumentFormat.OpenXml*`;
      record the APK size before (20,838,787 bytes, `dino-v1.136.5`) and after. RTST APK still carries both libraries.
- [ ] 3. iOS: after merge, take the new release's `DinoReader-<v>.ipa` asset and list `Payload/`: no PdfPig or OpenXml
      `.dll`; record the installed (uncompressed `Payload/`) size before (84.6 MB) and after, and the
      `DinoReader.App.r2r` size before (55.65 MB) and after. Also note which of `System.Text.Encoding.CodePages.dll`,
      `System.Private.Xml.dll` and `System.IO.Packaging.dll` disappeared with them. RTST IPA still carries both libraries.
- [x] 4. (Built: new `documents` group in `ship-targets.mjs` -> windows + android + ios + dinowin; `dinowin` also lists Documents, so `dinoreader-windows.yml`, `dinoreader.yml` and `release-dino.yml` keep it; `actionlint` clean.) CI path filters: a `src/Shared/Sirous.Reading.Documents/**` change no longer touches Dino Android or iOS.
      Drop it from `dinoreader-android.yml` and `dinoreader-ios.yml` (keep it in `dinoreader-windows.yml`), check
      `dinoreader.yml`, and make `ship-targets.mjs` map Documents to RTST (all heads) plus Dino Windows only, with
      `ship-targets.test.mjs` updated to match. `actionlint` clean.
- [x] 5. `src/Dino/DinoReader.App/CLAUDE.md` line 23: Dino shares Domain/Application (not Documents) on Android and iOS;
      add the rule "Dino's phone apps never reference `Sirous.Reading.Documents`: it pulls PdfPig and OpenXml (about
      8 MB of IL, plus their precompiled iOS code) that Dino does not use" with a link to this slice.

**Verification:**
- Fast checks: `dotnet test` for `tests/Dino/DinoReader.Tests`, `tests/Shared/Shared.Tests`,
  `tests/Rtst/ReadTheStupidText.Tests`; `node --test .github/scripts/ship-targets.test.mjs`; `actionlint`.
- Android (emulator, scripted): Dino launches, shelf shows, a store book opens and reads a page aloud, record a page.
- iOS: the IPA listing in item 3, and the Dino TestFlight build launches and opens a book (`[phone]` line in
  *Not yet verified* if no run reaches it).
- RTST unchanged: its APK/IPA assembly lists still contain PdfPig and OpenXml; RTST Android opens a PDF file on the
  emulator.

**Built results (2026-11-27):**
- Builds, 0 errors each: Dino Android Release publish, Dino iOS compile, Dino Windows x64 publish, RTST Android publish, RTST iOS compile.
- Dino APK 20,838,787 -> 18,319,747 bytes (debug-signed Release, -2,519,040); no PdfPig or OpenXml in its assembly store. RTST APK 42,775,428 bytes still carries both.
- Tests: Dino 4888/4890 (0 fail), Shared 1736/1736, RTST 628/630 (0 fail); `ship-targets.test.mjs` 187/187; `actionlint` clean.
- Emulator (emulator-5554, cached Dino build 69442739e477): first-run, open-book, premade-audio-plays and record-page-shows-words all PASS; logcat has no TypeLoadException, FileNotFoundException or "Could not load" naming PdfPig, OpenXml or Documents.

*Changed while building:* item 4 added a `documents` group to `ship-targets.mjs` (windows + android + ios + dinowin); `dinowin` also lists Documents because Dino Windows reaches it through Infrastructure.

*From review:* both passes found no blocking findings; none applied.
- Skipped #1 (`reviews/pr-1389-high.md`): "lacks a space after the comma" - nit: no behaviour change; follow-up.
- Skipped #2 (`reviews/pr-1389-high.md`): "now groups Documents with `core`" - nit: no behaviour change; follow-up.
- Skipped #3 (`reviews/pr-1389-high.md`): "no test ties the two" - nit: no behaviour change; follow-up.
- Skipped #4 (`reviews/pr-1389-high.md`): "one wasted CI run per Documents-only PR" - nit: no behaviour change; follow-up.
- Skipped #5 (`reviews/pr-1389-high.md`): "records no outcome for the five builds" - nit: no behaviour change; follow-up (the outcomes are now in *Built results* above).
- Skipped #6 (`reviews/pr-1389-high.md`): "unused by XAML or code but advertises PDF and Word import" - pre-existing: unused Add_FileSub string, follow-up.
- Skipped #1 (`reviews/pr-1389-medium.md`): "no test pins that a Documents-only change selects the shared and rtst suites" - nit: no behaviour change; follow-up.
- Skipped #2 (`reviews/pr-1389-medium.md`): "header comment, nit: describes" - nit: no behaviour change; follow-up.

#### For CLAUDE.md
- Dino's phone apps never reference `Sirous.Reading.Documents`: it pulls PdfPig and OpenXml (about 8 MB of IL, plus their precompiled iOS code) that Dino does not use. Dino shares Domain/Application on Android and iOS; Windows reaches Documents only through Infrastructure. (Slice 432: docs/plan/slices/432-dino-drop-pdf-word-libraries.md) (already written in `src/Dino/DinoReader.App/CLAUDE.md` by the builder)

#### Not yet verified
- [cloud] Item 3: the IPA `Payload/` listing, sizes (`Payload/`, `DinoReader.App.r2r`) and which of the three System DLLs disappeared, from the next Dino release.
- [phone] The Dino TestFlight build launches and opens a book.
- [session] RTST Android opens a PDF on the emulator (no flow exists; a flow that pushes a small PDF is a follow-up).
- [session] A record-and-save flow (record-per-character) on the emulator.
