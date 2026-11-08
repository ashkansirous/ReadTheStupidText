# Slice 341: Listening page: Farsi short words never dropped (Slice 149)

*Planned 2026-11-08.* The user asked for one slice per check that needs their ear ("Abou the ones that need my ear, I probably need a page to listen to them and say what I think. Add a slice for each so that you can implement a page for me to listen to them and tell what is wrong with them"). This one comes from Slice 149's open check, moved out of `docs/plan/unverified.md` on 2026-11-08 ([record](../notes/2026-11-08-unverified-triage.md)):

> **iOS:** Slice 149: emulator Farsi read passed by logs only, not heard; iOS compile of `IosSupertonicSpeechReader.Synthesis.cs` and `SiblingRescue`; a ... [full text](notes/2026-10-30-outstanding-detail.md#o039) ([slice](slices/149-never-drop-a-short-word.md))

**Touches:** `docs/plan/slices/341-farsi-short-words-never-dropped.md`, `docs/plan/unverified.md`, the voice-pick working folder under the worktrees folder (renders and page files, not in the repo)

## Items

- [x] Make the audio exactly as the app plays it. This is Read The Stupid Text's Farsi path (Dino has had no speech engine since Slice 278). A first render on 2026-11-08 used a Python port of the chunking; the user said none of the clips "say anything proper". It was redone with the app's real C# reading code on Windows (retry and ganji sibling rescue included), piper-fa pack v2 from catalog packs-11, main 9c82f37f, through a throwaway harness that was never committed.
- [x] Publish one private listening page: https://claude.ai/artifact/SMcmfN7etCqhYC7KN9TDii (answers under doc ids `r2-<clip>` and `r2-done`; the older ids belong to the Python-port clips and do not count).
- [ ] **Fix 1 (measured on 2026-11-08 before any answer, from the real C# render):** lone short Farsi sound words come out silent or nearly silent in mana, and the guard does not always rescue them. «میو میو!» renders silent (peak 3% of full scale, 0 s audible) and no rescue fires, so the app would play silence. «تق!» is 0.1 s audible even after the ganji rescue. «ووش!» needs a retake. «هاپ!» is rescued by ganji. ganji alone renders «ووش!» silent, and its retake fails too. Lines of two or more words are audible (27-32% peak). Find why `SilentOutputGuard` lets «میو میو!» through and why the rescues are this weak, fix it in the shared reader for Android, iOS and Windows, and put the new clips on the same page. The Python-port render failed on the same lines, so the port was not the cause.
- [ ] When the user has answered, write every answer into this file, word for word.
- [ ] Each "something is wrong" becomes a fix item in this slice (cause, then fix, through the normal review gate). Every "sounds right" closes the check.

## Verification

The user's answers are recorded here. Each wrong item has a fix merged and its clip re-rendered on the same page for a second listen.
