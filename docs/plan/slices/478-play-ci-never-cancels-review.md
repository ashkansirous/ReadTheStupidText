### Slice 478 — CI uploads to Play's internal track never cancel a submission in review
**Product:** Shared

**Touches:** `.github/workflows/release-dino.yml` (Play step, about line 334), `.github/workflows/release-rtst.yml` (Play step, about line 336), `.github/CLAUDE.md`, a new `.github/scripts/play-upload.mjs` + test only if the action cannot do it

**Why.** On 2026-11-28 the owner lost a Dino Play submission that had waited 3 days in review: "they are never to publish the code to production automatically! it should only be allowed to do by my direct request!" The direct cause was a night agent's Play edit commit at 02:01 UTC; the agent rule is fixed in ClaudeSkills (`home/rules/store-publishing.md`), with a hook planned there as ClaudeSkills Slice 43.

Both release workflows have the same risk. Every app-code merge commits a Play edit through `r0adkll/upload-google-play@v1.1.5` (`tracks: internal`, `status: completed`); runs at 2026-11-27 22:08 and 2026-11-28 02:27 UTC did. Google's `edits.commit` docs (<https://developers.google.com/android-publisher/api-ref/rest/v3/edits/commit>, read 2026-11-28):
- The default `changesInReviewBehavior` is `CANCEL_IN_REVIEW_AND_SUBMIT`: "If there are changes already in review, then this will cancel that review first."
- `ERROR_IF_IN_REVIEW` returns 400 `CHANGES_ALREADY_IN_REVIEW` instead, and "won't invalidate the edit".

The docs do not say whether an internal-only edit counts as "changes". So treat every CI commit as able to cancel a review.

Managed publishing does not cover internal tracks: "Internal test tracks are not covered by Managed publishing" (<https://support.google.com/googleplay/android-developer/answer/9859654>). Turning it on would not stop this.

**Decisions.**
- Owner (2026-11-28): nothing cancels or replaces a store submission unless the owner asks for it.
- Decided by this plan:
  - The CI upload commits with `changesInReviewBehavior=ERROR_IF_IN_REVIEW`.
  - If a review is pending, the internal release is left out for that run, and the job writes a notice: "Play internal upload skipped: a submission is in review." It does not fail the release, and it never cancels the review.
  - The next release run after the review ends uploads the newest build as usual.
  - No retry loop, and no `changesNotSentForReview` (the docs tie it to a rejection only).

**Work.**
- [ ] 1. Check through context7 or library-docs whether `r0adkll/upload-google-play` (the newest version) exposes `changesInReviewBehavior`. If it does, set it to `ERROR_IF_IN_REVIEW` in both workflows. If it doesn't, replace the step with a small `play-upload.mjs`: googleapis `edits.insert`, `bundles.upload`, `tracks.update` internal, then `edits.commit?changesInReviewBehavior=ERROR_IF_IN_REVIEW`. On `CHANGES_ALREADY_IN_REVIEW` it deletes the edit and writes the notice. Unit-test the error branch with a mocked client.
- [ ] 2. Both apps (Dino and RTST) get the same step.
- [ ] 3. `.github/CLAUDE.md`: one line on the rule and the skip notice.

**Not yet verified.** A live run while a submission is in review shows the notice and leaves the review in place (Publishing overview). The workflow is CI-only, so the check runs on the first release after a submission.

**Verification.** The unit test for the skip branch is green, and the next release run after merge uploads to internal as before.
