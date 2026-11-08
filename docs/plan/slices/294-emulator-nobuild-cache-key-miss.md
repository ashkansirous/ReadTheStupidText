# Slice 294 — The `-NoBuild` cache-key miss in the emulator runner

*Built 2026-11-07 (wave 2026-11-07), merged as PR #900 (87126e55); verified on the emulator, cause still not proven.* *Planned 2026-11-07.* Slice 275 item 4, open: `run-flows -NoBuild` once computed a different build-cache key than `install-debug -BuildOnly` in the same worktree with no source change between them (63d09e94b4b3 versus 95de159d430d). Source: [Slice 275](275-night-2026-11-06-0042-leftovers.md) item 4 (half done: `tools/emulator/build-cache.ps1` prints the key's inputs on a miss; on 2026-11-06 `-NoBuild` hit the cache for both apps, key `78daf6ab6d08`). Moved from plan.md's *Outstanding work*. Tooling only; no app change.

**Touches:** `tools/emulator/build-cache.ps1`, `tools/emulator/run-flows.ps1`, `tools/emulator/install-debug.ps1`, `tools/emulator/common.ps1`, `tools/CLAUDE.md`, `.github/workflows/tools-selftests.yml`, `docs/plan/slices/294-emulator-nobuild-cache-key-miss.md`

## Work

- [x] 1. Reproduce: no miss reproduced (repeated runs on an unchanged tree gave one key). Instead the miss printout now names the changed input next time: it prints the git tree hash and the exact input list the key was hashed from, and `build.json` records both.
- [x] 2. Hardening, not a proven fix: `Get-BuildKeyInputs` throws on a git failure; `Get-BuildKey` returns its key from one computation whose input list the miss printout and `Save-BuildMeta` reuse (never recomputed; a git failure there is logged, a finished build is kept and exit 6 stays 6). Self-test `tools/emulator/test-build-key.ps1`: both callers print one key; a failing git throws; a docs change keeps the key; an input change gives a new key.
- [x] 3. Look at the Slice 275 `first-run` flake ("StoryTile not visible within 30 s", passed on rerun) next to it, as Slice 284 item 6 says.

## Cause (likely, not proven: no miss reproduced)

