# Slice 457 — Dino Alexa: finds a book by any part of its name, lists every book, plays only what it can, and keeps favourites
**Product:** Dino
**Touches:** `worker/alexa.ts`, `worker/alexa*.test.mjs`, `worker/alexa-favourites.ts` (new), `worker/schema/stats.sql` (the favourites table), `alexa/skill-package/interactionModels/custom/en-US.json`, `alexa/skill-package/interactionModels/custom/en-GB.json`, `tools/dino/alexa-book-names.mjs` (new), `PRIVACY.md`, `site/dino/privacy.html`, this slice file.

*Planned 2026-11-26 from the owner's first real-Echo test of the Slice 282 skill.*

## Why

The owner, 2026-11-26, on a real Echo: "It starts ok, but it gives the list of 5 stories out of all but then cannot find them. It said goldylock which was its first suggestion, it said I cannot find it. I said goldylock and the 3 bears, it said it cannot find it. I said the lion and the mouse, it started reading. I said alexa the lion, it started audibale even though it was in dino. It did not suggest buying any subscription and not suggested any list or search command. It did not check with likeliness to search, like goldy or goldylock should be enough". Then: "we also need to add favorites in case a kid likes a story and parents want to play that".

What was found the same day (three sources):
- **Code** (origin/main db6113ed): `worker/alexa.ts` `findBooks` (:205-214) matches whole words only (exact, then a word run, then 75% of words), with no spelling tolerance; "goldylock" scores 0 against "goldilocks" and "3" is not "three". The interaction model has one custom intent, `ReadBookIntent`, whose `book` slot is `AMAZON.SearchQuery` behind a carrier word ("read", "play" ...), so a bare title reaches no intent. The launch reads the first 5 store titles (`MAX_LISTED_BOOKS`, :21) and names no command; Help repeats the launch line. No list intent, no favourites.
- **Simulation** (`ask smapi simulate-skill`, en-GB, development stage): "goldilocks", "goldylock", "goldilocks and the three bears", "what books do you have" each failed with "did not resolve to any intent"; "read goldilocks" resolved and returned `AudioPlayer.Play`.
- **Live store and logs:** of the 16 English books in `/api/stories`, only 3 listening tracks play (`/api/stories/<id>/listen/en` answers 206 for The Lion and the Mouse, X Marks the Treasure! and The Gingerbread Man); 12 answer 402 (paid) and The Three Little Pigs answers 404. Four of the five books the launch offers are paid, so the skill sends Alexa a Play for a track that refuses it (this is also why only The Lion and the Mouse played). The Worker's own logs show every Echo request answered 200; they record no intent or outcome, so a miss cannot be read from them.
- "Alexa, the lion" during playback went to Audible: an AudioPlayer skill must end its session when the story starts, so a bare "Alexa, …" goes to Alexa itself. No skill can take that over; only "Alexa, ask Dino Bedtime Stories for …" and the built-in pause / resume / next / previous / start over reach the skill.
- A subscription offer is Slice 415 item D (Alexa In-Skill Purchasing, kid skill), not this slice.

## Decisions

Claude, 2026-11-26 (obvious behaviour, from the owner's report):
1. **A book is found from any recognisable part of its name, with spelling slack.** Number words and digits are the same ("3" = "three"), plural and singular are the same, small spelling slips match (edit distance 1 for words of 4-7 letters, 2 for longer), and a first-word prefix of 4+ letters matches ("goldy" → Goldilocks). When two books match equally, the skill names both and asks which.
2. **A bare title works, with no carrier word.** A second intent takes the title alone, using a custom slot type of every book's name plus short names ("goldilocks", "three bears", "the lion", "gingerbread man"), generated from the repo's English book metadata by `tools/dino/alexa-book-names.mjs`; the carrier-word `SearchQuery` intent stays for names not in the list. A test fails when a repo book has no slot value.
3. **Only books that play are offered as playable.** The launch says how many books there are, names the free ones first, then says the rest come with the Dino subscription. A book whose track would refuse (402) or is missing (404) is never sent as an `AudioPlayer.Play`: a paid book gets a short spoken "this one comes with the Dino subscription in the Dino app" and the free list (Slice 415 decision 2 wording rules apply once 415 builds the link); a missing track is left out of every list.
4. **Every reply says what you can say next.** Launch: "Say a book's name, say 'more books', or say 'play my favourites'." Help lists all commands, including "Alexa, ask Dino Bedtime Stories for <book>" while a story plays.
5. **A list command pages through every book**, 5 at a time ("what books do you have", "list the books", "more books", "what else"), then "that's all the books".
6. **Favourites, per Alexa account.** "Add this to my favourites" (also as "Alexa, ask Dino Bedtime Stories to save this story" during or after playback, using the current or last AudioPlayer token), "play my favourites" (plays them in a row, next/previous move through them), "what are my favourites", "remove <book> from my favourites". Stored in the `STATS` D1 database keyed by the opaque Alexa user id, nothing else; the privacy text says so. Launch mentions favourites only when the account has some.
7. **Logs record the outcome, never what was said.** Each request logs intent name and outcome (found <book id> / not found / paid / list page / favourite action). The child's words are never logged (child-directed skill); misses are read from the Alexa console's anonymised utterance history instead.

## Work

- [ ] 1. Matching: decision 1 in `findBooks`, unit tests for "goldylock", "goldy", "goldilocks and the 3 bears", "the lion", "gingerbread", and a tie.
- [ ] 2. Interaction model (en-US and en-GB): bare-title intent and slot type (decision 2), `ListBooksIntent`, favourites intents; generator script and its test.
- [ ] 3. Launch, list, help and paid/missing handling (decisions 3-5), with tests that no Play directive is sent for a 402/404 track.
- [ ] 4. Favourites (decision 6): D1 table in `worker/schema/stats.sql`, `worker/alexa-favourites.ts`, tests; privacy text in `PRIVACY.md` and `site/dino/privacy.html`.
- [ ] 5. Outcome logging (decision 7).
- [ ] 6. After merge and deploy (Workers Builds) and `ask deploy` of the model: simulate every utterance from the owner's report plus the list and favourites commands, and fetch each returned Play URL to prove it answers 206.

## Not yet verified

- On the owner's Echo: "Alexa, open Dino Bedtime Stories", then "goldylock", "the lion", "more books", "add this to my favourites", "play my favourites" (owner, after item 6).
