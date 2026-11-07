# Slice 311 — Crash data for the Windows apps from the Microsoft Store

*Built 2026-11-07 (wave 2026-11-07, night 2026-11-07-1412 wave 5), PR #930, not merged; the real pull is not yet verified.* *Planned 2026-11-07.* Not covered by any slice (round 3 feedback loop of 2026-11-01): crash data for the Windows apps (RTST's WinUI tray app and Dino's Windows head) from the Microsoft Store. The vendor docs read on 2026-11-01 (session 6777932c, entry 453) say Partner Center's Health report shows crashes and hangs and the Store analytics API has a `failurehits` endpoint. Not checked: whether they cover these MSIX/WinUI apps, and the reported 100-install threshold. Moved from plan.md's *Outstanding work*. Related: [Slice 227](227-crash-reports.md) (Android/iOS crash reports), [Slice 243](243-crash-report-gaps.md).

**Touches:** `tools/crashes/**` (new `pull-store.mjs` or `.py` and a test; `pull-crashes.test.mjs` and `stub-server.mjs` if shared), `.github/workflows/store-submit.yml` (only for the secrets it already holds), `STORE.md`, `docs/plan/slices/311-windows-store-crash-data.md`

## Work

- [x] 1. Read the live docs (`web-research` or `library-docs` with context7 for the Store analytics API) for the Health report and `failurehits`: which app types and install thresholds they cover. Write the answer, with the source, in the slice file. Check Partner Center for RTST (Store ID `9NGT1BN1H92V`) and for Dino for Windows through the user's signed-in Chrome only if the API cannot answer.
- [x] 2. If it covers these apps and our install counts pass the threshold: a `tools/crashes` script that pulls the failure rows with the Partner Center API credentials (they are GitHub Actions secrets only, not in `D:\secrets`: say so, and use `manage-secrets` if a read-only copy is needed), same output shape as `pull-crashes.mjs`.
- [x] 3. (Not needed: item 1 found the API documented for these apps and no threshold, so item 2 was built.) If it does not cover them (below the threshold, or MSIX/WinUI not included): say which, and decide with the user whether the Windows apps report their own crashes the way mobile does (that would be its own slice). Windows is a desktop head, so the Android/iOS parity rule does not apply.

## Answer to item 1 (read 2026-11-07)

- **`failurehits` covers Store-distributed packaged apps** ("UWP applications" methods): `GET https://manage.devcenter.microsoft.com/v1.0/my/analytics/failurehits?applicationId=<Store ID>&startDate=mm/dd/yyyy&endDate=...`, event types `crash`, `hang`, `memory`, `jse`; only the last 30 days; paging by `top` (max 10000) and `@nextLink`. The same data is the Failures section of the Health report. The separate "desktop application" methods belong to the Windows Desktop Application Program (unpackaged installers), which does not apply to our Store MSIX apps. The docs never name WinUI 3 or MSIX explicitly, so coverage of our two packages is read from "Store-distributed package", not proven.
- **Install threshold: not stated.** No Microsoft page found says a minimum install count (the "100 installs" in the 2026-11-01 note is unconfirmed). The Health report page only says data can show as `Unknown`, and that symbols in the package are needed to name failures (`!unknown` otherwise).
- Token: Entra client credentials, POST `https://login.microsoftonline.com/<tenant>/oauth2/token` with `resource=https://manage.devcenter.microsoft.com`; the app needs the Manager role in Partner Center (our `AZURE_AD_*` secrets are that app).
- Sources: https://learn.microsoft.com/en-us/windows/uwp/monetize/get-error-reporting-data , https://learn.microsoft.com/en-us/windows/uwp/monetize/access-analytics-data-using-windows-store-services , https://learn.microsoft.com/en-us/windows/apps/publish/health-report (redirects to the Partner Center Health report page), https://learn.microsoft.com/en-us/windows/win32/appxpkg/windows-desktop-application-program . Docs read through web fetch/search; context7 has no Microsoft Store docs entry, so library-docs was not used.
- Decision: the API is documented for our app type and no threshold is documented, so item 2 is built (item 3 is not triggered). If the real pull returns no rows for a Store ID that has users, that is the threshold or coverage case: then plan the self-reporting Windows slice.

