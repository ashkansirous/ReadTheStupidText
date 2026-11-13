# Slice 367 — Root CLAUDE.md keeps shared rules only; each app's rules live in its own folder

*Planned 2026-11-13.* Part of the tidy-by-product slices (366-371).

**Not the user's choice:** this slice was suggested by Claude in the 2026-11-13 planning session; the user chose items 1-5 (Slices 366, 368-371) and did not answer this one. Drop it if the user says no.

**Touches:** `CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`, `src/ReadTheStupidText.App/CLAUDE.md`, `src/ReadTheStupidText.Mobile/CLAUDE.md`, `AGENTS.md` (check only)

## Why

The root `CLAUDE.md` loads into every session. It carries RTST-only text (tray, MSIX package identity, StartupTask, Store ID, tray build and run commands) and Dino-only text (Dino naming, Dino Windows publish command) next to the shared rules. An agent working on Dino reads RTST tray rules and the reverse.

## Work

- [ ] 1. Sort every root `CLAUDE.md` section and line into RTST, Dino or Shared. Keep in the root: what the repo holds (both products, one line each), layered structure, shared stack rules (sherpa pin, .NET, slnx), tests commands, versioning, licence, code quality, *Where the rules live*.
- [ ] 2. Move RTST-only lines (tray app description, RTST naming note, tray build/run, VS (Package) profile, Store identity) into `src/ReadTheStupidText.App/CLAUDE.md`; RTST phone-only lines into `src/ReadTheStupidText.Mobile/CLAUDE.md`; Dino-only lines (Dino naming, Dino Windows publish command) into `src/DinoReader.App/CLAUDE.md`. Move text verbatim; no rule is dropped or reworded.
- [ ] 3. *Where the rules live* grouped under RTST, Dino, Shared.
- [ ] 4. When Slice 371 moves folders, these files move with their folders; this slice must merge before 371 starts.

## Verification

- Every line removed from the root appears in exactly one folder `CLAUDE.md` (diff the removed and added lines).
- Root `CLAUDE.md` is smaller than before and under ~20 KB.
