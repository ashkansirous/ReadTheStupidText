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

## Notes moved from CLAUDE.md (2026-10-21)

The neural reader (`SupertonicSpeechReader`) **chunks** long text
(`SpeechTextChunker`, paragraph→sentence→word) and synthesizes the chunks
**concurrently** while playing them **in order**, so a long read starts
speaking after the first chunk. The **first** chunk is biased toward a single
sentence (Slice 18, Decision 25) so time-to-first-audio is a short synthesis, not a
whole paragraph; later chunks keep the ~200-char paragraph→sentence→word split. A
superseded/stopped read is torn down via a
**generation counter + `CancellationToken`** (`ISpeechReader.Stop()`), so stale
synthesis can never reach the shared `MediaPlayer`; `read` is marked only on the
reader's **`Completed`** (natural end), never on a stop-induced idle.


Auto-read uses an **adaptive settle** (Slice 18, Decision 25) rather than a flat
delay: a lone select (click/double-click) reads after a short **150 ms** baseline,
but while events keep arriving within `BurstGapMs` (a live drag) each one extends
the wait to **500 ms**, so a drag still collapses to one read. The
swapped-`_selectionCts` supersede makes any late extra read harmless. Both auto-read
paths (UIA selection + clipboard copy) share this debounce in `ReadAloudService`.

