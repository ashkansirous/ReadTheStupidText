# Slice 23 — Voice swap continues the current read

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 23 — Voice swap continues the current read.** ([#105](https://github.com/ashkansirous/ReadTheStupidText/issues/105)) (Decision 29) On
      `SetVoice` during an active read, cancel pending synthesis and re-synthesize the
      remaining chunks with the new speaker from the current `_currentChunkIndex`
      (reuse the generation-counter machinery); already-played audio is not repeated.
      Drive it from `ReadAloudService.SetVoice`.
      **Built:** the playback loop was extracted into a shared `SpeakChunksAsync(chunks,
      startIndex, speakerId, …)` that takes the **speaker as a parameter** (not the mutable
      field) so a change can't half-apply to queued chunks. `SetVoice` just records the
      selection (`_speakerId`); the loop switches **at the next chunk boundary** — when the
      current chunk finishes it notices `_speakerId` changed and restarts at the *next*
      chunk in the new voice (`BeginGeneration` cancels in-flight old-voice synthesis). So
      the current chunk finishes in the old voice: nothing already heard is repeated **and
      no unheard text is skipped**, and earlier chunks are never re-synthesized. (Reviewed
      with the user: chosen over resume-at-current-chunk, which replayed the current
      sentence.) A single-chunk read or a change during the last chunk applies to the next
      read. The native-reader logic isn't unit-tested (no engine without package identity),
      per the project's test story; runtime check under the (Package) profile remains.

## Verification (moved from plan.md)

- **Slice 23:** start a long read, then change the voice actor mid-read → already-
  spoken audio is **not** repeated; the remaining text continues in the **new**
  voice. Changing voice while idle still applies to the next read.
