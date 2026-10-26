# Slice 138 — RTST phone: a wait indicator after 500 ms

*Built 2026-10-26 (wave PR #390); emulator checks passed 2026-10-26.*

**Batch:** 29 (Decisions 143-148)
**Touches:** `src/ReadTheStupidText.Mobile.Shared/Waiting/**` (the shared overlay, Decision 149; built here or by Slice 143, whichever goes first), `src/ReadTheStupidText.Mobile/ReaderPage.xaml`, `src/ReadTheStupidText.Mobile/ReaderPage*.cs`, `src/ReadTheStupidText.Mobile/ReviewPage.xaml`, `src/ReadTheStupidText.Mobile/ReviewPage.xaml.cs`, `src/ReadTheStupidText.Application/Reading/WaitStage.cs` (new), `src/ReadTheStupidText.Application/Usage/**`, `src/ReadTheStupidText.Mobile/Resources/Strings/*.resx`, `src/ReadTheStupidText.Mobile/SettingsPage.xaml*` (dark speed-chip contrast), `tests/ReadTheStupidText.Tests/**`

## Why

A user thought the app hung after tapping Translate. Whenever the user waits on the phone (model
download, translating, preparing the voice, or playback stalled because synthesis is behind), they
must see that it is working and on what.

## Decisions (locked with the user, 2026-10-26)

- **Decision 146:** after 500 ms of waiting, show an overlay over the reading area (not a separate
  page) with a native spinner, the stage ("Downloading French… 40%", "Translating…", "Preparing the
  voice…") and a Cancel button. Hide it the moment audio starts. Under 500 ms nothing shows (no flash).
  No Lottie for now: it adds an animation library per platform and the stage text is what tells the
  user it has not frozen. A Lottie animation can come later as polish.

#### Design (RTST Batch 29 mock, frames R8 and R9, added 2026-10-26)

Two frames in `docs/design/rtst-phone/RTST Batch 29.dc.html` (R8 line 42, R9 line 133), both the Main page behind the overlay. The mock agrees with the plan except for the points below. The text box, the two source buttons and the greyed player are the real page; only the overlay is new.

- **Overlay (R8, R9).** Covers the text box only (the white 16px-radius box), not a new page; "Text from pictures", "Text from a file" and the player (opacity .5) stay visible. Fill `rgba(255,255,255,.9)`, content centred in a column, gap 14, padding 24. The text behind is dimmed to `rgba(0,0,0,.45)`.
- **Spinner.** 44px, ring `rgba(0,0,0,.10)`, arc `#5B57E8`, stroke 2.6, one turn per .9 s. The note calls it the phone's own spinner, so a native `ActivityIndicator` coloured `#5B57E8` is fine; the SVG is an illustration.
- **Stage line.** 18px, 600. R8 "Downloading French… 40%"; R9 "Preparing the voice…". Other stages: "Translating…" and a mid-read stall.
- **Progress bar (R8 only).** 200x8, radius 4, track `rgba(0,0,0,.10)`, fill `#5B57E8` (40% in the frame). Shown only when ML Kit reports progress; otherwise no bar. Under it a hint, 13.5px, `rgba(0,0,0,.58)`, max 240: "Only the first time. Translation happens on this phone."
- **Cancel.** 48px high, radius 14, padding 0x28, fill `rgba(91,87,232,.10)`, text `#4A46D6` 16px 600, 4px extra top margin. Stops the work and shows the original text.
- **Timing.** Shows 500 ms after the wait begins, goes away the moment audio starts, nothing under 500 ms. On mobile data the download starts at the tap (Decision 147). TalkBack and VoiceOver read the stage. The copy is a draft.

**Open point: R8 replaces the R7 progress card.** The plan says only to replace `BusyIndicator` and `PlayLoadingIndicator`; the mock also drops the R7 progress card. **Default:** remove the R7 card when the overlay lands.

**Open point: Cancel on a mid-read stall.** The plan says Cancel stops the work and returns to the text for every stage. The RTST mock lists the mid-read stall as "the same layout" and so keeps Cancel (the Dino mock removes it for stalls, but that is Dino). **Default:** follow R9: Cancel on RTST stalls too, and it stops playback.

**New strings (all RTST locales):** "Downloading <language>… N%", "Translating…", "Preparing the voice…", the download hint, "Cancel" (if not present).

## Work

- [x] `WaitStage` enum and a pure "show after 500 ms, hide on first audio or cancel" timer model in
      Application, unit-tested.
- [ ] (partly: download, translate, preparing the voice, file; mid-read stall NOT wired, needs a reader signal outside Touches) Wire the stages: ML Kit model download (with progress where ML Kit gives it, else
      indeterminate), translate, synthesis of the first chunk, and mid-read stalls when the next chunk
      is not ready. Cancel stops the work and returns to the text.
- [x] Check the ReviewPage "Translate and read" path never blocks the UI thread (the grounding pass
      did not read it); fix it if it does.
- [x] Replace the separate `BusyIndicator` / `PlayLoadingIndicator` uses that this overlay covers.
- [ ] Usage: `wait_shown` (stage, duration bucket) and `wait_cancelled` (stage), added to the
      Slice 137 closed set, the Worker `EVENT_RULES` and the privacy texts.
- [x] Strings in every RTST locale.

## Deferred to a follow-up slice (with Dino's stall and wait_shown from Slice 143)

- Usage counts `wait_shown` / `wait_cancelled`: need `worker/events.ts` (`RTST_EVENT_RULES`), `PRIVACY.md`, `site/privacy.html`, which Slice 140 owns now.
- Mid-read stall wait: `ISpeechReader` has no "next chunk not ready" signal.
- Dimming the text behind the veil (rgba(0,0,0,.45)) is skipped: the .9 veil already hides it.

## Verification

- Unit tests of the timer model.
- Emulator: first translation to a new language shows "Downloading…" then "Translating…", Cancel
  works; a short text that starts within 500 ms shows nothing; TalkBack announces the stage.

## Changed while building

- Reuses Slice 143's `Mobile.Shared/Waiting` overlay with an RTST style (`ReaderPage.Wait.cs`, `WaitThemeFollower`, `ThemeColors.cs`); `WaitOverlayStyle` gained Cancel radius and padding fields (Dino defaults unchanged) and `WaitController` gained `ShownChanged`.
- Main page: voice-prep wait with Cancel and the player greyed. Review page: the download/translate overlay replaces the R7 card; an abandoned call is ignored after Cancel.
- File read shows the wait without Cancel (the extractor takes no token).
- ML Kit reports no percentage, so there is no progress bar.
- Dark-mode speed chips made readable.
- 4 `Wait_` strings in 14 resx files (drafts).

## From review

- 138-high: 6 findings applied.
- 138-medium: 8 applied, 8 skipped.

#### For CLAUDE.md

- RTST waits use `Mobile.Shared/Waiting` through `ReaderPage.Wait.cs`; colours live in `Colors.xaml` / `ThemeColors.cs`, no hex literals. Goes in `src/ReadTheStupidText.Mobile/CLAUDE.md`. (Slice 138: docs/plan/slices/138-rtst-phone-wait-indicator.md)

#### Not yet verified

- Emulator (AVD RTST_Test, 2026-10-26): PASS for the voice overlay + Cancel + greyed player, translate download/translating overlays + Cancel + retry, file read overlay (none under 500 ms), dark mode and a theme switch with the overlay up. Not seen: a sub-500 ms play start, an ML Kit progress %. Found in passing (pre-existing): a 36 MB text file crashes ReviewPage with OutOfMemoryError in `EditText.setText` (follow-up).
- Native-speaker read of the 4 `Wait_` strings in 14 locales.
- iOS.
