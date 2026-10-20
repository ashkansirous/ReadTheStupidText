# Slice 38 — Reading text box: zoom + fit-to-box pagination

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 38 — Reading text box: zoom + fit-to-box pagination.** (Decisions
      47, 48) New pure `TextBoxPaginator.Paginate(text, fits)` (Application) —
      a greedy, sentence-boundary-respecting fill that takes a caller-supplied
      `fits` predicate, so its "own segmentation, independent of
      `SpeechTextChunker`" splitting/greedy-fill logic is fully unit-tested (7
      cases, incl. the never-drop-or-split-a-sentence and single-oversized-
      sentence-gets-its-own-page rules) without needing real text measurement;
      `PageIndexContaining` finds which page a chunk's source position falls
      in. `ReadingTextBoxWindow` supplies the real `fits` via an off-screen
      `MeasureProbe` `TextBlock.Measure()` against the box's current
      (padding-adjusted) width/height — repaginating on text change, zoom, and
      `ScrollViewer.SizeChanged`, always restoring the currently-playing
      chunk's page (or page 1) so a zoom/resize never loses the reader's
      place. Replaced the Slice 37 whole-text scrollable view; the
      `ScrollViewer` stays only as a safety net for a single sentence too long
      to fit a page. New toolbar row with +/- zoom buttons (Segoe Fluent
      glyphs) and a page indicator; zoom-in is clamped to
      `min(32px, the size at which a fixed 30-word stand-in sentence stops
      fitting the box)`, zoom-out to a 10px floor — both via the same
      `MeasureProbe`, no separate Domain value object (this is a
      single-window WinUI display concern, not a cross-platform one, unlike
      `PlaybackRate`).

## Verification (moved from plan.md)

- **Slice 38:** with the text box open, click +/- → text zooms in/out live;
  zooming in stops right where a 30-word sentence would no longer fit at the
  current box width; a long paragraph is shown one fit-to-box page at a time,
  auto-advancing as playback crosses into the next page's chunk.
