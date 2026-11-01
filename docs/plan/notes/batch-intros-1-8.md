# Batch introductions (Batches 2-8), moved from plan.md

**Batch 2 — release-readiness (Slices 11–16).** With the feature set complete,
this batch polishes voices + UI, then makes the project releasable: license,
automatic versioning, a code-review pass, and the Store-pipeline wiring, ending
in the **first auto-versioned release**. The version follows Conventional
Commits (default patch) and **stays `0.x`** — it is **not** forced to `v1.0.0`;
the app reaches `1.0.0` only when the user declares it stable. Ordered
smallest-first; each is independently shippable.

**Batch 3 — read-latency reduction + local diagnostics.** Addresses the user
report that "from selecting to reading takes a lot, and sometimes it feels like
it isn't picking up the text, then suddenly reads." Root-caused to three delays
in the read pipeline (Decisions 24–26): a cold neural-engine build on first
read, a flat 500 ms settle delay on every read, and a whole-first-paragraph
first chunk — plus a way to *measure* the improvement that keeps the "we collect
nothing" policy literally true. Ordered smallest-first; each is independently
shippable. (No GitHub issues yet — create via `plan-to-issues` if wanted.)

**Batch 4 — diagnostic logs, text sanitizing, and read-control fixes.** Five
user requests: (1) two daily on-disk log files + an "open logs" button, (2)
redact/simplify noise (URLs, passwords, …) before reading, (3) a draggable
control panel, (4) make the new logs explain the ~7 s paragraph latency (plus
low-risk tuning), and (5) make a mid-read voice change actually take effect.
Ordered smallest/most-foundational first — the sanitizer (Slice 20) ships
listening value on its own *and* is the prerequisite for "logs store redacted
text," so it leads; logging (Slice 21) then unblocks the latency analysis (Slice
22). (No GitHub issues yet — create via `plan-to-issues` if wanted.)

**Batch 6 — Android app (MAUI): type-to-read, camera OCR, file upload, neural
voice.** Expands the app beyond Windows, starting with Android (Decisions
37-42). Domain and Application are reused unchanged; only new platform
implementations are written. Ordered smallest-first, each independently
runnable/installable — the same vertical-slice principle as every earlier
batch. iOS/Mac are deliberately not slices here (Decision 37) — they're future
batches on the same MAUI codebase once there's an Apple developer account.

**Batch 7 — Read-along text box, disk-backed audio, timer bug fix (Windows).**
Addresses three user-reported problems: (1) no visual read-along display, (2)
synthesized audio held fully in memory instead of spilling to disk on large
files, (3) the elapsed/total read-through timer getting stuck. Ordered
smallest-first: the standalone bug fix, then the (also standalone) memory
fix, then the text box in two passes — a minimal window that proves the
highlight/toggle wiring, then zoom + real pagination on top of it.

**Batch 8 — CI within the 500 MB Actions storage quota (CI only).** On
2026-09-26/27 the account's shared Actions+Packages storage quota (500 MB)
was exhausted: a single CI run uploaded ~1.07 GB of artifacts, and GitHub
bills storage as monthly GB-hours, so deleting artifacts doesn't reset it.
PR #163 (retention + cleanup) reduced but couldn't close the gap. This batch
moves every build output to GitHub Release assets (which don't count toward
the quota) and stops building or publishing platforms a change didn't touch.
Exception to vertical slicing: pure pipeline infrastructure with no app-facing
surface, so slices are ordered by risk. Windows goes first because it
already publishes to a Release; Android follows, then path filtering on top.

Implemented in PR #165 (Slices 39-42 together; plan merged in #164). Two review passes (high, then
medium effort) led to these changes before commit:
- diff against the last release tag (Decision 56);
- carry Android assets forward when signing is absent (Decision 57);
- signed Android builds only when cutting a release, so no discarded signed
  publishes or secret exposure on PRs;
- the lookup shared as `.github/scripts/find-release.sh`, with `.sh` forced to
  LF so the Windows Store job's `bash` can run it;
- `release-publish` deletes a stale draft instead of publishing it over a newer
  release;
- the Android versionCode widened to `major*1000000 + minor*1000 + patch` (the
  old `*10000 + *100` packing collided at patch 100; every new code exceeds
  the old `10012`).

Live checks (Verification, below) run on the first `main` pushes after merge.
