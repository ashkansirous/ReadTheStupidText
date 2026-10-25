# Slice 115 — Dino: storyteller voices by default

*In progress (wave 2026-10-25), draft PR.* *Planned 2026-10-25*, from the user's report that Dino "is stuck to the old voices": the cast still
shows Demiurge and Momonga after the storyteller pack is downloaded, and "Use Dino's first voices"
deletes the pack so the offer comes back on every visit. Dino Reader only; RTST is not touched.

**Batch:** 24 (storyteller characters)
**Depends on:** Slice 114 (the `kokoro-en` pack and `StorytellerController`)
**Touches:** `src/DinoReader.App/StorytellerController.cs`, `src/DinoReader.App/CastStoryteller.cs`, `src/DinoReader.App/CastPage.cs`, `src/DinoReader.App/CastText.cs`, `src/DinoReader.App/NarratorRecast.cs`, `src/DinoReader.App/NarratorPage.cs`, `src/DinoReader.App/StoryVoicing.cs`, `src/DinoReader.App/SpeechStartup.cs`, `src/DinoReader.App/GalleryPage.xaml.cs`, `src/DinoReader.App/MauiProgram.cs`, `src/DinoReader.App/Resources/Strings/*.resx`, `src/DinoReader.Core/CastEditor.cs`, `src/ReadTheStupidText.Mobile.Shared/MobileVoicePackService.cs`, `tests/DinoReader.Tests/**`, `src/DinoReader.App/CLAUDE.md`

## Why

Grounded on `origin/main` 2026-10-25:

- The re-cast to Kokoro (`NarratorRecast.TryRecastAsync`) runs only when the cast or narrator screen
  opens (`CastPage.cs:383`, `NarratorPage.cs:207`), never when the download finishes or a book is read.
  A book that wasn't reopened on the cast screen keeps its Supertonic ids.
- Hand-picked (`Edited`) Supertonic voices stay Supertonic ids by design (`CastEditor` `StaysPicked`) and
  are mapped to a Kokoro voice only at read time. The name shown depends on a static flag in `CastText`
  that is stale until the pool is rebuilt, so the old names (Demiurge, Momonga) can still show.
- "Use Dino's first voices" (`StorytellerController.UseFirstVoices`) uninstalls `kokoro-en`, so the
  offer card shows again on every visit.

## Decisions (2026-10-25, agreed with the user)

- **Decision 120: the storyteller pack is Dino's default voice, downloaded by itself.** On first
  launch (and on any launch where it is missing) Dino downloads the storyteller pack over Wi-Fi in the
  background, with a small "Getting the storyteller voices…" line in the grown-up gallery and one
  "Storyteller voices are ready" sheet (joined with the re-cast note, see Design) when done. Until then books read with the current voices.
  On mobile data it waits for Wi-Fi; a grown-up can tap "Download now" to use mobile data.
- **Decision 121: one re-cast of every book when the pack lands, hand-picked voices included.** When
  the install finishes, every book's cast in a language the pack serves is re-cast once (Edited picks
  too; genders and flavours kept), and Dino shows "Your stories now use the new storyteller voices"
  once. This replaces Slice 114's rule that Edited Supertonic picks stay.
- The offer card, "Hear one" and "Use Dino's first voices" are removed from the cast screen. There is
  no way to go back to the old voices in a language the storyteller pack serves.

#### Design (Dino Reader Batch 24 mock, frames 50-51, added 2026-10-25)

Source: `docs/design/dino-reader/Dino Reader Batch 24.dc.html`, section "Storyteller voices by default" (frames 50 and 51). Two real-UI frames (360x760 phone, 6px `#2b2540` border, radius 34), each with a caption. The mock agrees with the plan on automatic download, Wi-Fi only and the single re-cast. It disagrees on the two one-time notes. The tile action bars are left out of both frames (a cut-down gallery, not UI to build from). Icons are inline SVG strokes (2.2-2.8 wide), not Phosphor names; the app uses its own `IconKind` set. It supersedes frames 48-49 of the Batch 22-23 mock: the offer card, "Hear one" and "Use Dino's first voices" are gone.