## Built (item 2)

- `tools/crashes/pull-store.mjs`: `node tools/crashes/pull-store.mjs [--app dino|rtst|both] [--from] [--to] [--top]`. Credentials only from env (`AZURE_AD_TENANT_ID`, `AZURE_AD_APPLICATION_CLIENT_ID`, `AZURE_AD_APPLICATION_SECRET`; `STORE_PRODUCT_ID`, required, no default; `DINOREADER_STORE_APP_ID`). Output: `=== app: N failure row(s) ===`, then `N x <eventType> | <failureName> | <failureHash>` with versions, markets, devices and newest date; no rows prints `none (...)`. Groups are keyed by `eventType|failureName|failureHash`. `STORE_PRODUCT_ID` is required (no default id); `DINOREADER_STORE_APP_ID` is optional: under `both`, a missing one warns and skips Dino. Every fetch has a 30 s timeout, `@nextLink` is followed only on the API host, and an incomplete pull prints PARTIAL or FAILED and exits 1. No secret was copied or rotated.
- `tools/crashes/pull-store.test.mjs`: node tests against the recorded response from the docs sample (token request, URL, paging and incomplete-read detection, foreign-host nextLink, grouping, printout, env and Store id checks, argument and date-range validation).
- Run all of it with `node --test "tools/crashes/**/*.test.mjs"` (Node 24 reads the bare directory form `node --test tools/crashes/` as a module path and fails with MODULE_NOT_FOUND, before and after this slice).

*Changed while building:* the default Store id was dropped (`STORE_PRODUCT_ID` is required, as `.github/CLAUDE.md` says there is no default id); a missing Dino id skips Dino with a warning instead of aborting; grouping gained `failureHash`; paging, timeouts and exit codes were hardened after review (see below). Tests: `node --test "tools/crashes/**/*.test.mjs"` 29/29.

*From review:*
- High (`reviews/s311-high.md`), findings 1-6 and 10 applied; 9 applied in part (see below):
- Skipped #7 (`reviews/s311-high.md`): "re-implemented from `pull-crashes.mjs`" (a shared module for the argument and helper code was not made).
- Skipped #8 (`reviews/s311-high.md`): "Work items 1 and 2 are still unticked" (the tick and the status line are done here, but `STORE.md` got no line).
- Skipped #9 in part (`reviews/s311-high.md`): "No `.github` workflow step runs" the tools tests, so a CI step for `tools/crashes/*.test.mjs` was not added (the stub-server route for `main` was not built either).
- Medium (`reviews/s311-medium.md`), findings 1-4, 8 and 9 applied:
- Skipped #5 (`reviews/s311-medium.md`): "`groupby` omits `date` but `newest` relies on `row.date`" (unverified against the real API; goes to a real pull).
- Skipped #6 (`reviews/s311-medium.md`): "the \"device-days\" label overstates" (the sum is not treated as a user count).
- Skipped #7 (`reviews/s311-medium.md`): "duplicate `pull-crashes.mjs`" (no shared module).
- Check 1 (`reviews/s311-check1.md`), findings 2-4, 8 and 9 applied:
- Skipped #1 (`reviews/s311-check1.md`): "GROUP_BY lacks `date`" (unverified against a real response).
- Skipped #5 (`reviews/s311-check1.md`): "no guard for --from/--to outside the 30-day window" (the API's own error is shown).
- Skipped #6 (`reviews/s311-check1.md`): "labelled 'device-days' but rows are grouped without date" (not treated as a user count).
- Skipped #7 (`reviews/s311-check1.md`): "`--top=5` throws" (only `--flag value` form is supported).
- Skipped #10 (`reviews/s311-check1.md`): "token fetched once, no retry on 429/5xx" (a failed pull prints FAILED and exits 1).
- Check 2 (`reviews/s311-check2.md`), finding 1 applied, findings 2-6 listed next:
- Skipped #2 (`reviews/s311-check2.md`): "'newest' relies on row.date but groupby omits date" (same open point as above).
- Skipped #3 (`reviews/s311-check2.md`): "Date.parse accepts 2026-02-31" (no round-trip check added).
- Skipped #4 (`reviews/s311-check2.md`): "range not checked against 30-day API limit" (footer shows the requested range).
- Skipped #5 (`reviews/s311-check2.md`): "completeness depends on TotalCount" (a capped page with no `TotalCount` is not flagged).
- Skipped #6 (`reviews/s311-check2.md`): "AZURE_AD_TENANT_ID interpolated unencoded" (same host only).
- Check 3 (`reviews/s311-check3.md`), all five findings low, listed next:
- Skipped #1 (`reviews/s311-check3.md`): "groupRows has no test for the failureHash key branch".
- Skipped #2 (`reviews/s311-check3.md`): "`add` uses a plain `{}`" (local dev tool, no security impact).
- Skipped #3 (`reviews/s311-check3.md`): "informational (speculative, not actionable)" (keying on hash alone).
- Skipped #4 (`reviews/s311-check3.md`): "omits the `| <failureHash>` suffix" (this file's Built section now carries the suffix, so it was fixed here).
- Skipped #5 (`reviews/s311-check3.md`): "response.json() has no handling of a non-JSON body" (no retry on 429 or 5xx).

