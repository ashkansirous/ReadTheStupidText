### Slice 166 — Usage counts: country, OS version, form factor and hardware buckets (both apps)

*Built 2026-10-27.* Not yet verified: the Windows heads were only compiled (no desktop run of the RTST tray app or Dino's Windows head); iOS compiled only (no simulator); first live rows with the new fields to be checked in the Analytics Engine studio after deploy.

**Touches:** `worker/events.ts`, `worker/events.test.mjs`, `worker/index.ts`, `src/DinoReader.Core/UsageEvent.cs`, `src/DinoReader.App/UsageCounter.cs`, `src/ReadTheStupidText.Mobile.Shared/Usage/*`, `src/ReadTheStupidText.Application/Usage/*`, `src/ReadTheStupidText.Infrastructure/Usage/*`, `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `tests/DinoReader.Tests/UsageEventTests.cs`, `tests/ReadTheStupidText.Tests/UsageTests.cs`, `tests/ReadTheStupidText.Tests/UsageServiceTests.cs`, `tools/dino-usage-report.ps1`, `tools/rtst-usage-report.ps1`, `PRIVACY.md`, `site/privacy.html`, `site/dino/privacy.html`, `STORE.md`, `src/DinoReader.App/Platforms/iOS/Resources/PrivacyInfo.xcprivacy`, `src/ReadTheStupidText.Mobile/Platforms/iOS/Resources/PrivacyInfo.xcprivacy`

Planned 2026-10-27, the day counting was switched on for both apps (PR #487). The counts say what people do but not
where or on what: which countries to translate for, which OS versions to keep, whether slow reads are a low-end-phone
problem, and whether people read on tablets (picture layout).

**Decisions (settled with the user, 2026-10-27):**

- Add five context fields to every count of **both apps** (Android, iOS and Windows, one code path each):
  - **country**: ISO 3166-1 alpha-2 from `request.cf.country`, set by the Worker, never sent by the app. `XX`, `T1`
    (Tor) and anything not two capital letters become `none`.
  - **OS major version**: Dino gains it; RTST already sends `osMajor` on `app_opened` and now carries it on every count.
  - **form factor**: closed list `phone | tablet | desktop | other`, from MAUI `DeviceInfo.Idiom`; the RTST Windows tray
    app (WinUI, no MAUI) always sends `desktop`.
  - **CPU**: architecture (`arm64 | x64 | x86 | arm | other`, `RuntimeInformation.OSArchitecture`) and a core-count
    bucket (`1-4 | 5-8 | 9+`, `Environment.ProcessorCount`).
  - **RAM**: bucket in GB (`<3 | 3-4 | 4-6 | 6-8 | 8+`) from total physical memory: Android `ActivityManager.MemoryInfo.TotalMem`,
    iOS `NSProcessInfo.PhysicalMemory`, Windows `GlobalMemoryStatusEx` (or `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes`
    if it reports physical memory on every head; check with library-docs first).
- **Never** the device model or manufacturer, the exact chip, exact RAM/core numbers, the full OS build, or any id.
  Buckets only: exact values together with country would fingerprint a single phone (kids' app: COPPA profile rule,
  UK Children's code Standard 8; see docs/plan/notes/2026-10-25-dino-usage-insights.md).
- **Shape:** the app sends the device fields once per batch in a `device` object next to `events`
  (`{schemaVersion, device: {osMajor, formFactor, arch, cores, ram}, events}`), validated once by the Worker against
  closed lists and stamped on every point of the batch. Every field is **optional**: builds already in people's hands
  send no `device` object and their counts must still be written (fields empty), never dropped. A present but invalid
  field becomes empty; it never drops the count.
- **Layout:** RTST has only blob18-blob20 free, so both apps use the same layout for the new fields: text fields as
  blobs (Dino blob10 country, blob11 osMajor, blob12 formFactor, blob13 arch, blob14 cores, blob15 ram; RTST blob18
  country, blob19 formFactor, blob20 arch) and RTST's two buckets as doubles (double2 core bucket lower bound 1/5/9,
  double3 RAM bucket lower bound 0/3/4/6/8). RTST's existing blob11 `osMajor` stays where it is and is now filled on
  every event. Document both layouts in the report scripts' headers.
- **Order (legal gate, as Decision 145):** the store answers and privacy pages are updated and published **before** the
  Worker change is merged, because country is recorded from the moment the Worker deploys.

**Work**

- [x] **Worker, end to end for Dino first:** accept the optional `device` object, read `request.cf.country`, write the
  new blobs; old-shape batches still written. Tests in `worker/events.test.mjs`: new shape, old shape (no `device`),
  invalid values become empty, `XX`/`T1` become `none`, no country header.
- [x] Worker for RTST: same envelope, blob18-20 and double2-3; `osMajor` from `device` on every event (fall back to the
  `app_opened` field for old builds).
- [x] Shared client: one `DeviceContext` (pure bucketing in Application/Core, unit-tested: boundaries of each bucket,
  unknown arch to `other`) plus a platform probe per head.
- [x] **Android:** probe (Idiom, arch, cores, `ActivityManager` RAM) for Dino and RTST phone. Emulator run: one batch
  shows the `device` object in logcat/HTTP and a row in each dataset with every new field filled.
- [x] **iOS:** probe (Idiom, arch, cores, `NSProcessInfo.PhysicalMemory`) for Dino and RTST phone. iOS compile on CI;
  simulator if available.
- [ ] **Windows:** Dino Windows head (MAUI probe, `desktop`) and RTST tray app (`WindowsUsage.cs`, `desktop`, physical
  memory). Desktop run: a batch reaches the Worker with the device object.
- [x] Report scripts: group-by options for country, OS major, form factor, arch, cores and RAM, still behind the
  10-event floor; header documents the new layout.
- [x] Privacy texts: `PRIVACY.md`, `site/privacy.html`, `site/dino/privacy.html` (name each new field and that country
  comes from the connection, not GPS, and the IP itself is never stored), `STORE.md` answers below, both
  `PrivacyInfo.xcprivacy` (add `NSPrivacyCollectedDataTypeCoarseLocation` and `NSPrivacyCollectedDataTypeOtherDiagnosticData`,
  linked false, tracking false, purpose analytics).
- [x] Store forms (Claude fills them in the user's Chrome; the user presses Submit/Publish), **before the merge**:
  - Google Play Data safety, both apps: add **Location > Approximate location** (collected, not shared, not
    ephemeral, optional, Analytics). Diagnostics is already declared on both.
  - Apple App Privacy, both apps: add **Coarse Location** and **Other Diagnostic Data** (not linked, no tracking,
    Analytics).
  - Microsoft Store: the privacy policy URL already points at the updated page; nothing else asked.
  - Check Google Play's Families policy wording on approximate location for Dino before submitting.
- [ ] Merge only after the user has published the forms; then confirm in the Analytics Engine studio that new rows
  carry the fields (and old-build rows still arrive with them empty).

**Verification**

- `node --test worker/events.test.mjs`, `npm run typecheck`.
- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` and `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj` (bucket boundaries).
- Android emulator (both apps), iOS compile (both apps), Windows desktop (Dino head and RTST tray): one batch each with
  the `device` object, and the matching row in `dino_usage` / `rtst_usage` via the Analytics Engine studio.
- Live store pages show approximate/coarse location and the extra diagnostics after the user publishes.

**Built notes (2026-10-27):** RAM bucket rounds total memory up to whole GiB first, so an advertised 4 GB phone lands in 4-6. RTST rows from builds without a `device` object write -1 to double2/double3; report scripts filter older rows (blob19 empty). Store forms published by the user before merge: Play Approximate location (both apps), Apple Coarse Location and Other Diagnostic Data (both apps). In-app consent strings (Dino `Usage_Helper`, `FirstRun_Usage`; RTST `Settings_CountsCaption`, `Language_CountsNote`) mention country and device ranges in all 13 languages (machine translations, need a native check). RTST Settings now scrolls (the longer caption was cut off on a 320x570 screen). Android emulator: both apps sent a batch with all five device fields.

**Query notes (Slice 179, 2026-10-28):** reports leave out Android and iOS rows whose arch (RTST blob20, Dino blob13) is `x64` or `x86`: Play's pre-launch robot and emulators (the Worker drops them from Slice 179 on, older rows stay). Windows rows from before Slice 179 may be our own runs of CI or test MSIX builds (a burst of `app_opened` from one version in minutes); after it only Store-signed installs count.

**blob9 notes (Slice 200, live rows 2026-10-30):** Dino `blob9` is the per-event detail, empty when an event has none. Seen live: `voice_changed` = `narrator` | `flavour` | `character_voice`; `reading_language_changed` = the language it changed from (`fa`, `de`, `nl`, `en`); `store_download` = `none` (no filter in use); `story_languages_set` = `count:intro|settings` (`2:settings`, `3:intro`); `story_language_added` and `story_language_switched` = empty; `own_book_added` = `typed` on every row so far (the old format from builds up to 1.71.2; Slice 178's `kind:bucket` shape has no live row yet); `wait_shown`/`wait_cancelled` = `stage:duration bucket`. Split `kind:bucket` client-side, never with SQL string functions. Windows Dino rows carry country and form factor (`desktop`) like the phones.
