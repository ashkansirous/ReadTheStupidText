# Slice 75 — Windows translation engine

*In progress (wave 2026-10-21), draft PR.*

**Batch:** 9
**Decisions:** 67, 68
**Depends on:** Slice 57 (localized UI and the inert translate-before-read flow)
**Touches:** `src/ReadTheStupidText.Infrastructure/Translation/**`, `src/ReadTheStupidText.Infrastructure/ReadTheStupidText.Infrastructure.csproj`, `src/ReadTheStupidText.App/**` (only to unhide the translate UI and DI wiring), `src/ReadTheStupidText.Application/Translation/**`, `src/ReadTheStupidText.Application/Reading/ReadAloudService.cs`, `.github/workflows/packs.yml`, `packs.json`, `tests/ReadTheStupidText.Tests/**`, `STORE.md`, `PRIVACY.md`
**Story issue:** —

## Goal

Make the hidden translate-before-read toggle on Windows work fully offline. Slice 57 shipped the flow (`ReadTranslator`, `TranslationFlow`, toggle, source picker, note, tray item) but `PackTranslator.IsAvailable` is false and `TranslateAsync` always raises `TranslationModelUnavailableException`, so the UI is hidden.

## Steps

- [ ] **Translation research (before code).** Compare Opus-MT ONNX models
      (Helsinki-NLP/OPUS-MT, via onnxruntime, with pivot through English) with
      Bergamot (Firefox translation models, own native runtime): licence of
      engine and each pair's model, whether `fa` is covered in both directions,
      per-pair size (target < ~100 MB), CPU speed, and tokenizer needs
      (SentencePiece). Recommended default per Decision 67 is Opus-MT ONNX; record
      the decision and numbers here and in `plan.md`'s Decisions if it changes.
- [ ] **Translation packs.** Ship translation models as packs (kind `translation`)
      through the Slice 50/56 pack service and `packs.yml`; `packs.json` lists
      `xx -> en` and `en -> xx` pairs for the user's language (pivot through English).
- [ ] **A real `PackTranslator.TranslateAsync`** (ONNX inference plus SentencePiece), with
      `IsAvailable` true once the pair's pack is present, which unhides the toggle, source
      picker, note and tray item.
- [ ] **Translate the first sentences first.** Translation adds latency before the first word and
      Slice 47's head start does not cover it, so translate the first sentences first to keep
      time-to-first-audio low.
- [ ] `STORE.md` third-party notices for the chosen models; `PRIVACY.md` check (still 100% offline, only packs are downloaded).

## Notes

- Auto-read must not stall: the 20 s budget and fall-back to the original text from Slice 57 stay.
- Split out of Slice 57 on 2026-10-21; the exact original wording is in `docs/plan/slices/57-windows-localization-translation.md` (steps marked "-> Slice 75").
