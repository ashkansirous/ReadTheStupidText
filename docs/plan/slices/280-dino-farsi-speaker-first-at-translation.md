# Slice 280 — Dino: Farsi speaker first, built at translation time from the pieces we already have
**Product:** Dino

*Built 2026-11-06, merged as PR #869 (53ae8447). Checked on the Android emulator (build 2e3eab03e1f3); iOS by compile only.* Claim notes (wave 32): Touches widened at claim from a code check:
`src/DinoReader.Core/PremadeAudio.cs` (A2 index keying), `src/DinoReader.App/StoryVoicing.cs` (calls
`SpeakerFirst.Regroups`/`SourceOrder` at :304-305, A3), `content/dino/**/audio/fa/manifest.json` (A2 rewrite; the
`.m4a` clips stay, Slice 254's unused colon-tag clips may go) and each changed book's `book.json`/meta version bump.
The rewritten books are uploaded to the live store right after the merge (standing approval for store-book bumps,
2026-10-27), so store and app match. Dino Reader only. Replaces the read-time regroup of Slice 254
([254](254-dino-speaker-first-and-sound-placement.md)).

From the user, 2026-11-06:

- "in farsi, we dont say "I am ok" he said "what about it". We say he said "I am ok. what about it". I told you before
  but instead of fixing the story, you changed the reading of it. you should apply this to the translation time, not
  when reading"
- "you shouldnt need to retranslate the same text. you have "part a." he said "part b.". you should be able to just
  concat them and say he said "part a. part b." both in text and then should also be able to do the same with the
  audio that was generated. no re-recording"

**Touches:** `src/DinoReader.Core/SpeakerFirst.cs`, `src/DinoReader.Core/StoryTranslation.cs`,
`src/DinoReader.Core/TranslatedStory.cs`, `src/DinoReader.Core/BundledTranslations.cs`,
`src/DinoReader.Core/AnimalSounds*.cs`, `src/DinoReader.App/StoryTranslator.cs`,
`tools/DinoReader.BookBuilder/**`, `tests/DinoReader.Tests/**`, `src/DinoReader.Core/CLAUDE.md`,
`.claude/skills/dino-storytelling/SKILL.md`.

## Why

Slice 254 (my mistake) kept each Farsi piece in the English order in the translated story and regrouped the pieces
only when reading (`SpeakerFirst.Regroup`, called from `StoryTranslator` after `StoryTranslation.TranslateAsync`).
The user asked for the translation itself to say «پیم گفت: «A. B.»», not for the reader to rearrange it. The
translations and the recorded clips we already have are enough: the tag, part A and part B each exist as a
translated piece and as a clip.

## Decisions (from the user's words)

- **No re-translation, no re-recording.** The Farsi line is made by joining the existing translated pieces
  (tag, then A, then B) and its audio by playing the existing clips in that same order. No new Gemini request, no
  new entry in any `fa.json`.
- **Done once, when the translated story is built**, so the text the reader shows is already in Farsi order and
  the voice, the highlight and the page text all follow that one order. Nothing reorders while reading.
- Each part keeps its own voice: the tag in the narrator's, A and B in the character's.
- The tag reuses its existing clip as is; any colon shown in the text is display only and never changes the key
  a clip is looked up by.
- Arabic was added to Slice 254 by me, not asked for by the user. It moves to the same translation-time join, so
  removing the read-time code does not send it back to the split order (coordinator call; change if wrong).

## Decided while building

- Rollout skew (review finding 3, high): a phone on the previous app (Slice 254: regroups at read time, looks a tag up by the text with its colon) misses the tag clip of every moved tag once it
  downloads the new pack, and a new app with an old pack misses it until the pack updates. Accepted: the old app already misses tag clips with today's packs (A2: "Slice 254 ... missed the index keying"),
  and the pack update is what fixes it. A miss is the device voice for that tag, not a failed read.
- join-audio never renames a clip (medium review finding 1, second strike on the retarget): every manifest entry keeps the clip file it has; the command rewrites only an entry's text hash and
  text (its index is the piece's), and fails if an entry has no clip. No renames and no adopting a file, so no stale or orphaned clip can arise. The manifests were rewritten from origin/main's, so Slice 254's
  colon-text clips are the ones used; the old-stop clips in Pigs and Goldilocks stay on disk, unused.
- (Slice 284 removed `SplitSegment.SpokenText`: nothing read it, a device voice speaks `Segment.Text` with no colon. The lines below describe the Slice 280 state.)
- Device voice (review finding 2): the clip key stays the tag's text as translated; what the device voice speaks for a joined tag is `SplitSegment.SpokenText` (the tag with its colon, as a lead-in),
  the one place that decides spoken text vs lookup key. (Later review found `SpokenText` is not read by app code, so a piece with no clip read by a device voice loses the colon; recorded under *From review* as a follow-up.)
- Join at translation time: `StoryTranslation.TranslateJoinedAsync` calls `SpeakerFirst.Join`; `Regroup`/`SourceOrder` are removed. Animal sounds use `SpeakerFirst.JoinedSourceOrder`.
  The colon is display only (`SplitSegment.IntroducesQuote`, drawn by `TranslatedStoryBuilder`); every lookup, hash and count uses the plain text. Arabic joins the same way.
- BookBuilder `join-audio` rewrote all 32 fa/ar manifests from origin/main's: index and hash only, never a file name (no-rename design, chosen after two failed retarget fixes); it fails on a missing clip or a duplicate index.
  `audioPacks` versions went up by one in each `audio-cast.json` (fa v3, ar v2). The old-stop clips of Pigs and Goldilocks stay unused on disk.
- Fixture `install-audio-pack` numbers clips in the joined order via `StoryTranslator.TranslateAsync`. The reading log line now ends with `clip <file>` (one line in `ReadingSession.cs`, outside Touches, accepted).
- After the merge: upload the 16 books' new fa/ar packs to the live store (standing approval for store-book bumps).

## From review

Findings files: `reviews/s280-high.md`, `s280-medium.md`, `s280-check1.md`, `s280-check2.md`, `s280-check3.md` (in `D:/ReadTheStupidText-worktrees/reviews/`, outside the repo).

- High: applied 1, 2, 4, 5. 3 decided (rollout skew, see above). Skipped 6 (BookBuilder error text/O(n^2) scan), 7 (double enumeration in `TranslatedStory`), 8 (slice file not updated; done by this record).
- Medium: applied 1 (one design: no renames), 2, 3, 4. 5 refuted: nothing re-splits after `Join`. Skipped 6 (`VoicedSegment.Text` vs `Segment.Text`, low; lookups use `Segment.Text`).
- Check 1: applied 4, 6, 7. Skipped 1 (decided: same as high 3), 2 (the device flow decided it), 3, 5, 8.
- Check 2: all 9 skipped: the tool was already run and its outputs pass the tests. `SplitSegment.SpokenText` is now dead code; remove it in a follow-up.
- Check 3: all 10 skipped. 1, 2, 9: `join-audio` is a one-time tool already run, its outputs pass tests and `check-audio`. 3: dead `SpokenText`, follow-up. 4: the emulator run showed fixture-13 then fixture-14 in tag order. 5: debug builds do not send production analytics, unconfirmed (see Not yet verified). 6, 7, 8: flow robustness, low; the flow passed. 10: low.

## Work

- [x] A1. Translation-time join in Core: when a story is translated into a speaker-first language, build the
  translated story with each split quote (A, tag, B) and each quote-tag (A, tag) as tag + A (+ B), using only the
  existing translated pieces. The same function serves the app and the BookBuilder dump. Tests: quote-tag-quote,
  quote-tag, tag-quote and narration unchanged, a quote spanning two sentences, and every split quote in the 16
  store books' Farsi files joins without a missing piece.
- [x] A2. Audio reuse: each part of a joined line plays the clip it already has. Checked 2026-11-06 (origin/main):
  `PremadeAudio.Find(index, text)` (`PremadeAudio.cs:54`) matches a manifest entry by **position in the spoken list
  and** SHA-256 of the text, so a new order needs each Farsi `audio/fa/manifest.json` rewritten to the joined
  order (indexes only, the clip files stay), and the tag's lookup text keeps its original stop so its old clip
  matches (a colon is display only). 16 books have Farsi manifests. A BookBuilder command rewrites them from the
  existing clips and fails if any piece has no clip. Remove the colon-tag clips Slice 254 added if nothing else
  uses them. Test: every Farsi book needs zero new clips. (Slice 254's "only the colon tags get new clips" missed
  the index keying.)
- [x] A3. Built: the read-time regroup is gone. `StoryTranslator.TranslateAsync` joins once, through
  `StoryTranslation.TranslateJoinedAsync` (`SpeakerFirst.Join`), so the translated pieces are already in speaker-first
  order and nothing reorders while reading. `SpeakerFirst` keeps the join and `JoinedSourceOrder`, which
  `StoryVoicing.SpokenOrder` (via `StoryTranslator.JoinedSourceOrder`) uses only to give the animal-sound plan the
  story's pieces in the joined order; Slice 254's placement tests still pass.
- [x] A4. The highlight and pagination follow the joined text (piece ranges are those of the joined story).
  Android, iOS and the Windows head share this Core code; Android is checked on the emulator, iOS by compile
  (`-p:IncludeIos=true -p:IncludeWindows=false -p:IncludeAndroid=false`) and the same Core tests.
- [x] A5. Skill wording: `dino-storytelling` section 3 says the join is made from the existing pieces at
  translation time, no re-translation or re-recording (done in the planning PR). When A3 merges, drop its
  "Until Slice 280 ships" sentence.

## Verification

- `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- BookBuilder `dump-sentences three-little-pigs fa` shows «پیم گفت:» then both halves, with no missing clip.
- Android emulator: Three Little Pigs in Farsi shows and reads the tag first, highlight in order. iOS: compile.

Result (emulator build 2e3eab03e1f3): `premade-audio-past-roar`, `premade-audio-three-clips` and `animal-clip-plays` PASS. `farsi-speaker-first-join` failed once (flow setup: the read ran in English, `skip-first-run lang=` is ignored past the first run); fixed with the set-language hook, then PASS.

#### For CLAUDE.md (lifted 2026-11-06)

- `src/DinoReader.Core/CLAUDE.md`: speaker-first languages (fa, ar) join at translation time from the existing pieces. Pack manifests are keyed by spoken position and the plain-text hash, so any reorder needs `join-audio` (index and hash only, never renames). (Slice 280: docs/plan/slices/280-dino-farsi-speaker-first-at-translation.md)

#### Not yet verified

- iOS beyond compile.
- The Windows head.
- On a real phone: the live fa/ar packs after the upload.
- Whether a debug build skips the production usage counter when the `install-audio-pack` test hook hits a table hole (check3 finding 5, unconfirmed).
