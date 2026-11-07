# Slice 311 — Crash data for the Windows apps from the Microsoft Store

*Planned 2026-11-07.* Not covered by any slice (round 3 feedback loop of 2026-11-01): crash data for the Windows apps (RTST's WinUI tray app and Dino's Windows head) from the Microsoft Store. The vendor docs read on 2026-11-01 (session 6777932c, entry 453) say Partner Center's Health report shows crashes and hangs and the Store analytics API has a `failurehits` endpoint. Not checked: whether they cover these MSIX/WinUI apps, and the reported 100-install threshold. Moved from plan.md's *Outstanding work*. Related: [Slice 227](227-crash-reports.md) (Android/iOS crash reports), [Slice 243](243-crash-report-gaps.md).

**Touches:** `tools/crashes/**` (new `pull-store.mjs` or `.py` and a test), `.github/workflows/store-submit.yml` (only for the secrets it already holds), `STORE.md`, `docs/plan/slices/311-windows-store-crash-data.md`

## Work

- [ ] 1. Read the live docs (`web-research` or `library-docs` with context7 for the Store analytics API) for the Health report and `failurehits`: which app types and install thresholds they cover. Write the answer, with the source, in the slice file. Check Partner Center for RTST (Store ID `9NGT1BN1H92V`) and for Dino for Windows through the user's signed-in Chrome only if the API cannot answer.
- [ ] 2. If it covers these apps and our install counts pass the threshold: a `tools/crashes` script that pulls the failure rows with the Partner Center API credentials (they are GitHub Actions secrets only, not in `D:\secrets`: say so, and use `manage-secrets` if a read-only copy is needed), same output shape as `pull-crashes.mjs`.
- [ ] 3. If it does not cover them (below the threshold, or MSIX/WinUI not included): say which, and decide with the user whether the Windows apps report their own crashes the way mobile does (that would be its own slice). Windows is a desktop head, so the Android/iOS parity rule does not apply.

## Verification

- A written answer with the doc URLs; if built, the script prints rows (or "none, below threshold") for both Windows apps and its test passes against a recorded response.
