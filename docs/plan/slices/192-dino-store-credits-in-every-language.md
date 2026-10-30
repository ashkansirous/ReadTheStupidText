### Slice 192 — Dino: the store credit line in every language, served by the store

*In progress (wave 2026-10-30, wave 2), draft PR.* *Planned 2026-10-29.* Dino Reader (Android, iOS and the Windows head share the store page). Gap found in Slice 159.

**Touches:** `content/dino/*/book.json`, `tools/DinoReader.BookBuilder/Books.cs`, `tools/DinoReader.BookBuilder/StoreBuilder.cs`, `tools/DinoReader.BookBuilder/BookHash.cs`, `worker/stories.ts`, `worker/stories.test.mjs` (or the Worker's existing stories tests), `src/DinoReader.Core/StoreCatalog.cs`, `src/DinoReader.App/StorePage.xaml.cs`, `tests/DinoReader.Tests/**`, `CLAUDE.md` (the `meta.json` field list), `docs/plan/slices/192-dino-store-credits-in-every-language.md`.

#### Why

Every store card's credit line ("Retold traditional tale · free to share", "Retold from Aesop · free to share", ...) shows in English whatever language the app is in. The store already serves each book's title in every language (`titles` in `meta.json`, Slice 159); the credit line gets the same treatment.

#### Decisions (user, 2026-10-29)

- Translate it at the source and serve it, not a map in the app: "When we have everything, we do the work."
- All 14 non-English languages, for every book.

#### Claim notes (2026-10-30, grounded)

- `creditsByLanguage` is new; copy the `Titles` pattern (`StoreCatalog.cs:8`, `worker/stories.ts` `parseTitles` ~:159; `credits` passes at ~:187/:236; builder writes it at `StoreBuilder.cs:95`).
- Ships by itself on merge: Workers Builds deploys the Worker, and `release.yml`'s `publish-store` uploads each stamped book's `meta.json` (standing approval). The Worker reads R2 per request, so the order of the two does not matter.

#### Work

- [ ] `book.json` gets `creditsByLanguage` (language code to text) next to `credits`, filled for every book in all 14 languages (machine drafts; Peter Rabbit's Beatrix Potter credit keeps the name as is).
- [ ] The builder copies it into `meta.json` as `creditsByLanguage`; `check` covers it, `stamp` bumps every book so the change reaches the live store.
- [ ] `worker/stories.ts` passes it through, trimming each value and capping it at 200 characters like `credits`; tests.
- [ ] `StoreCatalog` reads it (like `Titles`); `StorePage` shows the reader's language and falls back to `credits`; unit tests for the fallback.
- [ ] After merge, `publish-store` uploads every bumped book; compare `/api/stories` with `main`.

#### Verification

- `dotnet run --project tools/DinoReader.BookBuilder -- check`; `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`; Worker tests.
- Live: `GET /api/stories` carries `creditsByLanguage` for every book.
- Emulator in Farsi and German: the store cards show the translated credit. iOS: compile here, TestFlight check.
