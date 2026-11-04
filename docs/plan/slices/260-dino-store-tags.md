# Slice 260 — Dino: tags on store books (Hot, Popular, Recommended, New)

*Planned 2026-11-04.* Dino Reader only; Android and iOS alike (the Windows head shares the pages). First of the onboarding group (260-265); 261's first-run book picker shows these tags.

**Touches:** `content/dino/*/book.json`, `tools/DinoReader.BookBuilder/**`, `worker/stories.ts`, `worker/stories.test.mjs`, `src/DinoReader.Core/StoreCatalog.cs`, `src/DinoReader.Core/StoreTag*.cs` (new), `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/StoreTagBadge.cs` (new), `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `tests/DinoReader.Tests/**`, `.maestro/dino/store-tags.yaml` (new), `docs/plan/slices/260-dino-store-tags.md`.

## Decisions (user, 2026-11-04, RefineScope)

- The user sets tags by hand in each book's `book.json`, from a fixed list: **Hot, Popular, Recommended, New**. No free text, nothing computed from usage counts.
- A tag shows as a small coloured badge on the book's card, in the store and in the first-run book picker (Slice 261).
- Tagged books come first; inside that, the existing order (newest `added` first, then title).
- The four labels are translated into all 15 languages, like every other string (Slice 159: every word in the app language).
- A book may have more than one tag; the card shows at most two badges, in the list order above.

## Work

- [ ] Core: `StoreTag` enum (closed set) and the rule "tagged first, then the existing order", with unit tests.
- [ ] `book.json`: optional `tags` array; BookBuilder validates it against the four names (an unknown name fails `check`), copies it into `meta.json`. Not part of `BookHash`, so changing a tag never bumps a book's version (same as `credits`, Slice 192).
- [ ] Worker: `StoryMeta` and `ListItem` carry `tags`, unknown values dropped, sorted as above; `stories.test.mjs` covers it.
- [ ] App: `StoreCatalog` DTO and `StoreItem` read `tags`; `StorePage` rows show the badges (`StoreTagBadge`, colours from `DinoTokens`); an older Worker with no `tags` shows no badges.
- [ ] Strings: four labels in all 15 resx files, in the store's own block.
- [ ] Set first tags on a few store books (the user can change them in `book.json` any time; a tag change publishes through `dino-store.yml` only, Slice 239).
- [ ] Android task: store row badges on the emulator. iOS task: the same page compiles and shows badges (shared code; iOS compile in CI).

## Verification

- `dotnet test tests/DinoReader.Tests`, `node --test worker/*.test.mjs`, BookBuilder `check`.
- Emulator flow `store-tags.yaml`: the store lists a tagged book first with its badge, in English and one more language.
