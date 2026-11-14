# Slice 371 — Folders grouped by product: src, tests and tools
**Product:** Shared

*Built 2026-11-14 (wave 2026-11-14), merged as PR #1109 (303cd267); emulator flows passed, iOS builds, Windows VM run and the first-merge release run not yet verified.*

**Parked (night 2026-11-13-1149, wave 7):** not started. Its own rule needs no other worktree with changes; worktree `gedlg` (`feat/ge-dialogue`, Good Enough redraw, live at 23:55Z) changes 474 files, one of them `tests/DinoReader.Tests/PictureBookSentenceDumpTests.cs`, and the primary checkout holds another session's untracked `src/DinoReader.Core/AppliedOnce.cs`, `tests/DinoReader.Tests/AppliedOnceTests.cs` and `src/DinoReader.BookSample/`. Run it once those are merged or cleared. Correction (feedback loop 2026-11-13-1149): the two `AppliedOnce` files are empty files from 2026-11-08 and `BookSample/` is 12 pictures from 2026-10-23 (Slice 157's known leftover), so they are stale and the move may leave them where they are; only `gedlg` is a real blocker. Moved back to `planned` by the 2026-11-13-1149 feedback loop so the next night checks again.

**Unblocked (user, 2026-11-14):** "I stopped the redraw". The `gedlg` worktree (`feat/ge-dialogue`, no PR, 474 uncommitted files) is no longer live work and no longer blocks this slice: build it now. Leave `gedlg`, the `AppliedOnce` files and `BookSample/` untouched (not this slice's to move or delete); whoever resumes the redraw rebases onto the new folders.

*Planned 2026-11-13.* Last of the tidy-by-product slices (366-371). User's choice, 2026-11-13 (item 2 of 5). Runs only when no other PR or worktree with changes is open (it moves almost every file); after Slice 370.

**Touches:** everything under `src/`, `tests/`, `tools/`; `ReadTheStupidText.slnx`, `Directory.Build.props`, `src/*.props`, `src/AndroidProguard.cfg`, `global.json` (check), `.github/workflows/*.yml`, `.github/scripts/*`, `.github/CLAUDE.md`, `tools/CLAUDE.md`, root `CLAUDE.md`, `README.md`, `.claude/skills/**` and `.claude/agents/**` that name paths, `package.json`, `tsconfig.json`, `.gitattributes`, `.gitignore` (comment), `.maestro/dino/farsi-speaker-first-join.yaml` and `.maestro/dino/premade-audio-starter.yaml` (comments), `STORE.md`, `docs/analytics/dino-dashboard-queries.md`, `docs/analytics/dino-voice-queries.md`, `docs/store-listings/dino/appstore.md`, `docs/voice-packs.md`, `worker/CLAUDE.md` (widened 2026-11-14: old paths in live docs)

## Why

`src/`, `tests/` and `tools/` list RTST, Dino and shared folders side by side; nothing in the tree says which product a folder belongs to.

## Layout

```
src/Shared/   Domain, Application, Documents, Infrastructure, Mobile.Shared (names from Slice 370), SherpaOnnxVersion.props, Android*.props, AndroidProguard.cfg
src/Rtst/     ReadTheStupidText.App, ReadTheStupidText.Windows.Tray, ReadTheStupidText.Mobile
src/Dino/     DinoReader.App, DinoReader.Core (and DinoReader.BookSample if it lands)
tests/Shared/ tests/Rtst/ tests/Dino/
tools/        shared harnesses stay: emulator, phone, desktop-vm, crashes, lease and path scripts
tools/dino/   DinoReader.BookBuilder, sound-words-test, premade-audio, prepare-story-pictures.ps1, upload-store-story.ps1, dino-usage-report.ps1, stats-page-check, voice-listen (check), ocr-starter-pages.ps1 (check)
tools/rtst/   rtst-usage-report.ps1
```

Folders marked "check" are placed by reading what they do; any that serve both stay in `tools/`.

## Constraints

- **Windows path length:** Android builds already sit near 260 characters (global rule, slug about 12 characters). Run `tools/path-length.ps1` (or its new place) on the deepest Android and iOS intermediate paths of both apps before and after; the extra `Shared/`, `Rtst/`, `Dino/` segment must not push any past the limit. If it does, use shorter folder names before merging.
- Every move is a `git mv` so history follows (`git log --follow`).
- Installed apps are untouched: no id, URL or string changes in this slice; only paths.
- Folder `CLAUDE.md` files move with their folders; the root *Where the rules live* list is updated.

## Work

- [x] 1. Write the full old-to-new path map for every folder into this file; place every "check" folder. (map under *Built*)
- [x] 2. `git mv` per the map; fix every `ProjectReference`, `Import`, `.slnx` entry and relative path in props files.
- [x] 3. Fix every path in CI: workflow `paths:` and `working-directory`, `ship-targets.mjs` groups and its tests, `site-checks-needed.mjs`, `check-sherpa-pins.mjs`, `check-android-natives.sh`, `check-ios-bundle.sh`, `stage-android-package.sh`.
- [x] 4. Fix every path in `tools/` scripts, `test-targets.ps1` (the `-App auto` selector), the device-test build cache keys, `package.json`/`tsconfig.json`, `.claude/skills` and `.claude/agents`, the repo-wide guard tests (`FallbackGuardTests` src scan, `SherpaVersionPinTests`, `FallbackGuardBaseline.txt`).
- [x] 5. Update root `CLAUDE.md` (build commands, layer diagram, *Where the rules live*), `README.md`, `.github/CLAUDE.md`, `tools/CLAUDE.md`.
- [x] 6. (left as history on purpose, still matching the grep: `detailed-code-review.md` and `summary-code-review.md` (2026-06 review records), `design_handoff_tray_panel/README.md` (design handoff copy), `content/dino/freddy-and-the-slipper/art.md` (book art record; editing content could touch a book's hash), `plan.md` (index pass)) Search for leftovers: `git grep -n -E "src/(ReadTheStupidText|DinoReader)\.|tests/(ReadTheStupidText|DinoReader)\.Tests|tools/(DinoReader\.BookBuilder|sound-words-test|premade-audio|stats-page-check|voice-listen|prepare-story-pictures|upload-store-story|dino-usage-report|ocr-starter-pages|picture-review-copy|rtst-usage-report)"` outside `docs/plan/` (history docs keep old paths) prints nothing except lines that already read `tools/dino/` or `tools/rtst/` (filter them with `grep -v -E "tools/(dino|rtst)/"`).

## Verification

- All six builds (RTST Windows, Android, iOS; Dino Android, iOS, Windows) and all test projects pass locally and in CI on the PR.
- `ship-targets.test.mjs` and the `tools` self-tests pass; the device-test selector picks the right app for a Dino-only and an RTST-only change.
- One emulator flow per app runs from the moved `tools/emulator` (scripted, under 10 minutes).
- First merge after this slice cuts releases through the moved paths (run ids recorded here).

## Built (implementer, 2026-11-14; not yet reviewed)

**Path map** (every move a `git mv`; project, folder and namespace names unchanged):

- `src/Sirous.Reading.{Domain,Application,Documents,Infrastructure,Mobile.Shared}` -> `src/Shared/<same>`; `src/{SherpaOnnxVersion.props,AndroidRelease.props,AndroidSherpaPackaging.props,AndroidProguard.cfg}` -> `src/Shared/`
- `src/ReadTheStupidText.{App,Windows.Tray,Mobile}` -> `src/Rtst/<same>`
- `src/DinoReader.{App,Core}` -> `src/Dino/<same>` (`DinoReader.BookSample` is untracked on the primary checkout only; not moved)
- `tests/Shared.Tests` -> `tests/Shared/Shared.Tests`; `tests/ReadTheStupidText.Tests` -> `tests/Rtst/ReadTheStupidText.Tests`; `tests/DinoReader.Tests` -> `tests/Dino/DinoReader.Tests`
- `tools/{DinoReader.BookBuilder,sound-words-test,premade-audio,stats-page-check,voice-listen,prepare-story-pictures.ps1,upload-store-story.ps1,dino-usage-report.ps1,ocr-starter-pages.ps1,picture-review-copy.ps1}` -> `tools/dino/<same>`
- `tools/rtst-usage-report.ps1` -> `tools/rtst/`
- Placed by reading: `voice-listen` (Dino roster and Dino casts) and `ocr-starter-pages.ps1` (Dino starter pictures) and `picture-review-copy.ps1` (only the Dino picture tools use it) go to `tools/dino/`. **Stayed in `tools/`:** `usage-sessions/` (reads `dino_usage` but `docs/analytics/*` and `worker/CLAUDE.md`, outside Touches, name its path), `emulator`, `phone`, `desktop-vm`, `crashes`, `path-length.ps1`, lease/detached/test scripts.
- Depth fixes in moved tools: `upload-store-story.ps1` (`'..' '..'`), `stats-page-check/check.mjs` (`../../..`), `premade-audio/generate.py` (`REPO_ROOT`) and `test_premade_audio.py` (`parents[3]`).
- `ReadTheStupidText.slnx` keeps its flat `/src/`, `/tests/`, `/tools/` solution folders (only the `Path=` values changed; the file is CRLF and a split into per-product folders was left out).
- `tools/test-targets.ps1 -Checks` prints `dotnet test tests/<group>/<project>/...` through a `$testGroups` map.
- `.gitattributes` (LFS `VoiceModel` patterns) had to follow the move; it is outside the Touches line (see below).

**Checks run (local):** `dotnet test` of Shared.Tests 1677 passed, ReadTheStupidText.Tests 630 total (628 passed, 2 skipped), DinoReader.Tests 7169 total (7167 passed, 2 skipped), 0 failed; no test added or removed, so the counts equal origin/main's by construction (origin/main was not re-run). Builds with 0 errors: RTST Windows (x64), Dino Windows (x64), RTST Android, Dino Android. `node --test ".github/scripts/*.test.mjs"` 356/356 (it caught the linked-test-file list in `ship-targets.mjs`); `crashes` + `usage-sessions` node tests 42/42; `premade-audio` python tests 121 OK; `tools/test-test-targets.ps1` passes; `tools/run-selftests-ci-form.ps1` (CI form) exit 0, 0 failing; `lint-flows.ps1` 119 flows, 0 findings; actionlint exit 0. The selector (`test-targets.ps1 -Raw`) picks `dino` for a Dino.App file and `rtst` for an Rtst.App file. **NOT RUN:** iOS builds (no Mac), emulator flow, VM, CI on the PR.

**Path length** (worktree `D:\ReadTheStupidText-worktrees\s371`, 35 chars; limit 260). Longest file under each project's `obj`/`bin` after an Android Debug build, repo-relative: RTST Mobile 198 (193 before, one folder segment `Rtst/` shorter), Dino App 188 (183 before). With the worktree prefix: 234 and 224, both under 260. The emulator and phone builds use `--artifacts-path`, whose depth does not depend on the source folders, so `path-length.ps1` is unchanged. The deepest tracked source path is Dino App 106 (101 before) and Mobile.Shared 92 (85 before).

**Leftovers (superseded: the live docs listed here were updated after the Touches widening; only the four history files named at item 6 remain)** (the Work item 6 grep printed these; they are prose or comments, none breaks a build, test or CI run): `.gitignore:91` (comment), `.maestro/dino/farsi-speaker-first-join.yaml:13` and `.maestro/dino/premade-audio-starter.yaml` (comments), `STORE.md` (lines 119, 430, 456, 498, 522, 524, 818, 847, 888), `content/dino/freddy-and-the-slipper/art.md:209`, `design_handoff_tray_panel/README.md:286`, `docs/analytics/*.md`, `docs/store-listings/dino/appstore.md:52`, `docs/voice-packs.md:165`, `worker/CLAUDE.md`, `plan.md` (lines 110, 112, 125; index pass), `detailed-code-review.md` and `summary-code-review.md` (stale review files at the repo root).

**Owed:** the verification items that need CI, an emulator and a release run (first merge's release run ids) are not done here.

*Changed while building:* `.gitattributes` (LFS `VoiceModel` patterns) followed the move and the live docs listing old paths were updated; both are outside the original Touches line (the live docs are now on it).

**Emulator (2026-11-14):** from the moved `tools/emulator`, PASS RTST paste-and-read (67.5 s) and PASS Dino open-book (866 s, slow emulator). Logs: `D:/ReadTheStupidText-worktrees/reviews/s371-device/`.

**Checks (final):** builds RTST Windows x64, Dino Windows x64, RTST Android, Dino Android 0 errors; tests Shared 1677, RTST 628 (+2 skipped), Dino 7167 (+2 skipped), 0 failed; `node .github/scripts` 356/356; selftests CI form exit 0; actionlint 0; longest path 234 (RTST Mobile obj) with the 35-character worktree prefix.

*From review:* (`reviews/s371-high.md` findings 4/4 read, `reviews/s371-medium.md` findings 2/2 read)
- Applied #1 (`reviews/s371-high.md`): `worker/CLAUDE.md` stats-page rule path updated to `tools/dino/stats-page-check/check.mjs`.
- Applied #2 (`reviews/s371-high.md`): Work item 6 grep widened to every moved tool name.
- Skipped #3 (`reviews/s371-high.md`): "Not a regression" - `$noDevicePrefixes` in `tools/test-targets.ps1` lists the same per-file entries as on origin/main.
- Skipped #4 (`reviews/s371-high.md`): "no measurement recorded" - speculative; the PR's CI builds the Windows heads.
- Skipped #1 (`reviews/s371-medium.md`): "not re-measured after sources moved" - emulator builds use `--artifacts-path` (`tools/emulator/build-cache.ps1:173`), so source depth is not in the intermediate paths.
- Skipped #2 (`reviews/s371-medium.md`): "No runtime effect" - the text is the same as on origin/main; it is a project-relative name.

#### For CLAUDE.md (lifted 2026-11-14)

Nothing beyond the diff: the root layer diagram and *Where the rules live* list were already updated by this slice (Slice 371: docs/plan/slices/371-folders-by-product.md).

#### Not yet verified

- iOS builds of both apps from the new folders (no Mac; CI builds them on the PR or at the first release).
- Windows VM run (`tools/desktop-vm`) from the new paths.
- First merge's release run ids through the moved paths (Verification line 4).
