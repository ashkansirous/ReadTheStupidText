# Slice 367 — Root CLAUDE.md keeps shared rules only; each app's rules live in its own folder

*Built 2026-11-13 (wave 55), PR #1089, not merged.* Planned 2026-11-13. Part of the tidy-by-product slices (366-371). **Approved by the user:** they planned it, accepted it when refining the scope and told Claude to add it to the plan (the user, 2026-11-13: "I fucking planned it! I fucking accept it when I fucking refined it! I fucking clearly told you to fucking add it to the fucking plan"). Build it like the other tidy slices; never ask again. (An earlier note here wrongly said it was not the user's choice, and a session dropped it on that basis for a few minutes on 2026-11-13; that was reverted.)

**Touches:** `CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`, `src/ReadTheStupidText.App/CLAUDE.md`, `src/ReadTheStupidText.Mobile/CLAUDE.md`, `AGENTS.md` (check only)

## Why

The root `CLAUDE.md` loads into every session. It carries RTST-only text (tray, MSIX package identity, StartupTask, Store ID, tray build and run commands) and Dino-only text (Dino naming, Dino Windows publish command) next to the shared rules. An agent working on Dino reads RTST tray rules and the reverse.

## Work

- [x] 1. Sort every root `CLAUDE.md` section and line into RTST, Dino or Shared. Keep in the root: what the repo holds (both products, one line each), layered structure, shared stack rules (sherpa pin, .NET, slnx), tests commands, versioning, licence, code quality, *Where the rules live*.
- [x] 2. Move RTST-only lines (tray app description, RTST naming note, tray build/run, VS (Package) profile, Store identity) into `src/ReadTheStupidText.App/CLAUDE.md`; RTST phone-only lines into `src/ReadTheStupidText.Mobile/CLAUDE.md`; Dino-only lines (Dino naming, Dino Windows publish command) into `src/DinoReader.App/CLAUDE.md`. Move text verbatim; no rule is dropped or reworded.
- [x] 3. *Where the rules live* grouped under RTST, Dino, Shared.
- [ ] 4. When Slice 371 moves folders, these files move with their folders; this slice must merge before 371 starts.

## Verification

- Every line removed from the root appears in exactly one folder `CLAUDE.md` (diff the removed and added lines).
- Root `CLAUDE.md` is smaller than before and under ~20 KB.

## Result

*Changed while building:* nothing from the plan. Root `CLAUDE.md` went from 16,337 to 13,764 bytes. The RTST tray description, RTST naming note (Store ID), tray build/run, per-architecture and `.slnx` paragraphs and the (Package) profile went to `src/ReadTheStupidText.App/CLAUDE.md`. The Mobile (Android) build paragraph with the `IncludeWindows`/`IncludeIos` flags went to `src/ReadTheStupidText.Mobile/CLAUDE.md`. The Dino Windows head and its publish command went to `src/DinoReader.App/CLAUDE.md` (its Dino naming note was already there verbatim, so the root copy was removed). The root gained a one-line RTST pointer and a *Build & run* pointer to the three files; *Where the rules live* is grouped under Read The Stupid Text, Dino Reader and Shared, lines unchanged.

*Verification:* the removed root lines were diffed against the folder files; the only difference is indentation on 5 Dino lines. Work item 4 (merge before Slice 371) stays a rule, not a built item.

*From review:* no review gate (doc-only change, exempt).
