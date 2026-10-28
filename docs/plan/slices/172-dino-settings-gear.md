# Slice 172 — Dino: one gear for all grown-up settings

*In progress (wave 2026-10-28b), draft PR (planned 2026-10-28).* Dino Reader only (Android, iOS and the Windows head).

**Batch:** 37 (Dino settings, 2026-10-28; Decision 167)
**Touches:** `src/DinoReader.App/SettingsPage.cs` (new), `src/DinoReader.App/ReadingSpeedPage.cs` (new), `src/DinoReader.App/AnimalSoundsPage.cs` (new), `src/DinoReader.App/UsageCountsPage.cs` (new), `src/DinoReader.App/AboutPage.cs` (new), `src/DinoReader.App/GalleryPage.xaml`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/LanguagePage.xaml`, `src/DinoReader.App/LanguagePage.xaml.cs`, `src/DinoReader.App/NarratorPage.cs`, `src/DinoReader.App/SettingsRows.cs`, `src/DinoReader.App/UpdatePage.cs` (`VersionCard` host only), `src/DinoReader.App/GrownUpGate.cs`, `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.App/Resources/Strings/AppResources.cs`, `src/DinoReader.App/Resources/Images/**` (gear icon), `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`

## Why

The user, 2026-10-28: the parents' settings are hidden. Today they only show after the shelf is switched
into grown-up mode, and most of them sit under the **Language** pill: `LanguagePage.xaml.cs:111-129` also
holds the reading speed, animal sounds, usage counts and version cards, none of which is about language.
Narrator and Backup are separate shelf buttons (`GalleryPage.xaml:125-165`). Parents should find one
clear **gear**, and each setting should have its own place. "Free stories" also has to go, because
in-app payments are coming later.

## Decisions locked with the user (Decision 167)

1. **A gear icon, always visible** in the shelf's top corner, in kids' mode too. Tapping it runs the
   existing `GrownUpGate` (hold, then a sum) and then opens a new **Settings** page. Grown-up mode stays,
   but only for adding and editing books.
2. **Settings is a list of rows, each opening its own sub-page from day 1:** Language, Narrator voice,
   Reading speed, Animal sounds, Usage counts, Backup, About & updates (the version card and the
   replay-intro row from Slice 155). Every row that isn't about language leaves `LanguagePage`, which
   keeps only the language list.
3. **The store stays outside Settings**: a shelf button, grown-up mode only (so behind the gate, which
   paid stories will need later).
4. **"Free stories" becomes "More stories"** (`Store_Title`) in all 14 languages. Also reword
   `Store_OfflineBody` ("The free stories need the internet") and `Intro_StoreLine` ("The free store
   has more stories"), dropping "free". `Store_Footer` ("Free, with no account...") stays until payments
   arrive.
5. **The narrator page shows only the current language.** Its title names the language, for example
   "Narrator voice (English)". Saving already works per language (`NarratorChoice.cs:11-19`, key
   `dino.narrator.<lang>`), so a pick made for another language stays and comes back when the parent
   switches back. Today `NarratorPage` shows a card per language (`NarratorPage.cs:111-128`); it drops
   to one.

#### Design (Dino Reader Batch 37 mock, frames 79-94, added 2026-10-28)

The mock has 8 frames for this slice (79-85, 87). It mostly agrees with the plan. Icons are inline SVG strokes, not Phosphor. Tokens: ink `#2b2540`, accent `#5B57E8`, muted `#5a5470` / `#8a8399`, divider `#efe9dd`.

- **Gear (79):** 48x48 dp, radius 14, fill `rgba(43,37,64,.07)`, glyph 24 px stroke `#8a8399`. It replaces the add-book icon, top corner, in kids' mode too. Gate, then Settings. Screen reader: "Settings (grown-ups)". Failing the gate leaves the shelf.
- **Settings (80):** title "Settings". A `Books` row on top ("Add, edit, Store"; opens grown-up mode, 85). Group "Reading": Language, Story languages, Narrator voice, Reading speed, Animal sounds. Group "This phone": Backup, Usage counts, About & updates. White cards radius 18, rows min 52 px, label 15 px, value 14.5 px / 600 `#5a5470` on the right, 14 px chevron. Values shown: "English", "English, فارسی", "Story Granny (EN), Mana (FA)", "0.8x", "On", "New version" (tag only when remote config says an update is ready). Group headings are a proposal. The Story languages row is hidden until Slice 173 ships. The store is never a row. Back lands on the kids' shelf.
- **Language (81):** title "Language", sub-line "Settings, the store and the shelf" (draft). Only the 14 names, each in its own script, scrolling.
- **Narrator voice (82a, 82b):** 82a is a middle page, one row per story language ("English | Story Granny", "فارسی | Mana"), intro "Each story language has its own narrator." With one story language, Settings skips 82a and opens 82b. 82b title "Narrator voice (English)", intro "Who reads the parts between the characters, in every English book." Cards: Story Granny "Warm and slow", Story Grandpa "Deep and calm", Bedtime Mum "Soft and gentle", Cheerful Dad "Bright and lively" (placeholder names; use the real roster).
- **Reading speed (83):** the existing `ReadingSpeedCard` ("Reading speed", "Slower is easier for little ones", "0.8x", 44 px buttons, end label "0.5x · slow"). Animal sounds and Usage counts pages hold their cards the same way. Saves at once, no Save button.
- **About & updates (84):** `VersionCard` ("Dino Book Reader", "Version 1.44.0", update box "A new version is ready (1.45.0)" with "Update", only when newer) and the row "Show the intro again", which now ends back on this page.
- **Grown-up mode (85):** header "Grown-ups", sub-line "Add and edit books", "Done". Buttons "Add from your own books" and "Store". Book options use the existing sheet. Done goes to the kids' shelf.

**Open point: store title.** Plan: `Store_Title` becomes "More stories". Mock 85: the page title becomes "Store". **Default:** "Store"; still drop "free" from `Store_OfflineBody` and `Intro_StoreLine`.
**Open point: back from grown-up mode.** Mock 85 note says Android back goes up to Settings; Slice 174 decision 6 and mock 94 say the kids' shelf. **Default:** kids' shelf (Decision 169).
**Open point: rows.** Plan lists seven rows and no Books row. Mock has Books plus eight rows in two groups, and the per-language Narrator middle page (82a). **Default:** follow the mock.
**Open point: language button on the shelf.** Plan removes the Language pill; mock 79 adds a kid's story-language button next to the gear, shown only with two or three story languages (Slice 173). **Default:** follow the mock.

New strings (14 languages): `Settings_Title`, "Books", "Add, edit, Store", group headings "Reading" and "This phone", "New version", "Settings (grown-ups)", the Language sub-line, the 82a and 82b intros, and the row labels.

## Work

- [ ] **First end-to-end piece:** the gear on the shelf, the gate, a `SettingsPage` with the Language and
      Narrator rows opening the existing pages. Works on Android, iOS and Windows.
- [ ] Move Reading speed, Animal sounds and Usage counts off `LanguagePage` onto their own sub-pages,
      reusing `ReadingSpeedCard`, `AnimalSoundsCard` and `UsageCountsCard` as they are.
- [ ] About & updates sub-page: `VersionCard` plus the replay-intro row (moved from `LanguagePage.ShowVersionCard`,
      still refreshing on remote-config change).
- [ ] Backup row opens `BackupPage`. Remove the Narrator and Backup shortcut buttons and the Language pill
      from the shelf; keep the store button (grown-up mode).
- [ ] `NarratorPage`: only the current UI language's card, the title with the language name.
- [ ] Strings: `Settings_Title` and one label per row (14 languages, 13 as drafts for a native read);
      `Store_Title` -> "More stories", `Store_OfflineBody` and `Intro_StoreLine` without "free".
- [ ] The shelf's accessibility labels: the gear reads "Settings" (`SemanticProperties.Description`), 48 dp touch target.
- [ ] Unit test (DinoReader.Tests): the narrator pick for one language survives saving another language's
      pick (pure part of `NarratorChoice`, if not already covered).
- [ ] Update `src/DinoReader.App/CLAUDE.md`: settings live under the gear, one sub-page per setting;
      `LanguagePage` holds only languages; the store is never a settings row.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj` green.
- Android (headless emulator, `device-test`): in kids' mode, gear -> gate -> Settings; every row opens its
  sub-page and Back returns to Settings; the Language page shows only languages; set the narrator for
  English, switch to Svenska, set a different one, switch back: the English pick is still there; the
  shelf shows "More stories" in grown-up mode and no Narrator/Backup buttons; failing the gate leaves the shelf.
- iOS: compile (`-p:IncludeIos=true -p:IncludeWindows=false -p:IncludeAndroid=false`) and the same flow
  on a simulator or TestFlight build.
- Windows head: the gear and Settings page on the desktop build.
- A 13-language native read of the new strings goes to Outstanding work.
