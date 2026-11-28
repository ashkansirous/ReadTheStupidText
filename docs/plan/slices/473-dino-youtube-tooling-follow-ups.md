# Slice 473: Dino YouTube tooling follow-ups (from Slice 435 reviews)

**Product:** Dino

*Planned 2026-11-28 by plan-cleanup, not started.*

**Touches:** `tools/dino/promo/youtube/**`, `.github/workflows/` (one test step)

Why: low follow-ups left over from Slice 435's reviews; not its own work.

- [ ] 1. A CI step running `npm ci && node --test "*.test.mjs"` for `tools/dino/promo/youtube/**` (and the Python tests of `make_voice.py`).
- [ ] 2. `make_voice.py`: a per-minute 429 with no retry hint waits a default (about 30 s) inside the same RATE_WAITS budget; a retry_after above MAX_WAIT_SECONDS returns the 429 at once; optionally reject a path separator or '..' in `check_lines`.
- [ ] 3. `upload.mjs`: apply a changed title/description/tags/thumbnail to a live video (hash of meta.json and thumbnail.jpg, then videos.update / thumbnails.set); reset the retry counter on forward progress; `assertTokenOutsideRepo` refuses any path under a directory containing `.git`; detect page-text overflow; fix the quota-stop wording.
