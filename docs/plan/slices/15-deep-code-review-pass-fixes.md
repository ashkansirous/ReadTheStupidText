# Slice 15 — Deep code-review pass + fixes

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 15 — Deep code-review pass + fixes.** ([#61](https://github.com/ashkansirous/ReadTheStupidText/issues/61)) (Item 5) Run
      `/code-review-in-detail` over the full app, triage the findings, and fix
      the confirmed real bugs (each non-trivial fix referenced in the PR). The
      generated `summary-code-review.md` / `detailed-code-review.md` are the
      record. Gates the first release tag.

- [x] **Slice 15b — Unit test suite (from the Slice 15 review).** The review's
      top finding was zero automated tests. Added `tests/ReadTheStupidText.Tests`
      (xUnit v3, net10.0-windows) with 43 tests covering the pure logic:
      `PlaybackRate` (clamp/snap), `SpeechTextChunker` (paragraph→sentence→word
      splitting), `SupertonicVoiceTable` (sid mapping/default), `ActivityLog`
      (ring buffer + events), and the `LocalSettingsStore` legacy-`IsEnabled`
      migration (extracted to a pure `ResolveAutoReadFlag` so it needs no package
      identity). A CI `test` job gates `build`/`release` so a failing test blocks
      the release. Run locally with `dotnet test`.

## Verification (moved from plan.md)

- **Slice 15:** `/code-review-in-detail` produces `summary-code-review.md` +
  `detailed-code-review.md`; confirmed real bugs are fixed (re-review or
  targeted tests pass) and referenced in the PR; the app still builds and runs.
