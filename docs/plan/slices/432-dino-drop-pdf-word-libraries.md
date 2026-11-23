### Slice 432 — Dino: drop the unused PDF and Word libraries from the Android and iOS apps
**Product:** Dino

*Planned 2026-11-23.*

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
- [ ] 1. Remove the `Sirous.Reading.Documents` ProjectReference from `DinoReader.App.csproj`, and from
      `Sirous.Reading.Mobile.Shared.csproj` (Mobile.Shared uses no Documents type; Windows keeps it through
      Infrastructure). Build Dino Android, Dino iOS (`-p:IncludeIos=true` compile), Dino Windows x64, RTST Android and RTST
      iOS with 0 errors.
- [ ] 2. Android: publish the Dino Release APK and list its assemblies: no `UglyToad.PdfPig*`, `DocumentFormat.OpenXml*`;
      record the APK size before (20,838,787 bytes, `dino-v1.136.5`) and after. RTST APK still carries both libraries.
- [ ] 3. iOS: after merge, take the new release's `DinoReader-<v>.ipa` asset and list `Payload/`: no PdfPig or OpenXml
      `.dll`; record the installed (uncompressed `Payload/`) size before (84.6 MB) and after, and the
      `DinoReader.App.r2r` size before (55.65 MB) and after. Also note which of `System.Text.Encoding.CodePages.dll`,
      `System.Private.Xml.dll` and `System.IO.Packaging.dll` disappeared with them. RTST IPA still carries both libraries.
- [ ] 4. CI path filters: a `src/Shared/Sirous.Reading.Documents/**` change no longer touches Dino Android or iOS.
      Drop it from `dinoreader-android.yml` and `dinoreader-ios.yml` (keep it in `dinoreader-windows.yml`), check
      `dinoreader.yml`, and make `ship-targets.mjs` map Documents to RTST (all heads) plus Dino Windows only, with
      `ship-targets.test.mjs` updated to match. `actionlint` clean.
- [ ] 5. `src/Dino/DinoReader.App/CLAUDE.md` line 23: Dino shares Domain/Application (not Documents) on Android and iOS;
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
