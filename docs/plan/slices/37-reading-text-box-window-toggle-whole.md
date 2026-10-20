# Slice 37 — Reading text box: window, toggle, whole-chunk highlight

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 37 — Reading text box: window, toggle, whole-chunk highlight.**
      (Decisions 45, 46, 50) New `ReadingTextBoxWindow` (a normal, resizable,
      brand-headered `AppWindow`, single instance kept for the app's lifetime),
      opened/closed by a new 4th icon `ToggleButton` in the control panel's
      `CONTROLS` row (`ReadingTextBoxRequested` → `MainWindow` → `Toggle()`).
      Extended the chunk map (Decision 50) via a low-risk, additive
      `SpeechTextChunker.SplitWithRanges` (new `TextChunk(Text, SourceStart,
      SourceEnd)`, `Split` itself untouched) — since the splitter only ever
      decides *where* to break, never dropping/reordering/duplicating a word,
      each chunk's source range is reconstructed by word-count alignment
      against the original text rather than threading offsets through the
      splitting algorithm itself (4 new unit tests). `ISpeechReader` gained
      `ChunkChanged` (text + source span), raised per chunk by
      `SupertonicSpeechReader`/once-per-read by the WinRT fallback, forwarded
      by `CompositeSpeechReader`, and passed through `ReadAloudService`
      alongside a new `ReadTextChanged` (the full read's text, raised once
      before synthesis starts) — the text box's two inputs. The window shows
      the full read text (`TextBlock` + `TextHighlighters`, confirmed via
      context7) with the current chunk's span highlighted and an approximate
      scroll-to-chunk (plain scrollable text for this pass — no pagination or
      zoom yet, those land in Slice 38). The system ✕ hides rather than closes
      (`AppWindow.Closing` cancelled) so playback keeps going and reopening
      re-syncs instantly, since the single instance never stopped tracking the
      read's events while hidden; `Shutdown()` lifts that guard for app quit.
      The interface addition required a mechanical (no new behavior) touch to
      the Mobile/Android readers to keep them compiling — verified via
      `dotnet build -f net10.0-android`.

## Verification (moved from plan.md)

- **Slice 37:** toggle the new 4th `CONTROLS` icon on → the text box opens;
  during a read, it shows the current chunk's text with it highlighted, and
  updates as playback moves to the next chunk. Close the window → playback is
  unaffected; reopen it → it re-syncs to whatever chunk is currently playing.
