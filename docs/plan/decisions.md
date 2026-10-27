## Decisions

Decisions 1-99 are in [notes/decisions-001-099.md](notes/decisions-001-099.md) (moved verbatim, 2026-10-27).

100. **Dino's layout never mirrors (Batch 18).** Every Dino screen, the reader
    included, stays left-to-right in every language, and page turns go the same
    way for every book; text elements follow their own content's direction.
    Extends Decision 92 to Dino. (Slice 96)
101. **The grown-ups button is a book with a plus, everywhere (Batch 18).** It
    replaces the lock in the gallery header and on the reading page; the gate
    and what it opens are unchanged. The `BackupPage` lock glyph stays.
    (Slice 97)
102. **Picture + text books (Batch 18).** A page with a picture and its own text
    (no line boxes) shows the picture beside the text (sideways) or above it
    (upright, as a text panel over the picture). Grown-ups add pictures per page in the
    Pages step: each page of a text-only file gets an optional picture slot,
    and "Pick pictures for all pages" fills them in file-name order; starters
    get pictures the same way. No schema bump. (Slice 98)
103. **The RTST phone main page shows only reading (Batch 19).** A gear for
    Settings (language, voice, speed; no translate toggle), a large read-only text area with the
    highlight and an Edit link, two big buttons (Text from pictures, Text from
    a file) and a simple player. (Slice 99)
104. **Multi-page capture is our own screen (Batch 19).** "Take a photo of
    each page": numbered thumbnails, "+ Add another page" and "Done"; each Add
    captures one page; OCR runs in the background. (Slice 100)
105. **One review page before reading; translation shows what it reads
    (Batch 19).** A big editable text box, a "Translate to <app language>"
    checkbox only when the languages differ, and one Read / Translate and read
    button. Translating replaces the text. Files go straight here. Reverses
    Slice 54's "the editor keeps the original" on the phone. (Slice 101)