- **Status line (115-2, frame 50).** Grown-up gallery only, under the 3-button row (Free stories, Narrator, Backup; 64 high, radius 16). Padding `0 18px 12px`. A row: download glyph 16 (`#5B57E8`, stroke 2.4), text "Getting the storyteller voices…" 13 / 600 `#5a5470` (flex), percentage "40%" 13 / 500 `#8a8399`. Under it a progress bar, 6 high, radius 3, track `#ece3d0`, fill `#5B57E8`. The child's gallery shows nothing. Books read with the current voices until it ends; the download resumes after a restart.
- **Waiting for Wi-Fi (115-2, frame 50, lower card).** On mobile data the line becomes a white card (radius 16, padding 12x14): a Wi-Fi glyph, "Storyteller voices wait for Wi-Fi" 13 / 600, size "90 MB" 12 `#8a8399`, and a "Download now" pill (44 high, radius 22, `#ECE7FF`, 13.5 / 600 `#5B57E8`). "Download now" uses mobile data for this download only.
- **Ready sheet (115-2, frame 51).** One bottom sheet over a dimmed gallery (scrim `rgba(43,37,64,.5)`, gallery at .45 opacity): white, top radius 28, padding `24px 18px 26px`. A 56 round `#E3F4E1` badge with a check (`#2E7D32`, stroke 2.8); title "Storyteller voices are ready" 21 / 600; body "Your stories now use the new storyteller voices." 14.5 `#5a5470`; a narrator row (`#F3EFE6`, radius 16, padding 10x12): a 44 round `#ECE7FF` play button (plays the narrator), name "Story Granny" 15 / 600 and "Your English narrator" 12.5 `#8a8399`; and an "OK" button (52 high, radius 26, `#FF7A59`, shadow `0 5px 0 #E2583A`, 16 / 600 white). It shows the next time grown-up mode opens, never in the child's view.
- **Simplified in the frames.** The 40% bar, the "90 MB" size and the narrator name "Story Granny" are illustrations. "Story Granny" is a draft roster name (Slice 116).

**Open point: one note, not two.** The plan (Decision 120, work item 2) says a one-time "Storyteller voices are ready" when the download ends and a separate one-time "Your stories now use the new storyteller voices". The mock joins them into one sheet, shown the next time grown-up mode opens. **Default:** the mock wins: one sheet, with the title and body above; the re-cast runs when the install finishes and the sheet is only the announcement.

**Open point: size placeholder.** The mock shows "90 MB" and says it is a placeholder; the real pack is about 126 MB. **Default:** never hard-code it; show the size from the pack catalog entry (rounded to whole MB).

**Open point: narrator row.** The plan had no narrator row in the note. The mock adds one so a parent can hear the new voice. **Default:** build it; the play button uses the language's default roster narrator (Slice 116) and falls back to the first narrator voice until that slice lands.

Settled by the mock: only grown-up mode shows the status line and the wait-for-Wi-Fi card; the status line sits under the button row; there is a progress percentage and bar.

New strings (all 14 locales): "Getting the storyteller voices…", "Storyteller voices wait for Wi-Fi", "Download now", "Storyteller voices are ready", "Your stories now use the new storyteller voices." (the plan's text gains a full stop), "Your {language} narrator", "OK" (check for an existing key). The size and percentage are formatted numbers, not strings.

## Work

- [ ] 1. Smallest end to end: re-cast on install. `StorytellerController` (or the pack service's
  `InstalledPacksChanged`) triggers a re-cast of every stored cast, Edited included, through
  `CastEditor`, then refreshes the `CastText` name flag. Unit test in `DinoReader.Tests`: an Edited
  Supertonic cast re-casts to Kokoro by gender and keeps flavours.
- [ ] 2. Automatic download: `SpeechStartup` starts the storyteller pack download on Wi-Fi when it is
  missing (resumes after a restart, no duplicate downloads), the grown-up gallery status line with
  progress, the wait-for-Wi-Fi card with "Download now", and the single "ready" sheet (title, re-cast
  line, narrator row with play, OK) shown the next time grown-up mode opens (frames 50-51).
- [ ] 3. Remove the offer card, "Hear one" and "Use Dino's first voices" (`CastStoryteller.cs`,
  `StorytellerController.cs`), and their strings from all 14 `.resx` files; add the new strings
  ("Getting the storyteller voices…", "Storyteller voices wait for Wi-Fi", "Download now", "Storyteller voices are ready",
  "Your stories now use the new storyteller voices.", "Your {language} narrator").
- [ ] 4. Rule in `src/DinoReader.App/CLAUDE.md` (written as a For CLAUDE.md block; `update-docs` lifts it).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Android emulator, fresh install on Wi-Fi: the pack downloads by itself, the ready note shows, an
  English starter's cast screen and a read both use Kokoro voices with no old name visible.
- Existing install with hand-picked Supertonic voices: after the pack lands every book shows Kokoro
  names without opening the cast screen first.
- Mobile data only: no download until "Download now" or Wi-Fi.
