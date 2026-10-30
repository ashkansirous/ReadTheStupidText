### Slice 192 — Dino: the store credit line in every language, served by the store

*Built (wave 2026-10-30), PR #582.* *Planned 2026-10-29.* Dino Reader (Android, iOS and the Windows head share the store page). Gap found in Slice 159.

**Touches:** `content/dino/*/book.json`, `tools/DinoReader.BookBuilder/Books.cs`, `tools/DinoReader.BookBuilder/StoreBuilder.cs`, `tools/DinoReader.BookBuilder/BookHash.cs`, `worker/stories.ts`, `worker/stories.test.mjs` (or the Worker's existing stories tests), `src/DinoReader.Core/StoreCatalog.cs`, `src/DinoReader.App/StorePage.xaml.cs`, `tests/DinoReader.Tests/**`, `.github/scripts/publish-store-books.mjs`, `.github/scripts/publish-store-books.test.mjs`, `docs/plan/slices/192-dino-store-credits-in-every-language.md`.

#### Why

Every store card's credit line ("Retold traditional tale · free to share", "Retold from Aesop · free to share", ...) shows in English whatever language the app is in. The store already serves each book's title in every language (`titles` in `meta.json`, Slice 159); the credit line gets the same treatment.

#### Decisions (user, 2026-10-29)

- Translate it at the source and serve it, not a map in the app: "When we have everything, we do the work."
- All 14 non-English languages, for every book.

#### Claim notes (2026-10-30, grounded)

- `creditsByLanguage` is new; copy the `Titles` pattern (`StoreCatalog.cs:8`, `worker/stories.ts` `parseTitles` ~:159; `credits` passes at ~:187/:236; builder writes it at `StoreBuilder.cs:95`).
- Ships by itself on merge: Workers Builds deploys the Worker, and `release.yml`'s `publish-store` uploads each stamped book's `meta.json` (standing approval). The Worker reads R2 per request, so the order of the two does not matter.

#### Work

- [x] `book.json` gets `creditsByLanguage` (language code to text) next to `credits`, filled for every book in all 14 languages (machine drafts; Peter Rabbit's Beatrix Potter credit keeps the name as is).
- [x] The builder copies it into `meta.json` as `creditsByLanguage`; `check` enforces all 14 languages. It is listing metadata, so it is NOT in `BookHash` and no book version was bumped (a bump would re-download all 15 books on every device through Slice 181; review finding 1). The four bundled starters are unchanged (their credits reach the app through the store list, not the package).
- [x] `worker/stories.ts` passes it through, trimming each value and capping it at 200 characters like `credits`; tests.
- [x] `StoreCatalog` reads it (like `Titles`); `StorePage` shows the reader's language and falls back to `credits`; unit tests for the fallback.
- [x] `publish-store-books.mjs`: a book at the same version as live gets only its `meta.json` re-uploaded when the built one differs in content from the bucket's (`metaDiffers`, `runMetaRefresh`; never a package). Tests in `publish-store-books.test.mjs`.
- [ ] (post-merge, not done) After merge, check `/api/stories` carries `creditsByLanguage` for every book (the run's step summary lists "refreshed meta.json of ...").

**CI preconditions of the first run on `main`** (all already in `release.yml`'s `publish-store`, no workflow edit): `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID` repo secrets (job `env`, same ones the package upload uses; the script fails without them); wrangler installed by `npm ci`; the R2 bucket `readthestupidtext-downloads` (`r2-wrangler.mjs`); `dotnet run ... BookBuilder -- build` producing `artifacts/store`. The token needs R2 read (new: `r2 object get` of `meta.json`) as well as write. `release.yml` is unchanged, so actionlint (run by `release-scripts.yml`) has nothing new to check; it is not installed locally.

*Changed while building:* `creditsByLanguage` is listing metadata outside `BookHash` (no version bumps, no re-downloads), so `publish-store-books.mjs` refreshes `meta.json` at the same version instead. `check` requires exactly `UiLanguage.Supported` minus English. The Worker's `parseCreditsByLanguage` trims, filters to supported languages, then caps at 200.

*From review:* high pass 3 of 3 applied (redesign away from version bumps); medium pass 4 of 6 applied (3 not a bug, 6 post-merge by design).

*Checks:* Dino 1779 tests, BookBuilder `check`, worker 93 + `.github/scripts` 167 node tests, typecheck, Dino Android and iOS compile.

#### For CLAUDE.md (index pass)

- Root `CLAUDE.md`, Dino store paragraph: add `creditsByLanguage` to the `meta.json` field list (language code to the credit line, all 14 non-English languages for every book, trimmed and capped at 200 like `credits` by `worker/stories.ts`; the store card shows the app language's, else `credits`; `book.json` needs it and `check` enforces it; it is NOT in `BookHash`, so editing it never bumps a version; Slice 192).
- `.github/CLAUDE.md`, `publish-store` paragraph: a book already live at the same version still gets its `meta.json` (only that file) re-uploaded when the built one differs in content from the bucket's, so listing-only data (credit lines) ships without a version bump.

#### Verification

- `dotnet run --project tools/DinoReader.BookBuilder -- check`; `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`; Worker tests.
- Not yet verified (post-merge live check by design): `.maestro/dino/store-credits-language.yaml` asserts German text that exists only after `publish-store` has run on `main`; it fails if run before the merge.
- Live: `GET /api/stories` carries `creditsByLanguage` for every book.
- Emulator in Farsi and German: the store cards show the translated credit. iOS: compile here, TestFlight check.

#### Not yet verified

- First `publish-store` run on `main` refreshes the 15 `meta.json` files (the CI token needs R2 read; unconfirmed).
- `.maestro/dino/store-credits-language.yaml` after that (German store shows "frei zum Teilen").
- Native read of the credit lines.
- iOS TestFlight.