Both callers already call the one `Get-BuildKey` in `build-cache.ps1`; repeated runs on an unchanged tree gave one key, and all 22 recorded `build.json` input lists hash to their own dir key, so a build does not change its own inputs. The only path in the key code that can give a different key for the same tree is `Get-BuildKeyInputs`: it ran `git rev-parse <tree>:<path>` per input inside `try/catch { $null }` and dropped the input on ANY git failure, so one transient git error gave a key with one input fewer (a silent, different key). The other candidate (the reviewer's) is the inputs changing between the separate key computations of one run (Build-BeforeLease, the stale check, install-debug before and after its lock, Save-BuildMeta): any edit under `src/` between two of them gives a different key with no error, which would also match 63d09e94b4b3 versus 95de159d430d. Neither is proven. Changes: one `git ls-tree` call that throws on failure; the key and the exact input list it was hashed from come from one computation, and the miss printout (which also prints the git tree hash) and `build.json` (inputs and tree) reuse that list, so the next miss names the changed input. A git failure while recording or printing is logged, never fatal; the key lookup itself still fails loudly. Test: `tools/emulator/test-build-key.ps1` runs `install-debug -NoBuild` and `run-flows -NoBuild` for both apps through the cache-miss line and requires equal keys, and that no other emulator script computes a key. The test is in `tools-selftests.yml`.

## Review decisions (medium pass)

- Applied: every emulator `-NoBuild` miss (`install-debug.ps1`, `run-flows.ps1`) prints through `Write-EmulatorNoBuildMiss` (miss line, then key inputs and tree hash); a failed `Save-BuildMeta` input record is marked `inputsRecorded = false` and never used as the "newest build" comparison; zero input lines is an error. Not covered (outside Touches): `tools/phone/run-checks.ps1` `Stop-CacheMissed` still prints only its own line.
- Not applied, one computation per run: `-BuildOnly` and `-NoBuild` are separate processes, so each must read the tree. If the tree changed between them, the different key is correct, and the miss printout now names the changed input on the next miss, which proves or rules out that cause.
- Not applied, pruning `BuildKeyResults`: the cache lives for one process.

## Item 3: the `first-run` flake (no cause found; not fixed)

`.maestro/dino/first-run.yaml` waits 25 s for `FirstRunLanguage`, relaunches with `skip-first-run`, waits 25 s for `GalleryTitle`, then 30 s for `StoryTile`. The only saved log is a passing run (`reviews/s275c-emu/dino-first-run.log`): after the relaunch the gallery layout line is at 23:09:28 and the shelf order (tiles) at 23:09:36, so tiles arrive about 8 s after the page, well inside 30 s. The failed run's log was not kept, so nothing shows what took over 30 s (a slow emulator after `clearState`, or the shelf load). No change made; the flake stays in *Not yet verified*. Next time it fails, keep the per-flow logcat (`logcat-i<N>/dino-first-run.log`) and compare the gap between the `shelf layout` and `shelf order` lines.

## Changed while building

- The key, its inputs and the tree hash are computed once per process (`Get-BuildKeyResult`); the miss printout and `build.json` reuse that result.
- Every `-NoBuild` miss in `install-debug.ps1` and `run-flows.ps1` prints the inputs and tree hash through `Write-EmulatorNoBuildMiss`.
- A failed input record in `build.json` is marked `inputsRecorded: false`; zero input lines throws.
- `test-build-key.ps1` runs in `.github/workflows/tools-selftests.yml`.

## From review

Files: `reviews/s294-high.md` (7 findings), `s294-medium.md` (5), `s294-check1.md` (6), `s294-check2.md` (6), all read in full. Outside the repo; each finding left unchanged is listed below with its reason.

- High and medium passes: the outcomes that the code and the Review decisions section above show are recorded there (one computation per result, git failure while recording logged not fatal, self-test cases for a throwing git / docs change / input change, `inputsRecorded:false`, zero-input throw, `Write-EmulatorNoBuildMiss` in both scripts, work items ticked). High pass, all 7 applied: 1-3 one key computation with its inputs, git failures logged (not thrown) in `Save-BuildMeta` and the miss printout; 4 tests for a failing git, an unrelated change and an input change; 5 `core.quotePath=false` and empty lines dropped; 6 the guard scans all of `tools/` with comments stripped; 7 items ticked.
- Skipped medium #5 (`reviews/s294-medium.md`): "script-scoped BuildKeyResults hashtable grows per distinct key, never pruned (long-lived watcher loops)" - the cache lives for one process.
- Skipped medium #1, phone part (`reviews/s294-medium.md`): "phone run-checks and desktop-vm misses never call Write-BuildKeyInputs" - tools/phone/run-checks.ps1 was outside this slice's Touches (see Not yet verified).
- Check pass 1: 1 applied (the not-a-repo test runs the real `Invoke-Git`; shown failing when the exit check is removed); 3 applied (the child-script check asserts exit 6 and the miss line).
- Skipped #2 (`reviews/s294-check1.md`): "turns any stderr line into a terminating NativeCommandError on Windows PowerShell 5.1" - test-build-key passed under powershell.exe 5.1.
- Skipped #4 (`reviews/s294-check1.md`): "Only latency; document as accepted or memoize." - low: re-hashing per call costs a second, in one process.
- Skipped #5 (`reviews/s294-check1.md`): "a configured path missing from the tree (typo, or submodule gitlink) is silently dropped unless all are missing" - a missing path is dropped on purpose, an empty list throws.
- Skipped #6 (`reviews/s294-check1.md`): "Save-BuildMeta on a git/tree failure saves a record with no inputs" - low.
- Check pass 2: none applied; the notes below are one line per finding.
- Skipped #1 (`reviews/s294-check2.md`): "on 5.1 any git stderr (CRLF warning) throws NativeCommandError before the exit-code check" - disproven by passing powershell 5.1 runs.
- Skipped #2 (`reviews/s294-check2.md`): "on 5.1 child stderr throws in the test process" - disproven by passing powershell 5.1 runs.
- Skipped #3 (`reviews/s294-check2.md`): "malformed line yields empty hash and a silently different key" - low, ls-tree hash validation left.
- Skipped #4 (`reviews/s294-check2.md`): "across processes it can print misleading" - low, the cross-process wording left.
- Skipped #5 (`reviews/s294-check2.md`): "can give false positives or negatives in the own-key check" - low; no script triggers it today.
- Skipped #6 (`reviews/s294-check2.md`): "Existing behaviour; skip or throw earlier." - low, left.

## Verification

- The self-test passes in pwsh and powershell 5.1 (`test-build-key.ps1`), and `test-no-build.ps1` passes.
- Emulator 2026-11-07: `install-debug -App dino -BuildOnly` (key `5443226e780e`), then three `run-flows -App dino -NoBuild -Flow back-cover` runs all hit the cache and PASS (logs `reviews/s294-emu/`).

## Not yet verified

- Item 3, the `first-run` flake: no failing log was kept, so no cause and no fix.
- `tools/phone/run-checks.ps1` `Stop-CacheMissed` miss printout does not print the inputs (outside this slice's Touches).
