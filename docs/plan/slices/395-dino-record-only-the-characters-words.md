# Slice 395 — Dino: recording a character shows and replaces only that character's words, not the narration around them
**Product:** Dino

*In progress (wave 2026-11-17), draft PR.* *Scoped 2026-11-15 from the user's report; scope and Decisions 1-6 approved by the user the same day.*

**Touches:** `src/Dino/DinoReader.Core/RecordSplit.cs`, `RecordSplitBook.cs`, `RecordSplitWalk.cs`, `StoryClipKey.cs`, `StoryRecordings.cs`, `TranslatedStory.cs` (only if item 5 needs it), `src/Dino/DinoReader.App/ReadingSession.cs`, `RecordPage.cs`, `RecordIntroPage.cs`, `tests/Dino/DinoReader.Tests`, a Maestro record flow under `.maestro/dino/`, this slice file. Widened at claim (2026-11-17 premise check): `src/Dino/DinoReader.App/ReadingPage.xaml.cs`, `RecordBooks.cs`, `GalleryPage.xaml.cs`, `OnboardingListenPage.cs`, `OnboardingVoicePage.cs`, `src/Dino/DinoReader.Core/StoryJson.cs`, `StoryBackupReader.cs`, `StoryBackupState.cs`, `StoryPackageWriter.cs`, `StoryPackageReader.cs`, `FileStoryLibrary.cs`, `Story.cs`, `src/Dino/DinoReader.App/Testing/DinoTestCommands.Record.cs`. Platforms: shared Core and MAUI code, so Android, iOS and the Windows head run it; the device check is the Android emulator (shared UI).

## Why

The user (2026-11-15) recorded their own voice for Freddy and the Slipper with Mummy and Tummy ticked. The record card showed whole sentences with the narrator's words in them, and the take replaced those whole sentences, narration included. From page 2 and 3 of the book (`content/dino/freddy-and-the-slipper/story.md`):

- `One evening his Mummy called, "Dinner, my dear! I've made you some stew. Come and sit over here!"`: the card should show only `Dinner, my dear! I've made you some stew. Come and sit over here!`. "One evening his Mummy called," stays the narrator's, read by the book's own voice.
- `"Just one little spoonful," said Mummy. "It's yummy! It's chicken and peas!"` is three parts: `Just one little spoonful,` (Mummy), `said Mummy.` (narrator), `It's yummy! It's chicken and peas!` (Mummy). Mummy's two parts are two cards, and "said Mummy" is still read by the narrator between them.

The same holds for every character in every book.

## What the code does today

- **The unit is a whole sentence.** `RecordSplitBook.Sentences` (`src/Dino/DinoReader.Core/RecordSplitBook.cs:73`) cuts the page into sentences and `SpeakerOf` (`:87`) gives the whole sentence to the first character whose quote is in it. So the narration in the same sentence goes on the card, and is recorded over.
- **Also wrong because of that:** `It's chicken and peas!" Said Freddy, "No room in my tummy!"` is one sentence whose first quote is Mummy's, so Freddy's line is on Mummy's card and Freddy's own list does not have it.
- **A clip is keyed by sentences.** `StoryClipKey(Page, First, Last, Language)` (`StoryClipKey.cs:8`) names a run of a page's sentences, and the reading screen plays a clip *instead of* those sentences (`ReadingSession.ClipAt`, `ReadingSession.cs:303`). So no clip can cover part of a sentence today.
- **The book's own audio is already per part.** The premade audio (`content/dino/<book>/audio/<lang>/`) is one file per segment (quote or narration, with its voice), from `StorySegmenter` plus `SpeakerAttributor`. So the narration parts are recorded apart already; a parent's take only needs to stand in for the quote parts it covers.

## Decisions (approved by the user, 2026-11-15)

