# 2026-10-28 Pack download stall (PR #537)

- Cause: on Android the response body stream ignores the cancellation token, so a stalled read hung forever; separately, the lock screen blocks background network, which starts the stall when the phone locks mid-download.
- Fix: `StallGuardedRead` times out a read that makes no progress so the download retries; Debug-only `PACKDL` log lines show each attempt, stall and resume.
- Phone evidence (Pixel 10 Pro, 2026-10-28): stall, retry, HTTP Range resume, done.
- Follow-ups: Slice 182 (a stopped download resumes by itself) and Slice 183 (downloads keep going when locked or in the background).
