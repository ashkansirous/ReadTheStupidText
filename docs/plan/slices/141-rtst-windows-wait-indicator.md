# Slice 141 — RTST Windows: a wait indicator after 500 ms

*Built 2026-10-26 (wave PR #397); packaged desktop run 2026-10-26: flyout, Cancel and focus passed; the panel ring re-check is pending.*

**Batch:** 29 (Decisions 143-148)
**Touches:** `src/ReadTheStupidText.App/WaitFlyout*` (new), `src/ReadTheStupidText.App/ControlPanelWindow.xaml`, `src/ReadTheStupidText.App/ControlPanelWindow*.cs`, `src/ReadTheStupidText.App/Tray*`, `src/ReadTheStupidText.App/Strings/**`, `src/ReadTheStupidText.Application/Reading/WaitStage.cs`, `src/ReadTheStupidText.Application/Reading/ReadAloudService.cs` (public `CancelRead`), `tests/ReadTheStupidText.Tests/**`

## Why

Same as Slice 138: a hotkey or selection read that waits on a translation-pack download, the Marian
translation or the first synthesis gives no sign of life today unless the control panel is open.

## Decisions

- Decision 146 applies: after 500 ms show the stage text, progress where known, and Cancel; hide on
  first audio. Reuse the pure `WaitStage` timer model from Slice 138.
- **Open point, default:** reads started from the hotkey or a selection have no window, so the
  indicator is a small flyout by the tray clock (the existing tray-anchored placement), plus the
  control panel's `ProgressRing` area when the panel is open.

#### Design (RTST Batch 29 mock, frames W1 and W2, added 2026-10-26)

Two frames in `docs/design/rtst-phone/RTST Batch 29.dc.html` (W1 line 150, W2 line 172). The mock agrees with the plan's default for its open point. The taskbar, clock and wallpaper in W1 are illustration.

- **Flyout (W1).** Opens by the tray clock, in the same spot as the control panel: right 16, 60px above the taskbar in the frame. 320 wide, radius 8, fill `rgba(251,251,253,.92)`, 1px `rgba(0,0,0,.10)` border, shadow `0 18px 50px rgba(20,30,60,.30)`, padding 14x16, gap 12. Header: 24px spinner (arc `#3B6FE3`, stroke 2.4), title "Downloading French… 40%" (13px, 600, `rgba(0,0,0,.89)`) and under it "ReadTheStupidText" (12px, `rgba(0,0,0,.56)`). Bar 4px high, radius 2, track `rgba(0,0,0,.10)`, fill `#3B6FE3`; only when the pack download reports progress. Cancel: 32px high, radius 5, padding 0x18, fill `rgba(255,255,255,.72)`, 1px `rgba(0,0,0,.10)` border, 13px.
- **Timing and stages.** Shows after 500 ms, closes when audio starts. Stages: "Downloading <language>…", "Translating…", "Preparing the voice…", and a mid-read stall.
- **Control panel (W2).** When the panel is open there is no flyout. The play button turns into the `ProgressRing` (32px, stroke 2.4, `#3B6FE3`, in a 42x42 box) inside a row: padding 11x12, radius 8, fill `rgba(255,255,255,.6)`, 1px `rgba(0,0,0,.08)` border, gap 12. Beside it the stage (13px, 600, "Translating…") and below it "French to English" (12px, `rgba(0,0,0,.56)`), then Cancel (30px high, radius 5, padding 0x12, 12.5px). The panel's header subtitle also reads "Translating…" (11px).

**Open point: W2 swaps the play button.** The plan says the control panel shows the wait in "the `ProgressRing` area". The mock turns the play button itself into the ring (no separate area). **Default:** follow the mock: the ring replaces the play button while waiting, and the play button returns when audio starts or Cancel is pressed.

**New strings (Windows locales):** the same stage lines as Slice 138, the "<from> to <to>" subtitle, Cancel.

## Work

- [x] Flyout by the tray with stage, progress and Cancel; the control panel shows the same state.
- [x] Stages: translation-pack download, translate, first synthesis (mid-read stall: deferred to Slice 148).
- [ ] `wait_shown` / `wait_cancelled` events from Windows (deferred to Slice 148).

## Verification

- Packaged dev run (`device-test`): a read that waits shows the stage and Cancel stops the read (a pack
  download it triggered may finish in the background); a quick read shows nothing. Hotkey read with a
  slow voice: the foreground window does not change when the flyout appears or hides.

## Built notes (review pass 1)

- The download stage shows only for a download of the read's own pair while the read is still resolving
  its text. A read never waits for a pack (it reads the original at once), so this stage is rare; Cancel
  stops the read, not the download.
- W2: the wait row's ring replaces the play button's own ring while the wait shows, and the header
  subtitle reads the stage, as the mock does (the mock and the plan default agree).
- The flyout window is no-activate and tool-window, and hides instead of closing.

## Deferred to Slice 148

- The mid-read stall stage (the service exposes no stall signal).
- `wait_shown` / `wait_cancelled` counts from Windows (needs `Application/Usage` and `worker/events.ts`).

## Changed while building

- `WaitFlyoutWindow` (no-activate tool window, bottom-right, hides on Alt+F4) and `WaitFlyoutController` reuse `WaitGate` (500 ms). `ControlPanelWindow.Wait.cs` holds the panel side: the ring replaces Play, the subtitle shows the stage, and when the panel is visible the wait moves from the flyout to the panel.
- `ReadWaitStages` is pure logic with tests. "Downloading" shows only when this read waits on its own pair's pack (reads never wait on packs; the original text plays at once).
- `ReadAloudService.CancelRead` swaps the cts and calls `StopIfSuperseded` only when no read is active; `CurrentReadId` is exposed.
- 5 `Wait_` strings in the 14 resw files (drafts).

## From review

- `141-high`: 6 findings applied. `141-medium`: 7 applied, 4 judged not a bug.

#### For CLAUDE.md

- Windows waits go through `WaitFlyoutController` (500 ms `WaitGate`); the flyout (`WaitFlyoutWindow`) must stay no-activate so a hotkey read never steals focus. Rule goes in `src/ReadTheStupidText.App/CLAUDE.md`. (Slice 141: docs/plan/slices/141-rtst-windows-wait-indicator.md)

#### Not yet verified

- Packaged desktop run (x64 dev build, 2026-10-26): PASS for the flyout after ~500 ms with the foreground window unchanged, Cancel then a normal next read, "Translating…" then "Preparing the voice…", and WM_CLOSE on the flyout then another read. After that run the panel was changed to match W2 (the Play button becomes the ring with Cancel): re-check it on the desktop.
- Native-speaker read of the 5 `Wait_` strings in the 14 resw files (drafts).
- One unconfirmed `CoreMessagingXP` `0xc000027b` crash when a hotkey read arrived 0.7 s after a running auto-read (not reproduced in ~15 runs).
