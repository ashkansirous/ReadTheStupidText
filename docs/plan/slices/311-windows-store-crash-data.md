# Slice 311 — Crash data for the Windows apps from the Microsoft Store

*In progress (wave 2026-11-07), draft PR.* *Planned 2026-11-07.* Not covered by any slice (round 3 feedback loop of 2026-11-01): crash data for the Windows apps (RTST's WinUI tray app and Dino's Windows head) from the Microsoft Store. The vendor docs read on 2026-11-01 (session 6777932c, entry 453) say Partner Center's Health report shows crashes and hangs and the Store analytics API has a `failurehits` endpoint. Not checked: whether they cover these MSIX/WinUI apps, and the reported 100-install threshold. Moved from plan.md's *Outstanding work*. Related: [Slice 227](227-crash-reports.md) (Android/iOS crash reports), [Slice 243](243-crash-report-gaps.md).

**Touches:** `tools/crashes/**` (new `pull-store.mjs` or `.py` and a test; `pull-crashes.test.mjs` and `stub-server.mjs` if shared), `.github/workflows/store-submit.yml` (only for the secrets it already holds), `STORE.md`, `docs/plan/slices/311-windows-store-crash-data.md`

## Work

- [ ] 1. Read the live docs (`web-research` or `library-docs` with context7 for the Store analytics API) for the Health report and `failurehits`: which app types and install thresholds they cover. Write the answer, with the source, in the slice file. Check Partner Center for RTST (Store ID `9NGT1BN1H92V`) and for Dino for Windows through the user's signed-in Chrome only if the API cannot answer.
- [ ] 2. If it covers these apps and our install counts pass the threshold: a `tools/crashes` script that pulls the failure rows with the Partner Center API credentials (they are GitHub Actions secrets only, not in `D:\secrets`: say so, and use `manage-secrets` if a read-only copy is needed), same output shape as `pull-crashes.mjs`.
- [ ] 3. If it does not cover them (below the threshold, or MSIX/WinUI not included): say which, and decide with the user whether the Windows apps report their own crashes the way mobile does (that would be its own slice). Windows is a desktop head, so the Android/iOS parity rule does not apply.

## Verification

- A written answer with the doc URLs; if built, the script prints rows (or "none, below threshold") for both Windows apps and its test passes against a recorded response.

## User answer (2026-11-07, before the night run)

- **Item 3 decided: plan a new slice.** If the Microsoft Store does not give crash data for the Windows apps, add a
  `planned` slice (next free number) so the Windows apps report their own crashes the way the mobile apps do. No
  question to the user is needed.

## Wave note (coordinator, 2026-11-07)

- The Partner Center credentials exist only as GitHub Actions secrets (`AZURE_AD_APPLICATION_CLIENT_ID`, `AZURE_AD_APPLICATION_SECRET`, `AZURE_AD_TENANT_ID`, `SELLER_ID` in `store-submit.yml`); they cannot be read back. So any script reads them from environment variables and is tested against a recorded response; nothing copies or rotates a secret tonight, and the real pull goes to Not yet verified.
- If the docs show the Store gives no crash data for these apps (MSIX/WinUI not covered, or under the install threshold), item 3 is already decided: plan the next free slice number (315 or later, check `plan.md` on origin/main) as `planned` for the Windows apps to report their own crashes like mobile, and build no script.
