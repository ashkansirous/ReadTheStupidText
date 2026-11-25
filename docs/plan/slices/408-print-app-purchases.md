# Slice 408 — Dino Worker: print-the-damn-book's own print products, a print-access check and its Windows ticket
**Product:** Dino

*Planned.*

**Check 2026-11-25 (plan-cleanup):** nothing of this slice is built: `git grep -i "printProductId|PRINT_PACKAGE_ID|print-access" origin/main -- worker site` finds nothing, and no PR carries the work. The only client, print-the-damn-book Slice 24, is **parked** in that repo's plan ("skipped by the user 2026-11-18; comes back only when the user asks in an interactive session"), so the Worker side has no caller. Verdict: keep open, not started; **Starts after:** print-the-damn-book Slice 24 is un-parked by the user. The Partner Center step under *Human steps* is an owner step that cannot start until the print app is reserved (id unknown), so it is not a wait inside this slice's code work; it is a *Watch* line in plan.md.

**Touches:** `worker/products.ts`, `worker/stories.ts`, `worker/index.ts`, `worker/entitlements/gate.ts`, `worker/entitlements/microsoft.ts`, `worker/entitlements/types.ts`, `worker/stories.test.mjs`, `worker/entitlements.test.mjs`, `worker/CLAUDE.md`, `site/dino/privacy.html`

**Client side:** print-the-damn-book Slice 24 (`docs/plan/slices/24-buy-to-print.md` in that repo) is the Windows print app that calls these routes. It copies the Dino reader's Windows purchase code.

## Why

The user, 2026-11-18, about print-the-damn-book: "make sure they bought the product before they can print the books other than the free books ... the price for the books to print is $0.99". On how: "do ... exactly as we do in the dino reader ... app to get the ... access to the ... store! copy the ... behaviour. create your own ... purchase pattern and products. printing is a different purchase than running it on your local", "no subscription for printing".

Nothing in the Worker today is multi-app:
- `worker/products.ts:8` hardcodes `APP_PACKAGE_ID = "uk.sirous.dinoreader"`.
- `microsoft.ts:14` and the Dino client both use the ticket reference "dino".

## Decisions (user, 2026-11-18)

1. **Print products are their own family.** Each paid book gets the product id `uk.sirous.printthedamnbook.print.<stem with _>`. A print product unlocks the download of that book and the new print check. A Dino reading product (`uk.sirous.dinoreader.book.*`), a bundle or a subscription never passes the print check.
2. **No subscription for printing.** `productsUnlocking` for the print check returns only the book's print product.
3. **Same receipt check, same codes.** The print check reuses `createPaidGate`, the cache and the limiter, and answers with the same codes: 402 for no proof, 403 for not owned, 429 for too many tries and 503 when the store can't be asked. Free books and the switch being off answer 200. Error bodies are status only, except a 400 `{"message"}` (`worker/CLAUDE.md:29`).

## Items
- [ ] `products.ts`: add `PRINT_PACKAGE_ID = "uk.sirous.printthedamnbook"`, `printProductIdFor(stem)` and a `family` argument (`read` or `print`) for `productsUnlocking` and `unlocksBook`.
- [ ] Make the download route accept a print product too, so the print app can fetch a paid book it bought printing for.
- [ ] Have `/api/stories` list `printProductId` next to `productId` for every paid book (`stories.ts:402-457`).
- [ ] Add a new route, `GET /api/stories/<id>/print-access`, that runs the gate with the `print` family and gives 200, 402, 403, 429 or 503. It goes in `paidDenial`'s route list (`stories.ts:680-694`).
- [ ] Windows ticket for the second app: `GET /api/stories/windows-ticket?app=print` uses `TICKET_REFERENCE` "print" (`microsoft.ts:14-15`). A missing or other `app` stays "dino". The collections query (`microsoft.ts:139-144`) passes the reference the key was minted with, which the proof carries as a new third field: `windows:print:<key>`, parsed in `proof.ts`. An old `windows::<key>` proof stays "dino".
- [ ] If the one Entra app (`MS_STORE_CLIENT_ID`) can't be associated with both Partner Center apps, add `MS_STORE_PRINT_*` secrets and choose them by reference. Upload only with `upload-worker-secrets.mjs --apply` (`worker/CLAUDE.md:54`).
- [ ] Log redaction (`microsoft.ts:62`) passes print product ids as well.
- [ ] Tests: a print product unlocks print-access and download; a reading product, bundle or subscription is refused at print-access with 403; a free book and the switch being off give 200; 402, 429 and 503 behave as on download; `?app=print` gives the print reference; both proof forms parse.
- [ ] `site/dino/privacy.html`: one line saying the print app checks a print purchase through this server with the store key only.
- [ ] `worker/CLAUDE.md`: a rule naming the two product families and the print reference.

## Human steps (the user's Partner Center login)
- Associate the Worker's Entra app with the print app under "Product collections and purchases" in Partner Center, https://partner.microsoft.com/dashboard/apps-and-games/overview. The print app's id is not known yet; this line gets its direct link once the app is reserved (print-the-damn-book Slice 24). It can take up to 16 hours to work.

## Verification
- `worker` tests as listed, `npm test` in `worker/`.
- After deploy through the pipeline: `GET /api/stories` shows `printProductId` on paid books. `GET /api/stories/<paid id>/print-access` with no proof gives 402. The same for a free book gives 200.
