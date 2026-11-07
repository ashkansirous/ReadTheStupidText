# Slice 294 — The `-NoBuild` cache-key miss in the emulator runner

*In progress (wave 2026-11-07), draft PR.* *Planned 2026-11-07.* Slice 275 item 4, open: `run-flows -NoBuild` once computed a different build-cache key than `install-debug -BuildOnly` in the same worktree with no source change between them (63d09e94b4b3 versus 95de159d430d). Source: [Slice 275](275-night-2026-11-06-0042-leftovers.md) item 4 (half done: `tools/emulator/build-cache.ps1` prints the key's inputs on a miss; on 2026-11-06 `-NoBuild` hit the cache for both apps, key `78daf6ab6d08`). Moved from plan.md's *Outstanding work*. Tooling only; no app change.

**Touches:** `tools/emulator/build-cache.ps1`, `tools/emulator/run-flows.ps1`, `tools/emulator/install-debug.ps1`, `tools/emulator/common.ps1`, `tools/CLAUDE.md`, `docs/plan/slices/294-emulator-nobuild-cache-key-miss.md`

## Work

- [ ] 1. Reproduce: in one worktree, run `install-debug -BuildOnly`, then `run-flows -NoBuild`, with no source change; print both keys' inputs (the miss output already lists them). Find which input differs (a file mtime, an untracked file, a generated file, line-ending filter, an env value).
- [ ] 2. Fix the input so both callers compute one key; add a self-test in `tools/emulator` that calls both and compares.
- [ ] 3. Look at the Slice 275 `first-run` flake ("StoryTile not visible within 30 s", passed on rerun) next to it, as Slice 284 item 6 says.

## Verification

- The self-test passes; `-NoBuild` after `-BuildOnly` hits the cache for both apps on three runs in a row.