106. **Ten open stories in the store (Batch 20).** Short retellings Claude
    writes from public-domain tales (Peter Rabbit in Potter's own words), all
    nine languages, coloured covers now and public-domain illustrations later
    as `story-v2`. `meta.json` gains an optional `credits` line shown on the
    store card. (Slice 102)
107. **Fourteen app languages (Batch 21).** `AppLanguage` adds Danish, Finnish,
    Swedish, Norwegian Bokmål (`nb`) and Dutch to all three apps; Claude writes
    the UI translations; da/fi/sv/nl read with Supertonic-3. Extends Decision 68.
    (Slice 103)
108. **A `piper-nb` voice pack (Batch 21).** Piper `no_NO-nvcc-medium` (ten
    speakers, CC0) for Norwegian on every platform; the espeak-ng GPL exception
    covers this pack with the same scope as `piper-fa`. (Slice 104)
109. **Dino speaks the five new languages fully (Batch 21).** A story rule set
    per language and every shipped book translated into all five. (Slice 105)
110. **Ten more Windows translation packs (Batch 21).** Opus-MT to and from
    English for da, fi, sv, nb and nl. (Slice 106)
111. **Every new Dino book translates to my language by default (Batch 22).** The per-story switch
    stays; on Windows and iOS the switch is greyed with "Translation works on Android for now". (Slice 107)
112. **Dino shows the translated text (Batch 22).** Highlight follows the translated sentence; scanned
    pages show picture plus text while translated; a per-story grown-up "Show original text" switch. (Slice 110)
113. **A Dino cast per reading language (Batch 22).** Auto-cast on first read in a language, pins kept
    per language, merges shared; the cast screen lists that language's voices. (Slice 108)
114. **One Dino narrator per language (Batch 22).** Chosen from a Narrator button on the grown-up
    gallery, used by every book; the per-book narrator is dropped. (Slice 109)
115. **Parent recordings in Dino (Batch 22).** AAC per page, on device, in backups, never in store
    uploads; mic only behind the grown-up gate; a recorded page always plays the recording. (Slice 111)
116. **Dino store age range and author (Batch 22).** `ageMin`/`ageMax`/`author` in `meta.json`,
    chips for Play's bands 5 & under, 6-8, 9-12, filtered on the device. (Slice 112)
117. **Dino voice flavours per character (Batch 23).** Small, Big, Bouncy, Sleepy or Normal (pitch
    and pace), guessed from the character, editable on the cast screen; narrator at a storyteller pace. (Slice 113)
118. **A downloadable `kokoro-en` pack for Dino English (Batch 23).** Kokoro storyteller voices when
    installed, Supertonic otherwise; RTST unchanged. (Slice 114)
119. **The espeak-ng GPL exception covers `kokoro-en` (Batch 23)**, same scope as `piper-fa`. (Slice 114)
120. **The storyteller pack is Dino's default voice (Batch 24).** Downloaded by itself over Wi-Fi on
    launch when missing ("Download now" for mobile data); the offer card, "Hear one" and "Use Dino's
    first voices" are removed. (Slice 115)
121. **One re-cast of every Dino book when the storyteller pack lands (Batch 24)**, hand-picked voices
    included (genders and flavours kept), replacing Slice 114's keep-Edited rule. (Slice 115)
122. **Dino's cast is a list of storyteller characters in every language (Batch 24).** About 16-20 per
    language, each a voice plus a flavour plus a local kid-friendly name, on the language's best engine;
    engine and Supertonic (Overlord) names are never shown; Dino casts, a grown-up swaps. (Slice 116)
123. **Dino animal characters say their sound about every 2-3 of their lines (Batch 24)**, in their own
    voice, per-language word lists, never when the line already has it, not on narration; a per-story
    switch, on by default. (Slice 117)
124. **The storyteller pack serves Spanish, French and Portuguese if it speaks them well (Batch 24)**;
    Japanese and Chinese only if the same check passes. (Slice 118)
125. **Claude illustrates Dino books by driving the Gemini web app in the user's Chrome (Batch 25).** One character
    chat per book (sheets, line-up), then fresh page chats with the line-up pasted in and outfits spelled out, at most 3
    pictures per chat and one per chat once a chat repeats a scene (a single long chat mixed scenes and outfits in the
    pilot); pictures are saved and committed without asking each time; stop at the free tier's daily cap and resume. Reverses Batch 10's "no AI illustrations" and
    supersedes Decision 106's public-domain follow-up. (Slice 119)
126. **One house style for every Dino book (Batch 25)**: soft watercolour and coloured pencil, warm gentle colours,
    simple rounded shapes, no text; style line and cast per book in `content/dino/<stem>/art.md`. (Slice 119)
127. **Dino pictures are 3:4 portrait, 896x1200 (Gemini's native output; never upscaled), text-free, characters and action in the top 70% (the words panel takes at most 30%, Slice 122; was top 40% before it) (Batch 25)**; the title on a
    cover comes from our code, never from Gemini. The store list gets a 288x384 `cover-thumb.jpg`; the full cover stays inside the package. (Slice 119)
128. **The bundled starters are also store stories with the same package id (Batch 25)**, so a deleted starter can be
    downloaded again. (Slice 119)
129. **Making a Dino story is the project skill `create-dino-story` (Batch 25).** (Slice 120)
130. **No Dino book shows harm or pain (Batch 25)**, in text or pictures, whatever the classic tale says;
    conflicts end gently; all 14 books audited and rewritten, translations updated, and a test scans every starter and store text for banned harm words. Bundled starters carry a content version (package manifest `version`, the same number in their store entry): an installed starter at a lower version is replaced on launch, keeping the cast and switches; a deleted one stays deleted. (Slice 119)
131. **In Dino picture mode the words take at most 30% of the visible picture with the phone upright, and each book has a grown-up switch to hide them (Batch 25).**
    The switch is off by default for books whose pictures already show the words (scans, photos of pages, PDFs) and on for every other book. (Slice 122)
132. *Reversed by Decision 157 (Slice 152, 2026-10-26): the animal comes from the book, not the cast character.* **A Dino cast character's animal comes from the storyteller character it is cast as (Batch 26; changed 2026-10-25).** Each roster character carries an animal kind or none (Cheeky Piglet = pig, Big Bad Wolf = wolf, Brave Knight = none), and animal sounds use the cast character's animal; a pig cast as a non-animal character makes no sound, by design. The stored `animal` field in `story.json`, the build-time animals and the guess from the name and story text only pick the first character when a book is cast or recast; they do not drive sounds afterwards. The Animal sounds card follows the current cast. There is no separate Animal control on the cast page (Batch 26-28 mock, frame 59). *Originally:* a stored per-character animal the grown-up could change on the cast page (Slice 125). (Slices 125, 136)

133. **Read The Stupid Text and Dino Reader stay in one repo (Batch 27).** About a third of the app code is shared and it is the engine (speech readers, voice packs, chunking, OCR, translation); splitting would mean versioned NuGet packages for every engine fix. The fix is in how changes ship. (note: docs/plan/notes/2026-10-25-product-separation.md)

134. **Only Mobile.Shared's namespace is renamed, to `ReadTheStupidText.Mobile.Shared` (Batch 27).** No wider rename of the shared projects for now. (Slice 131)

135. **Tray-only code lives in `ReadTheStupidText.Windows.Tray`, referenced only by the RTST Windows app (Batch 27).** Shared Infrastructure keeps speech, translation, settings, logging and sanitizing; tests run per product. (Slice 128)

136. **The shared speech engine carries no Dino-only voice features (Batch 27).** Flavours, pitch and animal effects live in `DinoReader.Core` and reach the engine through one small hook, so a shared-engine change is a real engine change and shipping both apps for it is correct. (Slice 129)

137. **Each product has its own version, tag and GitHub Release: `rtst-v<x.y.z>` and `dino-v<x.y.z>` (Batch 27).** Both continue from the current shared version; a release carries only its own product's files. (Slice 130)

138. **Dino fetches one small config from the Worker (Batch 28).** `GET /api/app-config` serves per-platform `minVersion`/`latestVersion` and the telemetry switch and sample rate from the R2 object `dinoreader/config.json`, so they change without a deploy or an app release; the app falls back to its cached copy, then to safe defaults. (Slice 132)

139. **Below `minVersion` Dino shows only a "Please update" page (Batch 28).** Its button passes the grown-up gate, then opens the Play in-app immediate update, the App Store page or the Windows Store update. Below `latestVersion` there is only a quiet line in grown-up settings. (Slice 132)

140. **Dino usage data is anonymous counts with context, never an install, device or advertising id (Batch 28).** No typed text, no own-book titles, nothing finer than the hour. A per-device profile would be a child's personal information under COPPA and profiling under the UK Children's code. (Slice 133; note: docs/plan/notes/2026-10-25-dino-usage-insights.md)

141. **Usage counts are on by default with a grown-up off switch and a line in the first-run screens (Batch 28).** (Slice 133)

142. **Usage events go to Workers Analytics Engine, batched per session, switched and sampled by the remote config (Batch 28).** Reports suppress counts under 10. (Slices 133-134)

143. **Read The Stupid Text usage data is anonymous counts with context, never an install, device or advertising id (Batch 29).** No read text, no file names, nothing finer than the hour; durations in buckets. On by default with an off switch in Settings (control panel on Windows) and one line on first run; no consent prompt. (Slices 137, 140)

144. **RTST reuses Dino's usage pipe (Batch 29).** The same Worker `POST /api/events` and `GET /api/app-config`, served on the RTST host, with RTST's own Analytics Engine dataset `rtst_usage` and config object `readthestupidtext/config.json`; the phone client is shared from `Mobile.Shared`. (Slice 137)

145. **No usage event is sent for an app until every store privacy declaration for it matches (Batch 29).** The remote `telemetry.enabled` stays `false` until the *Store privacy declarations* checklist in `plan.md` is ticked for that app; this also covers Dino's pending Play and App Store answers. (Slices 133, 137, 140)

146. **Any wait over 500 ms shows a stage indicator with Cancel (Batch 29).** An overlay over the reading area (a tray flyout for windowless Windows reads) naming the stage and progress; nothing shows under 500 ms; no Lottie for now. (Slices 138, 141)

147. **Early start translates and voices the head before the tap (Batch 29).** Original: first 2 sentences (else ~120 characters at a word boundary). Translation, only when the detected language differs from the user's: translate the first 3 sentences, voice the first 2. The tap cancels the other side; a missing translation model downloads early only on an unmetered network. Supersedes the one-vs-two-sentence mismatch of Decision 66 and `HeadPolicy.Default`: both now say two sentences (`Default` is two sentences, at most 120 characters; `Cached` stays two, at most 200). The phone's early start (Slice 139) keeps the translated head and translates only the rest, serializes the two voice preparations (the reader holds one at a time) and counts nothing itself. (Slices 139, 142)

148. **Batch 29 order: phone first, then Windows (Batch 29).** 137 (counts) -> 138 (wait indicator) -> 139 (early start) on Android/iOS, then 140-142 on Windows; iOS has no translation, so it gets counts and the indicator only. (Slices 137-142)

149. **One shared wait overlay for both MAUI apps; Dino shows it on every wait over 500 ms (Batch 30).** `Mobile.Shared/Waiting/`, driven by the `WaitStage` timer model; Dino's look is a bouncing dino drawn with MAUI animation (static under reduce-motion) and one grown-up line; no Lottie. (Slices 138, 143)

150. **Farsi's default voice is `mana` (female) in both apps; `ganji_adabi` leaves the cast (Batch 30).** Characters on it move to `ganji`/`gyro`; it stays selectable; grown-ups' picks are kept; the default changes in code, not in `packs.yml`. (Slice 144)

151. **Sound-word spellings are measured per voice before any book is rewritten (Batch 30).** Until the list exists, sound words use repetition with punctuation. (Slice 145)

152. **Dino books are written in spoken, kid-friendly language in every language (Batch 30).** The way a parent talks to a 4-6-year-old: colloquial forms, diminutives, exclamations; never literary or written-only forms. (Slice 146)

153. **All existing Dino books are rewritten to Decision 152, one PR per book: Farsi first, then English, then the other languages (Batch 30).** (Slice 146)

154. **iOS reviews follow one cadence for every app (Pipeline).** Never cancel a waiting review or expire a build; at most one review waiting per app; always the newest processed (`VALID`, unexpired) build. External TestFlight testers (group "Rofagha"): daily and automatic (`testflight-external.yml`). App Store: a manual button (`appstore-submit.yml`, release type after approval; the first listing is filled in App Store Connect by hand). Internal "Family" testers are unchanged (every merge, no review). Supersedes Decision 70's manual tester-group step. (Slice 147: docs/plan/slices/147-ios-review-cadence.md)

155. **Dino reads at 0.8x by default; grown-ups set 0.5x-1.2x in 0.05 steps, app-wide (Batch 31).** Behind the gate in grown-up settings; flavour pace multiplies on top; RTST's `PlaybackRate` range is unchanged. (Slice 150: docs/plan/slices/150-dino-slower-reading-speed.md)

156. **Dino has three view modes: Picture only, Text only, Picture + text (Batch 31).** Picture books open in Picture + text; an old saved `Picture` becomes Picture + text. The "Words over pictures" switch (Slice 122) and "Merge two characters" (Slice 65) are removed; stored merges are ignored. (Slice 151: docs/plan/slices/151-dino-three-view-modes.md)

157. **A store or starter book character's animal comes from the book's data only, never from its voice or cast character (Batch 31; reverses Decision 132).** Every such character carries an explicit `Animal` (a kind or `none`); recasting never changes the animal or its sound, which is still spoken in the character's voice. The animal is not shown in the UI. Parent-provided books are out of scope and unchanged. (Slice 152: docs/plan/slices/152-dino-animal-from-the-book.md)

158. **Robotic-sounding voices are found by ear (Batch 31).** Claude renders every English roster character to a listening page, the user flags the robotic ones, and the flagged flavours are toned down or the characters moved; un-edited casts are recast. (Slice 153: docs/plan/slices/153-dino-no-robotic-voices.md)

159. **Farsi and Arabic Dino books carry full vowel marks, shown on screen, if a listening test shows the voices read better with them (Batch 31).** A voice that reads worse gets the marks stripped before speaking only. Books are marked after their Slice 146 rewrite and any open PR. (Slice 154: docs/plan/slices/154-dino-farsi-arabic-vowel-marks.md) **Superseded in part, 2026-10-27 (user):** the marks are not shown on screen. They are a hidden "how to read" text (`speak:<lang>` table); kids see plain Farsi and the reader speaks the marked text if present, else the plain text. Marks come only from words the user approved by ear.

160. **Dino's first run ends with a three-step wizard of real actions (Batch 32; narrows Decision 98).** After language and voice download, "See what Dino can do" on Ready to read walks a new install through the store, recording your voice and adding your own book, in that order (no read step: the shelf is right there; Batch 32 mock, 2026-10-26); each step has Try it (opens the real feature, then the next step) and Skip, plus Skip all; no gate during it; new installs only, with a Show the intro again row; its steps are counted with closed-list usage events. Coach-marks, tips and what's-new screens stay out. (Slice 155: docs/plan/slices/155-dino-first-run-intro-wizard.md)

161. **A store or starter book chooses each character's voice; the app only falls back (Batch 33).** Every character in a `.dino` book carries traits (age, gender, kind, voice qualities; closed enums) and a suggested voice (a roster id, the same in every language), written by the story writer and checked by the reviewer. The app uses the suggestion when that roster id exists in the language, otherwise the roster character whose traits match best. An author's pick is its own voice source, never recast by the app; a grown-up's change always wins and can be undone with "Back to the book's voice". All new fields are optional (`schemaVersion` stays 1). Parent-added books keep the runtime picker. The "kind" trait is Slice 152's `Animal` field, not a second one. (Slice 156)

162. **Dino book content lives in `content/dino/<book>/` and is built by a console tool; a version bump on `main` uploads it to the store (Batch 34).** Each book is a folder (`story.md`, `book.json`, pictures, covers, translations, art record), built by `tools/DinoReader.BookBuilder`, not by env-var tests in `tests/DinoReader.Tests`. Starters stay committed `.dino` files, checked against their folders. `book.json` carries a content hash, so changed content without a version bump fails CI. A `dino-store.yml` job uploads every bumped store book on merge with a scoped R2 token: a merged version bump is the approval, replacing the per-upload OK. (Slice 157: docs/plan/slices/157-dino-book-content-out-of-tests.md)

163. **Store books publish themselves on merge, from `release.yml` (Batch 34; narrows Decision 162's upload part).** A `publish-store` job, triggered by a `dinostore` ship target or `force_all`, builds the store books and uploads every one whose version is above the live `/api/stories` version, `meta.json` last, never overwriting a version. It reuses `CLOUDFLARE_API_TOKEN` (the APK copy already writes the same bucket), so no new token. A merged version bump is the approval. It runs before and independently of Slice 157's content move. (Slice 158: docs/plan/slices/158-dino-publish-store-books.md)

164. **Every word Dino shows follows the chosen app language, own books included (Batch 35).** Strings resolve through the stored language pick, never the thread's `CurrentUICulture`. Book titles show in the chosen language only, on the shelf, store and reader (the store `meta.json` gets a `titles` map). Parents' own books translate on the phone only (no public translate API), and a translation pack downloads only after the parent agrees to the prompt, never on its own or silently. Where no on-device translator exists (Windows; iOS until a binding is found) own books stay in their language with a note. The layout still never mirrors. (Slice 159: docs/plan/slices/159-dino-everything-in-my-language.md)

165. **Device and desktop tests are scripted, headless and off the user's screen (Batch 36).** Android checks run on one warm headless `x86_64` emulator per wave with an `android-x64` Debug build (arm64 only for the Release smoke test); sherpa-onnx's own x86_64 natives (v1.13.3 release tarball, onnxruntime 1.24.3, fetched at Debug build time) load and read on the emulator (Slice 161 outcome), so neural checks run there too. UI checks are Maestro flows found by `AutomationId`, with state set by Debug-only test hooks (Android broadcast, iOS URL scheme) and asserted on `RTST-TEST` logcat markers. Windows desktop checks run in a headless Hyper-V VM over PowerShell Direct, reset to a clean checkpoint, never on the host (Windows Sandbox rejected: visible window, hard to script). Audio quality and timing are checked on the real phone over wireless adb only. Logic that can be pure moves into unit tests. (Slices 161-165: docs/plan/slices/161-warm-headless-x64-emulator.md)
