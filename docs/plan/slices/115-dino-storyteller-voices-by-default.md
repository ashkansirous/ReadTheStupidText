# Slice 115 — Dino: storyteller voices by default

*Planned 2026-10-25*, from the user's report that Dino "is stuck to the old voices": the cast still
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
  background, with a small "Getting the storyteller voices…" line in the grown-up gallery and a
  one-time "Storyteller voices are ready" when done. Until then books read with the current voices.
  On mobile data it waits for Wi-Fi; a grown-up can tap "Download now" to use mobile data.
- **Decision 121: one re-cast of every book when the pack lands, hand-picked voices included.** When
  the install finishes, every book's cast in a language the pack serves is re-cast once (Edited picks
  too; genders and flavours kept), and Dino shows "Your stories now use the new storyteller voices"
  once. This replaces Slice 114's rule that Edited Supertonic picks stay.
- The offer card, "Hear one" and "Use Dino's first voices" are removed from the cast screen. There is
  no way to go back to the old voices in a language the storyteller pack serves.

## Work

- [ ] 1. Smallest end to end: re-cast on install. `StorytellerController` (or the pack service's
  `InstalledPacksChanged`) triggers a re-cast of every stored cast, Edited included, through
  `CastEditor`, then refreshes the `CastText` name flag. Unit test in `DinoReader.Tests`: an Edited
  Supertonic cast re-casts to Kokoro by gender and keeps flavours.
- [ ] 2. Automatic download: `SpeechStartup` starts the storyteller pack download on Wi-Fi when it is
  missing (resumes after a restart, no duplicate downloads), the gallery status line and the one-time
  "ready" and "re-cast" notes.
- [ ] 3. Remove the offer card, "Hear one" and "Use Dino's first voices" (`CastStoryteller.cs`,
  `StorytellerController.cs`), and their strings from all 14 `.resx` files; add the new strings
  ("Getting the storyteller voices…", "Download now", "Storyteller voices are ready", "Your stories now
  use the new storyteller voices").
- [ ] 4. Rule in `src/DinoReader.App/CLAUDE.md` (written as a For CLAUDE.md block; `update-docs` lifts it).

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Android emulator, fresh install on Wi-Fi: the pack downloads by itself, the ready note shows, an
  English starter's cast screen and a read both use Kokoro voices with no old name visible.
- Existing install with hand-picked Supertonic voices: after the pack lands every book shows Kokoro
  names without opening the cast screen first.
- Mobile data only: no download until "Download now" or Wi-Fi.
