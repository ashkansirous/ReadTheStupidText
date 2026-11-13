# Slice 371 — Folders grouped by product: src, tests and tools

*Planned 2026-11-13.* Last of the tidy-by-product slices (366-371). User's choice, 2026-11-13 (item 2 of 5). Runs only when no other PR or worktree with changes is open (it moves almost every file); after Slice 370.

**Touches:** everything under `src/`, `tests/`, `tools/`; `ReadTheStupidText.slnx`, `Directory.Build.props`, `src/*.props`, `src/AndroidProguard.cfg`, `global.json` (check), `.github/workflows/*.yml`, `.github/scripts/*`, `.github/CLAUDE.md`, `tools/CLAUDE.md`, root `CLAUDE.md`, `README.md`, `.claude/skills/**` and `.claude/agents/**` that name paths, `package.json`, `tsconfig.json`

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

- [ ] 1. Write the full old-to-new path map for every folder into this file; place every "check" folder.
- [ ] 2. `git mv` per the map; fix every `ProjectReference`, `Import`, `.slnx` entry and relative path in props files.
- [ ] 3. Fix every path in CI: workflow `paths:` and `working-directory`, `ship-targets.mjs` groups and its tests, `site-checks-needed.mjs`, `check-sherpa-pins.mjs`, `check-android-natives.sh`, `check-ios-bundle.sh`, `stage-android-package.sh`.
- [ ] 4. Fix every path in `tools/` scripts, `test-targets.ps1` (the `-App auto` selector), the device-test build cache keys, `package.json`/`tsconfig.json`, `.claude/skills` and `.claude/agents`, the repo-wide guard tests (`FallbackGuardTests` src scan, `SherpaVersionPinTests`, `FallbackGuardBaseline.txt`).
- [ ] 5. Update root `CLAUDE.md` (build commands, layer diagram, *Where the rules live*), `README.md`, `.github/CLAUDE.md`, `tools/CLAUDE.md`.
- [ ] 6. Search for leftovers: `git grep -n -E "src/(ReadTheStupidText|DinoReader)\.|tests/(ReadTheStupidText|DinoReader)\.Tests|tools/(DinoReader\.BookBuilder|sound-words-test|premade-audio)"` outside `docs/plan/` (history docs keep old paths) prints nothing.

## Verification

- All six builds (RTST Windows, Android, iOS; Dino Android, iOS, Windows) and all test projects pass locally and in CI on the PR.
- `ship-targets.test.mjs` and the `tools` self-tests pass; the device-test selector picks the right app for a Dino-only and an RTST-only change.
- One emulator flow per app runs from the moved `tools/emulator` (scripted, under 10 minutes).
- First merge after this slice cuts releases through the moved paths (run ids recorded here).
