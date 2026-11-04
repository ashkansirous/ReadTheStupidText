# Slice 260 — Dino: tags on store books (Hot, Popular, Recommended, New)

*Built 2026-11-04 (night 2026-11-04-1441, wave 1), merged as PR #798 (aca9748e); tags and per-book sizes are live in /api/stories; fix PR #805 (store crash, AutomationId) open. Planned 2026-11-04.* Dino Reader only; Android and iOS alike (the Windows head shares the pages). First of the onboarding group (260-265); 261's first-run book picker shows these tags.

**Touches:** `content/dino/*/book.json`, `tools/DinoReader.BookBuilder/**`, `worker/stories.ts`, `worker/stories.test.mjs`, `src/DinoReader.Core/StoreCatalog.cs`, `src/DinoReader.Core/StoreTag*.cs` (new), `src/DinoReader.App/StorePage.xaml`, `src/DinoReader.App/StorePage.xaml.cs`, `src/DinoReader.App/StoreTagBadge.cs` (new), `src/DinoReader.App/DinoTokens.cs`, `src/DinoReader.App/App.xaml`, `src/DinoReader.App/Resources/Strings/AppResources*.resx`, `tests/DinoReader.Tests/**`, `.maestro/dino/store-tags.yaml` (new), `src/DinoReader.App/Resources/Strings/AppResources.cs` (typed accessors, hand-edited; added at claim), `src/DinoReader.App/Testing/DinoTestCommands.cs` (only if the flow needs a hook; added at claim), `docs/plan/slices/260-dino-store-tags.md`, `.github/scripts/publish-store-books.mjs`, `.github/scripts/publish-store-books.test.mjs` (added at review).

**Claim design note (coordinator, 2026-11-04, wave 1 of night 2026-11-04-1441):** a code check found no per-book size anywhere on main (`bytes` exists only for audio packs: `StoreCatalog.cs:28`, `worker/stories.ts:96-99`). The user's design-review answer asks for it, so this slice adds it: BookBuilder writes `bytes` (the size of the `story-v<n>.dino` package it writes) into `meta.json`; the Worker's `StoryMeta`/`ListItem` pass it through (absent or not a positive integer: dropped); `StoreCatalog`/`StoreItem` read it as `long?` (null when an older Worker sends none). Like `tags`, it is not part of `BookHash`. No UI shows it in this slice; Slice 261's picker reads it. `StoreTag` is a closed enum; parsing is case-sensitive on the four names, unknown names dropped in the app and Worker, rejected by BookBuilder `check`. Ordering: books with at least one tag first, then today's order inside each group (stable).

## Decisions (user, 2026-11-04, RefineScope)

- The user sets tags by hand in each book's `book.json`, from a fixed list: **Hot, Popular, Recommended, New**. No free text, nothing computed from usage counts.
- A tag shows as a small coloured badge on the book's card, in the store and in the first-run book picker (Slice 261).
- Tagged books come first; inside that, the existing order (newest `added` first, then title).
- The four labels are translated into all 15 languages, like every other string (Slice 159: every word in the app language).
- A book may have more than one tag; the card shows at most two badges, in the list order above.

## Work

- [x] Core: `StoreTag` enum (closed set) and the rule "tagged first, then the existing order", with unit tests (`StoreTag.cs`, `StoreTagTests.cs`).
- [x] `book.json`: optional `tags` array; BookBuilder validates it against the four names (an unknown or wrong-case name, or a repeat, fails `check`), copies it into `meta.json`, with `bytes` (the size of `story-v<n>.dino`). Neither is part of `BookHash`, so changing a tag never bumps a book's version (same as `credits`, Slice 192).
- [x] Worker: `StoryMeta` and `ListItem` carry `tags` (unknown dropped, list order) and `bytes` (positive integer or null), tagged first; `stories.test.mjs` covers it.
- [x] App: `StoreCatalog` DTO and `StoreItem` read `tags` and `bytes` (`long?`); `StorePage` rows show the badges (`StoreTagRow`/`StoreTagBadge`, colours from `DinoTokens`); an older Worker with no `tags` shows no badges. The list is also ordered tagged-first on the phone.
- [x] Strings: four labels (`Store_Tag_*`) in all 15 resx files, in the store's own block.
- [x] Set first tags on a few store books (not the four starters): Lion and Mouse Hot + New, Peter Rabbit Popular, Goldilocks Recommended, The Little Red Hen New. The user can change them in `book.json` any time; a tag change publishes through `dino-store.yml` only, Slice 239.
- [x] Android task: store row badges (built; flow `.maestro/dino/store-tags.yaml` written, not run: it is a post-merge live check, it needs `publish-store` to have put the tags in the live list). iOS task: the same page compiles (`net10.0-ios` built on Windows); iOS compile also in CI.

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
- Emulator run (cache cb7c21502c2a), log `D:/ReadTheStupidText-worktrees/reviews/s260-run/flows.log`: `store-credits-language` PASS; `dino-landscape-store` PASS (badge row empty with the current Worker); `store-tags` EXPECTED-FAIL before merge (no live tags yet); `dino-landscape-store-tablet` FAIL because it ran on the phone AVD (wrong AVD, not this slice).

