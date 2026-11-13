# Slice 337: Listening page: Dino first-run hello line sounds whole
**Product:** Dino

*Dropped 2026-11-08 (planned 2026-11-08).* **Why dropped:** Dino has no speech engine since Slice 278, so the first run plays nothing (a 30 s S21 recording of a fresh install was digital silence, -91 dB). The stale "Dino downloads a voice for it" line is no longer in the code on main (removed by Slice 325 part D). A listening page was made (https://claude.ai/artifact/992axYddYaAGWkFuPePHUM) and is withdrawn.

*Planned 2026-11-08.* The user asked for one slice per check that needs their ear ("Abou the ones that need my ear, I probably need a page to listen to them and say what I think. Add a slice for each so that you can implement a page for me to listen to them and tell what is wrong with them"). This one comes from Slice 214's open check, moved out of `docs/plan/unverified.md` on 2026-11-08 ([record](../notes/2026-11-08-unverified-triage.md)):

> **Android:** Dino: the first-run hello line (and the voice preview on a reader without segment support) now gets the short first piece; check on a phone that it still sounds whole ([slice](slices/214-rtst-phone-cold-first-audio.md)).

**Touches:** `docs/plan/slices/337-dino-first-run-hello-line-sounds-whole.md`, `docs/plan/unverified.md`, the voice-pick working folder under the worktrees folder (renders and page files, not in the repo)

## Items

- [x] ~~Make the audio exactly as the app plays it~~ (dropped 2026-11-08, see the status line: Dino has no speech engine, no hello line to render).
- [x] Publish one private listening page: made (https://claude.ai/artifact/992axYddYaAGWkFuPePHUM) and withdrawn unanswered.
- [x] ~~When the user has answered, write every answer into this file~~ (dropped 2026-11-08; nothing to answer).
- [x] ~~Each "something is wrong" becomes a fix item~~ (dropped 2026-11-08; no check left).

## Verification

The user's answers are recorded here. Each wrong item has a fix merged and its clip re-rendered on the same page for a second listen.
