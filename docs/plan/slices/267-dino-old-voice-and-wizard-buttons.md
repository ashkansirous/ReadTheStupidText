# Slice 267 — Dino bugs: books on the shelf keep the old voice; the first-run buttons overlap

*Planned 2026-11-04.* Bug fixes, Dino Reader, Android and iOS alike. Reported by the user on the latest Android build (dino-v1.102.1, 2026-11-04 20:10 UTC): "why the hell is it still using the old shitty voice? We changed that yesterday" and "the skip and check it out are on top of each other". The user also asked why the tests passed; the answers are under each bug.

**Touches:** `src/DinoReader.App/BookUpdater.cs`, `src/DinoReader.App/StoreClient.cs`, `src/DinoReader.App/AudioPackStore.cs`, `src/DinoReader.App/FirstRunPage.xaml`, `src/DinoReader.App/FirstRunPage.xaml.cs`, `src/DinoReader.App/IntroWizardPage*.cs`, `src/DinoReader.App/Testing/DinoTestCommands.cs`, `src/DinoReader.Core/**` (pack-due rule, new file), `tests/DinoReader.Tests/**`, `.maestro/dino/**`, `tools/emulator/**` (layout check), `tools/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`, `docs/plan/slices/267-dino-old-voice-and-wizard-buttons.md`.

## Bug A: books already on the shelf read with the device voice

**What is live (read 2026-11-04 ~20:20 UTC):** `/api/stories` lists 16 books, every one with pre-made audio in 15 languages (the four starters included). The pre-made playback code is in every build from dino-v1.100.0 (Slice 246 part C, #790).

**Likely cause (from the code, not seen on the phone):** a book gets its audio pack at install (`StoreClient.cs:158-167`), but a book that was already on the shelf gets it only from `BookUpdater.SyncPacksAsync` (`BookUpdater.cs:239-255`), which runs from the update check at most once every 7 days on a cold start (`BookUpdater.cs:7, 30-33`, key `dino.books.lastupdatecheck`). A phone that checked in the last week keeps the device voice for every book it already has, up to 7 days. Without a pack, `StoryVoicing.cs:129-139` falls back to the device voice and nothing is logged in a release build. A failed pack fetch is swallowed and waits for the next 7-day check (`BookUpdater.cs:226-233`, `StoreClient.cs:285-297`).

**Why the tests passed:** every pre-made audio flow (`premade-audio-plays`, `premade-audio-starter`) installs or fetches the book's pack on a fresh emulator install and then reads; none starts from an install with books already on the shelf and then upgrades. The upgrade path, which is what every real phone had, was never tested.

- [ ] Check first on the user's phone (wireless adb, Debug log or `dumpsys`): app version, `dino.books.lastupdatecheck`, whether `files/.../audio-packs/<book>/` exists. Write the result here; if the cause is different, fix that instead.
- [ ] Fix: missing packs no longer wait for the 7-day check. On every cold start (and right after a story language is added), compare the shelf books with the store list's `audio` entries and fetch any missing pack for the app's languages, one at a time, resumable; the 7-day check stays for book updates. Progress shows as a small note (any wait over 500 ms), never blocking reading.
- [ ] A release-build log line names which voice a read used (pre-made pack, or device voice and why), so a report like this can be checked from the phone's log.
- [ ] Test the real path: an emulator flow installs the previous release build, opens a book (device voice), installs the new build over it, cold-starts, and asserts `[AudioPack] premade clip plays` for that same book without any test hook fetching the pack. Android task and iOS task (the same code, iOS compile in CI).

## Bug B: "Skip" and "See what Dino can do" sit on top of each other

**Not a change from today:** no commit between dino-v1.100.0 and dino-v1.102.1 touched `FirstRunPage*`, `IntroWizardPage*` or the shared layout rules (`OnboardingLayout`, `LandscapeLayout`, `TabletPageLayout`, `WindowLayout`); the last layout changes were Slice 232 part E (`d860f63a`) and Slice 234 part B (`5a5a49f6`). Most likely screen (unproven): the Ready screen, `FirstRunPage.xaml:104-118`, a `VerticalStackLayout` (`ReadyActions`) holding the usage line, the "See what Dino can do" button and the "Skip, start reading" button inside a sized Grid row; on a short screen, a tall translation or a large font size, that row can be too small for the stack.

**Why the tests passed:** the flows check that a text is visible and tap it by text. Maestro taps the centre of the element's bounds from the accessibility tree, so a button covered by another still "passes". No flow checks bounds, overlap or clipping; the only screenshots (`dino-landscape-reading`, `-settings`, `-shelf`) are saved and never compared or looked at; no flow runs the Ready screen on a phone in portrait with a large font.

- [ ] Reproduce: the user's screenshot, phone model and font size (Android Settings > Display > Font size) first; then the same screen size and font scale on the emulator (`wm size`, `wm density`, `settings put system font_scale`). Name the screen and the cause here.
- [ ] Fix the layout so the buttons stack with their full height at every screen size and font scale (no fixed row height that the stack can outgrow), portrait and landscape, phone and tablet; Android and iOS share the page.
- [ ] Layout check in the device tests: a scripted step reads the element bounds from `uiautomator dump` and fails when two tappable elements on one screen overlap or one is cut off by the screen edge. Run it on every first-run and intro screen, on a phone in portrait at font scale 1.0 and 1.3, and in landscape. Same idea for iOS through the XCUITest tree where a simulator run exists, else recorded under *Not yet verified*.
- [ ] Screenshots taken by flows are put on one contact sheet per run (`reviews/<slice>-shots/`) for the review step, instead of being saved and never seen.

## Verification

- `dotnet test tests/DinoReader.Tests`; the upgrade flow (Bug A) and the layout check (Bug B) on the emulator, both shown failing on the current build first, then passing.
- The user's phone: after the fix build, a book already on the shelf reads with the pre-made voice after one cold start, and the Ready screen shows both buttons apart.