Changed while building:

- Per-book `bytes` was added to the plan (claim design note): BookBuilder writes the package size into `meta.json`; `publish-store-books.mjs` keeps the live size in its live-state, so a rebuild at the live version lists the live size. Books live before this ships list the rebuilt size until their next version bump.

From review (files `D:/ReadTheStupidText-worktrees/reviews/s260-high.md`, `s260-medium.md`, `s260-check.md`, all findings read):

- High 1 (`bytes` from a package that is not uploaded at the live version): applied (live size kept in live-state). 2 (fixed pill height clips at large font): applied. 5 (slim card layout with the extra badge row): applied. 6 (docs missing Not-yet-verified block): applied (below).
- High 3 (badge text stays in the old language after a language switch): not confirmed, no change; needs a device look.
- High 4 (`StoreRow.Badges` allocates per access, rows rebuild badge views): skipped, rebuild cost judged not worth a cache.
- High 7 (`store-tags.yaml` depends on live production data): skipped, it is a post-merge live check by design.
- Medium 1 (starter entries carry no LiveBytes): applied. 6 (`compareEntries` shared comparator): no change needed, its only caller is `worker/stories.ts:393`.
- Medium 2 (first publish after merge has no live `bytes`), 3 (language change, same as high 3), 4 (slim card layout, same as high 5), 5 (large font, long labels), 7 (flow matches the text "Hot"): skipped.
- Check 1-5 (first-publish size, `liveMeta` read twice, `DinoTokens.Get` needs `Application.Current`, tagged-first rule duplicated in Worker and app, no test for removing all tags): all low, none applied.

**Post-merge fix (2026-11-04, night 2026-11-04-1441 wave 1):** after #798 merged and `publish-store` put the tags live, `store-tags.yaml` found that opening the Store crashed: `InvalidOperationException: AutomationId may only be set one time` (`StoreTagBadge.Refresh` ran once with the default tag in the constructor and again when the binding set the real one; log `D:/ReadTheStupidText-worktrees/reviews/s260-postmerge/run.log`). Fix on `fix/s260-badge-automationid`: the badge is immutable (`new StoreTagBadge(tag)` sets text, colours and AutomationId once; `StoreTagRow.Rebuild` builds fresh badges). Reviews `reviews/s260fix-high.md`, `reviews/s260fix-medium.md` (no blocking findings). Emulator (cache c5cd33a299a9): store-tags PASS with the live tags, store-credits-language PASS, dino-landscape-store PASS (`reviews/s260fix-run/`). The release run for #798 (41882363096) could not be cancelled (auto-mode denial), so a Dino build with the crash may reach internal testers before this fix's release replaces it.

## Not yet verified

- Tablet store flow `dino-landscape-store-tablet` on the tablet AVD (it failed on the phone AVD).
- iOS run (compile only so far).
- Badge look at 130-200% system font size and the slim (sideways) store card with a tagged book (extra badge row vs title and cover centre).
- The 15 locale labels (`Store_Tag_*`) need a native read.
- Badge text after an app-language switch with the store page open (review high 3, not confirmed).

#### For CLAUDE.md (lifted 2026-11-04)

- Store tags are a closed enum (`StoreTag`, four names, case-sensitive) set by hand in each book's `book.json`, and are not part of `BookHash`, so a tag change never bumps a version. "Tagged first" is applied in both `worker/stories.ts` (`compareEntries`) and `StoreCatalog.cs`: keep the two the same. (Slice 260: docs/plan/slices/260-dino-store-tags.md) Place in `src/DinoReader.App/CLAUDE.md` or `src/DinoReader.Core/CLAUDE.md`.
