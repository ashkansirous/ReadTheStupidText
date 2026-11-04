# Slice 260 — Dino: tags on store books (Hot, Popular, Recommended, New)

*In progress (wave 2026-11-04), draft PR.* *Planned 2026-11-04.* Dino Reader only; Android and iOS alike (the Windows head shares the pages). First of the onboarding group (260-265); 261's first-run book picker shows these tags.

**Touches:** `content/dino/*/book.json`, `tools/DinoReader.BookBuilder/**`, `worker/stories.ts`, `worker/stories.test.mjs`, `src/DinoReader.Core/StoreCatalog.cs`, `src/DinoReader.Core/StoreTag*.cs` (new), `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/StoreTagBadge.cs` (new), `src/DinoReader.App/DinoTokens.cs`, `src/DinoReader.App/App.xaml`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `tests/DinoReader.Tests/**`, `.maestro/dino/store-tags.yaml` (new), `src/DinoReader.App/Resources/Strings/AppResources.cs` (typed accessors, hand-edited; added at claim), `src/DinoReader.App/Testing/DinoTestCommands.cs` (only if the flow needs a hook; added at claim), `docs/plan/slices/260-dino-store-tags.md`.

**Claim design note (coordinator, 2026-11-04, wave 1 of night 2026-11-04-1441):** a code check found no per-book size anywhere on main (`bytes` exists only for audio packs: `StoreCatalog.cs:28`, `worker/stories.ts:96-99`). The user's design-review answer asks for it, so this slice adds it: BookBuilder writes `bytes` (the size of the `story-v<n>.dino` package it writes) into `meta.json`; the Worker's `StoryMeta`/`ListItem` pass it through (absent or not a positive integer: dropped); `StoreCatalog`/`StoreItem` read it as `long?` (null when an older Worker sends none). Like `tags`, it is not part of `BookHash`. No UI shows it in this slice; Slice 261's picker reads it. `StoreTag` is a closed enum; parsing is case-sensitive on the four names, unknown names dropped in the app and Worker, rejected by BookBuilder `check`. Ordering: books with at least one tag first, then today's order inside each group (stable).

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

#### Design (Dino Reader Batch 47 Onboarding mock, frames 161-162, added 2026-11-04)

Source: `docs/design/dino-reader/Dino Reader Batch 47 Onboarding.dc.html`, 2 frames: 161 store rows (line 36, 360x760 phone) and 162 the four badges (line 52, a spec card, not a screen). The mock agrees with the plan on names, order, "at most two" and "no tags shows rows as today". Cover colour blocks with a title are placeholders for books with no art.

- **Badge (`StoreTagBadge`).** Pill height 22 dp, horizontal padding 8 (9 in the Farsi sample), radius 11, text 11.5 bold, no icon, gap 4 between pills, row wraps to a second line before the title shrinks (German "Empfohlen" is the longest). Farsi text runs right to left inside the pill; the row stays put (Slice 96).
- **Colours (fill on text).** Hot `#FFE3D9` on `#B4552F`. Popular `#ECE7FF` (= `BrandTint`) on `#3f3bc4`. Recommended `#DFF5E3` on `#2E7D32`. New `#FFF0C2` on `#8A5A00`. Only `BrandTint` is a `DinoTokens` key today; the other seven are new keys (`App.xaml` + `DinoTokens.cs`).
- **Store row (161, `StorePage.xaml`).** Card white, radius 18, padding 12, gap 12. Cover 66 wide, 3:4, radius 10. Text column gap 4, top to bottom: badge row (only when tagged), title 15.5/600 line 1.25, one grey line 12.5 `Body` joining credit and length ("Aesop · 8 pages · 4 min"). Right side: "Get" button 44 high, radius 22, `Brand` fill, white 14/600, padding 0/14; an installed book shows a green tick and "On the shelf" 13.5/600 `#2E7D32` instead. Page background `Sand`; header "Store" 20/600 with the sub-line "Picked and cast for Dino" 12 `Muted` and a "Done" pill (44 high, `Brand`). Order on screen: tagged first (Lion and Mouse: Hot, New; Peter Rabbit: Popular; Gingerbread Man: Recommended), then untagged.
- **Files.** `StoreTagBadge.cs`, `StorePage.xaml(.cs)`, `AppResources*.resx`; also `App.xaml` and `DinoTokens.cs` for the new colour keys (not in the Touches line, add them).
- **New strings (15 locales).** `Hot`, `Popular`, `Recommended`, `New` (mock drafts: German "Angesagt", "Empfohlen"; Farsi "داغ", "تازه"; the mock marks them drafts for the 15-language pass).

**Open point: badge colours are not tokens.** Slice says "colours from `DinoTokens`"; seven of the eight colours are not keys. **Default:** add them as new `DinoTokens` keys, light only.
**Open point: Hot is coral and loudest.** The mock asks (question 1) whether Recommended should lead. **Default:** keep the slice's list order Hot, Popular, Recommended, New, as drawn.
**Mock decides (slice silent):** badges sit above the title in the text column; credit and length are one line; German wraps before the title shrinks.

**User, 2026-11-04 (design review):** keep Hot (coral) as the loudest badge, as drawn; the list order stays Hot, Popular, Recommended, New. Seven of the mock's eight tag colours are new `DinoTokens` keys (only `BrandTint` exists): add them in `src/DinoReader.App/DinoTokens.cs` and `src/DinoReader.App/App.xaml` (added to Touches). The store list also carries each book's package size (`bytes`, from BookBuilder into `meta.json`, Worker, `StoreCatalog`), which Slice 261's picker shows as "2 picked · 9 MB".

## Verification

- `dotnet test tests/DinoReader.Tests`, `node --test worker/*.test.mjs`, BookBuilder `check`.
- Emulator flow `store-tags.yaml`: the store lists a tagged book first with its badge, in English and one more language.
