# Slice 150 — Dino: slower reading by default, and a grown-up speed setting

*Built 2026-10-26 (PR #429), not yet verified on a real phone.* Dino Reader only; RTST is not touched.

*Changed while building:* Dino stores its own key `dino.reading.speed` instead of the shared `ISettingsStore.Speed`, because the shared store cannot tell a missing value from a stored one. `SpeechStartup` loads and clamps it; any read failure falls back to 0.8. The card is a stock MAUI `Slider` styled to fit, not the mock's 28 px thumb. The Reading speed card sits on `LanguagePage` (grown-up settings) before the usage card. The label updates while dragging; the value is saved and `SetSpeed` called on release. "Hear this speed" reuses `Cast_PreviewLine` in the preview language, shows a spinner after 500 ms, and a second tap stops it.

*From review:* two passes (`D:\ReadTheStupidText-worktrees\reviews\150-high.md`, `150-medium.md`). Applied high 1, 2, 3, 6 and medium 1, 2, 3, 4, 7, 8, 9.

*Verified (emulator, headless, 2026-10-26):* 0.8 default, slider ends, steps, persistence, sample play/stop all pass; no crash.

**Batch:** 31 (tester review, 2026-10-26; Decision 155)
**Touches:** `src/DinoReader.App/SpeechStartup.cs`, `src/DinoReader.App/SettingsRows.cs`, `src/DinoReader.App/GrownUpMode.cs`, `src/DinoReader.App/LanguagePage.xaml.cs`, `src/DinoReader.Core/ReadingSpeed.cs` (new), `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`

## Why

A tester review (2026-10-26) found the reading too fast for small children. Dino always reads at 1.0x:
`SpeechStartup.cs:33` passes the shared `ISettingsStore.Speed` (`PlaybackRate`, default 1.0), and no Dino
screen ever writes it, so parents cannot change it.

## Decisions locked with the user (Decision 155)

- The default is **0.8x** (about 20% slower).
- Grown-ups can set **0.5x to 1.2x in 0.05 steps**, on the grown-up settings screen, behind the gate. Nothing above 1.2x.
- One setting for the whole app, not per book.
- A character's flavour pace (`VoiceFlavour`) multiplies on top of it, as today with 1.0.
- The shared `PlaybackRate` (0.5-2.0) is unchanged; RTST keeps its range. Dino clamps to its own range in a
  pure `ReadingSpeed` (Core).

## Work

- [x] `ReadingSpeed` (Core): Min 0.5, Max 1.2, Default 0.8, Step 0.05, `Clamp`, built on `PlaybackRate`. Unit tests.
- [x] `SpeechStartup` uses Dino's own default when nothing is stored. The fallback is `src/ReadTheStupidText.Mobile.Shared/MobilePreferencesSettingsStore.cs:37-38`
      (a missing value returns `PlaybackRate.Default`); give Dino 0.8 there or in `SpeechStartup.cs:33`: a stored 1.0 written by nobody must not exist, but a missing value must give 0.8. A stored
      value above 1.2 is clamped.
- [x] A "Reading speed" row in grown-up settings (`SettingsRows`): a slider with the current value ("0.8x"), plus
      a play button that reads a sample sentence at that speed. The new speed applies to the next sentence read.
- [x] Strings in all 14 locales.
- [x] Rule line in `src/DinoReader.App/CLAUDE.md`.

## Verification

- `dotnet test tests/DinoReader.Tests` (ReadingSpeed tests).
- Emulator: a fresh install reads at 0.8x. The slider stops at 0.5 and 1.2, and a change is heard on the next sentence.
  The setting survives an app restart.

#### Design (Dino Reader Batch 31 mock, screen 70, added 2026-10-26)

Source: `docs/design/dino-reader/Dino Reader Batch 31.dc.html`, screen 70 (lines 122-149). One frame at 360x760, real UI. The mock agrees with Decision 155 (0.5x-1.2x, 0.05 steps, default 0.8x, one app-wide setting, flavour pace multiplies on top).

- **Page.** Header: back 44x44, "Settings" 20/600, sub-line "Grown-up mode" 12 `#8a8399`. Cards on `#F3EFE6`, gap 10, side padding 14. Card order: App language and Narrator rows, **Reading speed**, Animal sounds (see Slice 152), "Help make Dino better", Version ("1.43.0"). Rows in the first card are min-height 50, text 14.5, value 14.5/700.
- **Reading speed card.** White, radius 18, padding 14/14/12, gap 10. Top row: title "Reading speed" 15/600; sub-line "Slower is easier for little ones" 12.5 `#5a5470`; value "0.8x" 17/700 `#5B57E8`, min-width 44, right-aligned; then a **Play** button, 44x44, round, fill `#ECE7FF` (play icon).
- **Slider.** Row height 44. Track 8 px, radius 4, fill `#ECE7FF`; filled part `#5B57E8`; thumb 28 px, white, 3 px border `#5B57E8`, shadow `0 2px 6px rgba(43,37,64,.2)`. At 0.8x the fill is 42.86% (0.5-1.2 on one linear scale). End labels 12 `#8a8399`: "0.5x · slow" at the left, "1.2x" at the right.
- **Behaviour (note).** Range 0.5x-1.2x in 0.05 steps. A fresh install starts at 0.8x. Play reads a sample sentence at the chosen speed. A change applies from the next sentence. A character's flavour pace multiplies on top. TalkBack and VoiceOver read the value as "0.8 times".
- **New strings (14 locales).** "Reading speed", "Slower is easier for little ones" (draft), "0.5x · slow", the value format "0.8x", and the accessible value "0.8 times". The mock does not give the sample sentence.

**Settled by the mock (the plan left open).** The row is a card of its own, the value sits left of the Play button, and the left end label says "slow". Still open: the sample sentence. **Default:** a short sentence from a starter book, translated per locale.

#### For CLAUDE.md (lifted 2026-10-27)

None to add: `src/DinoReader.App/CLAUDE.md` already has the rule line for this slice (keep it).

#### Not yet verified

- A mid-book speed change heard on a real phone.
- Windows head and iOS readers apply `SetSpeed` on the next sentence.
- Native-speaker read of the `Speed_*` strings (13 machine drafts).