## Not yet verified

- [ ] A real pull with the Actions secrets exported for both apps (they are not readable on this PC; run it from a workflow or after the user exports them): does it print rows or `none` for RTST `9NGT1BN1H92V` and Dino, and are the failure names symbolised?
- [ ] Whether either app is under an undocumented Store threshold. If a real pull returns no rows for an app with users, plan the self-reporting Windows slice (item 3, already decided).

#### For CLAUDE.md

- `tools/CLAUDE.md`: `tools/crashes/pull-store.mjs` pulls Microsoft Store crash rows for the Windows apps; it reads credentials only from env (`AZURE_AD_TENANT_ID`, `AZURE_AD_APPLICATION_CLIENT_ID`, `AZURE_AD_APPLICATION_SECRET`, required `STORE_PRODUCT_ID`, optional `DINOREADER_STORE_APP_ID`); the secrets exist only as Actions secrets. Test it with `node --test "tools/crashes/**/*.test.mjs"` (the bare-directory form fails on Node 24). (Slice 311: docs/plan/slices/311-windows-store-crash-data.md)

#### For README.md

Nothing user-facing.

## Verification

- A written answer with the doc URLs; if built, the script prints rows (or "none, below threshold") for both Windows apps and its test passes against a recorded response.

## User answer (2026-11-07, before the night run)

- **Item 3 decided: plan a new slice.** If the Microsoft Store does not give crash data for the Windows apps, add a
  `planned` slice (next free number) so the Windows apps report their own crashes the way the mobile apps do. No
  question to the user is needed.

## Wave note (coordinator, 2026-11-07)

- The Partner Center credentials exist only as GitHub Actions secrets (`AZURE_AD_APPLICATION_CLIENT_ID`, `AZURE_AD_APPLICATION_SECRET`, `AZURE_AD_TENANT_ID`, `SELLER_ID` in `store-submit.yml`); they cannot be read back. So any script reads them from environment variables and is tested against a recorded response; nothing copies or rotates a secret tonight, and the real pull goes to Not yet verified.
- If the docs show the Store gives no crash data for these apps (MSIX/WinUI not covered, or under the install threshold), item 3 is already decided: plan the next free slice number (315 or later, check `plan.md` on origin/main) as `planned` for the Windows apps to report their own crashes like mobile, and build no script.
