# Slice 18 — Adaptive settle delay + smaller first chunk

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 18 — Adaptive settle delay + smaller first chunk.** ([#85](https://github.com/ashkansirous/ReadTheStupidText/issues/85)) (Decision 25)
      Replace `ReadAloudService`'s flat `SelectionDebounceMs = 500` with a short
      (~150 ms) baseline that extends only while events keep arriving (a live
      drag), so click-selects fire fast and drags still collapse to one read. Bias
      `SpeechTextChunker`'s **first** chunk toward a single sentence so audio
      starts sooner; later chunks unchanged. Both shorten per-read
      time-to-first-audio without changing the concurrent-synthesis model.

## Verification (moved from plan.md)

- **Slice 18:** a quick click/double-click select reads with only a brief pause
  (no half-second wait); a slow drag-select of a sentence still reads **once**
  after the drag settles (no burst). A long multi-paragraph selection starts
  speaking after a short first synthesis (first sentence), not after the whole
  first paragraph. The clipboard-copy path behaves the same.