1. **Per character, a card is a run of one speaker's quote parts on a page, never narration.** The unit is the segment (`StorySegmenter` + `SpeakerAttributor` + `StoryCast`, the split the reading screen and the premade audio already use), not the sentence. Quote parts of the same speaker that follow each other with no narration between them make one card ("Dinner, my dear! I've made you some stew. Come and sit over here!"); any narration between them ends the card ("said Mummy" makes two cards). The quote marks are not shown on the card.
2. **Playback: the take replaces only the parts it covers.** While reading, a recorded part plays the parent's take and every other part (the narration, the characters not ticked) plays the book's own audio as before, in order. The Storyteller can still be ticked: then the narration parts are cards of their own, in the same way.
3. **A quote with no known speaker stays with the narrator** (as today), so it is the Storyteller's card, not a character's.
4. **Whole-page mode does not change:** it still records whole pages, sentence-keyed.
5. **Recordings made before this slice keep playing as they do now** (they replace the whole sentences they were keyed to). The record intro shows those older lines as "recorded" only if the new parts they cover are all inside them; nothing is deleted. A parent who wants the cleaner split records the line again.
6. **Other story languages:** a translated book is split into parts with the same rule. If its parts do not line up with the English ones for a page, that page falls back to today's sentence cards in that language (item 5 finds out whether it is needed).

## Work

- [ ] 1. Part keys: give `StoryClipKey` a part range inside a page (first and last segment of the page), next to the sentence range, with its own file name form (for example `rec-<lang>-<page>-p<first>-<last>.m4a`) that `StoryRecordings.FileNamePattern` accepts, and keep reading the old sentence keys. Backups and `.dino` packages carry the new keys (`StoryJson`, `StoryBackupWriter`/`Reader`, `StoryPackageWriter`/`Reader`).
- [ ] 2. `RecordSplitBook`: build per-character pieces from segments: each piece is a run of one speaker's quote segments with no other segment between them, its text the quote text only. `RecordSplit.ByCharacter` and `LineCounts` work on those pieces; the height cut (`Fit`) still cuts a long run at a sentence end.
- [ ] 3. Reading screen (`ReadingSession`): a part-keyed clip stands in for its segments only; the segments before and after it on the page play the book's own audio (premade or speech engine), and the highlight follows the part. A sentence-keyed clip plays as today (Decision 5).
- [ ] 4. Record screen and intro: the card shows only the piece's text; the progress counts (`RecordSplitWalk.Progress`, `SpeakerProgress`, `Recorded`) count parts; "Listen and change" plays a part's take alone.
- [ ] 5. Translated books: check whether `TranslatedStory` keeps segments one to one with the source (the premade audio of the other 14 languages is per segment, so it likely does). If yes, use the same parts; if not, build the Decision 6 fallback.
- [ ] 6. Tests (`tests/Dino/DinoReader.Tests`), on Freddy's pages 2 and 3: with Mummy ticked, the cards are exactly `Dinner, my dear! I've made you some stew. Come and sit over here!`, `Just one little spoonful,` and `It's yummy! It's chicken and peas!`; "No room in my tummy!" is Freddy's, not Mummy's; Tummy's card is `Psst! Freddy! I'm empty!`; no narration is on any character's card; an old sentence-keyed clip still resolves.
- [ ] 7. Device check on the Android emulator (a Maestro flow with the Debug record hooks in `src/Dino/DinoReader.App/Testing/`): tick Mummy and Tummy in Freddy, record the parts, read the book, and read the `[RecordedClip] playing` log lines: only Mummy's and Tummy's parts play the takes, and "One evening his Mummy called," and "said Mummy." play the book's audio.

## Verification

- Item 6 tests pass.
- Item 7 on the emulator: the cards show only the character's words, and reading back plays the narrator's own audio between the parent's takes.
- The user records Mummy in Freddy on their phone after the release and hears the narrator read "said Mummy" between their two lines.

## Out of scope

- Changing how a quote's speaker is guessed (`SpeakerAttributor`); this slice only uses it at a finer grain.
- Re-recording or moving the parent's existing takes for them.
