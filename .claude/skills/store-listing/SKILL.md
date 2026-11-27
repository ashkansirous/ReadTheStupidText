---
name: store-listing
description: What a store listing of Read The Stupid Text or Dino Reader says and which screenshots it shows, on every store (Google Play, App Store, Microsoft Store, Amazon Appstore, Alexa) - know the market first, lead with what only we do, the text order What, Why, Who, Features, captions and keywords per language, subscription wording, the screens each product must show, how the shots are taken by script and checked; then hand-off to the global store-listing agent to push. Use when the user says a listing is missing something, "update the store text", "new screenshots", "the listing doesn't show X", before any production submission, when pricing changes, and when a new store or language is added.
---

# Store listing: what to say, what to show

This skill decides the **content** of a listing. Pushing it is the global `store-listing` agent's job
(write, diff against live, push text and shots, read back), with no review round: the user's ask is the
go, and Claude checks the text and every shot against this skill itself (user, 2026-11-15). This skill
never calls a store API.
Field limits, image sizes, caption and subscription rules per store: [`stores.md`](stores.md) (official
pages, read 2026-11-14; re-check anything marked *not confirmed* or *search only*).

## 0. Know the market

Before a big listing rewrite, a new store or language, a price change, and at least once a year:

- Re-check the top competitors with the `web-research` agent and update the market note
  (Dino: [`docs/plan/notes/2026-11-14-dino-market-research.md`](../../../docs/plan/notes/2026-11-14-dino-market-research.md);
  write a new dated note, link it here). Dino's list to re-check: Readmio, Readby, Vooks, Dreambook Kids,
  Story Time Together, Eliozz, Kidzify, Yonde, My Story, Arden, StoryTime Buddy, BookaBooka, Booka, Lylli,
  Pickatale, Polyglutt/Polylino, Epic, Qesseh Shab, Ay Ghesse, I Read Arabic, Hekayat Belaraby, Inklingo,
  and retry Bookful, Moonbeam, Amazon Kids+.
- The listing **leads with what only we do**, never with what every app does ("stories read aloud" is
  table stakes). Dino today: a voice per character + a family member recording one character + 15
  languages + buying single books to keep; no competitor combines all four.
- **Never sell a feature where we are weaker than the market.** Dino's catalogue is 15 books against
  hundreds or thousands: no counts, no "library", no "hundreds of stories". Sell it as quality: classic
  tales, retold gently, hand-picked. Promise new books only when the user has confirmed a release rhythm.

## 1. The text: What, Why, Who, Features

The first screen of text (what a store shows before "more") answers What, Why and Who; the short
description already carries the strongest difference. Then the features, most important first, each a
thing the person does or gets; then prices, languages, privacy, offline.

- **Only what the live app does.** A feature still being built (a subscription, a new screen) gets its
  text written in advance as a separate version in the repo and is pushed only when it ships; the slice
  that ships it says when to switch, and the listing slice records which version is live.
- Every claim is read from the current app or the repo in the same session (strings, `AppResources.*.resx`
  for the language count, prices from the slice that ships them), never from an older listing or memory.
- Short sentences; no "best", "#1", rankings or awards; no keyword stuffing (Play suspends for it).
- Dino is for children: no "For Kids" wording on the App Store (reserved for the Kids category, off per
  Slice 291); the listing speaks to the grown-up.
- One English source per store in `docs/store-listings/` (Dino: `dino/play.md`, `dino/appstore.md`,
  `dino/microsoft.md`; RTST: `en.md`, `appstore-rtst.md`), translations in `<lang>.md` beside them, made
  from the English in the same pass (no approval of the English first).

### Selling a subscription

When an app sells auto-renewing subscriptions, the listing carries what `stores.md` (*Subscriptions*)
says each store requires: App Store metadata links to the Terms of Use (EULA) and the Privacy Policy (in
the description or the EULA and privacy fields), and the description names the plans, lengths, trial and
renewal (the sign-up screen must show them too); Microsoft's listing states the purchase types, price range
and trial terms; on Play the disclosures live in the app's offer screens, and the listing must not
contradict them. Never a price, trial or buying terms in a screenshot (Apple 2.3.7).

### Keywords

Per language and store (App Store keywords field, 100 bytes; Play and Microsoft through the text itself):
the differentiators first, in that language's own words: record, family voice, grandma, character voices,
bilingual, the language name itself ("Farsi stories", "arabiska sagor"), bedtime; generic words (kids,
books) only if bytes are left. Never repeat a word already in the app name.

### Dino: Kids Books & Stories (ages 3-9)

The public name is `publicName` in `tools/dino/public-name.json` (Slice 412); the old names "Dino Book Reader" and "Dino Reader"
never appear in a listing. The opening words of the description are the phrases parents search for ("bedtime stories for kids",
"picture books", "read aloud", "children's stories"), used naturally and never stuffed (Play suspends for stuffing); the
differentiators below follow at once.

The first screen of text says, in this order:

1. Every character has its own voice, and a parent or grandparent can record their voice as any character
   ("Grandma can be the wolf").
2. Each story comes in 15 languages, for families keeping a home language (Farsi, Arabic, Nordic...) next
   to the school language; a grown-up picks two or three story languages and the child can switch while reading.
3. No ads, no account, reads offline, recordings stay on the device.
4. Payment: **live** 4 free stories, buy each other book once and keep it; **after Slice 384 ships**
   "Subscribe for every book, or buy only the books you want and keep them", with the 7-day free trial.

Then: sentence lights up, three view modes, speed, animal sounds, night theme, backup. Play's short
description carries 1, and 2 if it fits. Versions: `dino/play.md` (live) and `dino/play-subscription.md`
(pushed when Slice 384 is live).

### Read The Stupid Text

What: reads selected or copied text aloud (Windows tray, Android, iOS) with natural voices that run on
the device. Why / Who / Features from `docs/store-listings/en.md`, rewritten in this order the next time
it is touched; step 0 runs first.

## 2. The screenshots: one difference per shot, strongest first

Each shot proves one item of the list above, in its order; the first two are what most people see. Real
app screens only. A difference with no shot is a gap: add the flow to the listing slice before pushing.

**Captions:** one short benefit line per shot (under ~40 characters, at most 20% of the image, the real
screen below it), translated with the listing; never a price, buying terms, ranking, "best", "new" or a
call to action ("Download now"); details in `stores.md` (*Captions*). The payment message lives in the
text, not in a caption.

**Screen language (user, 2026-11-16, every store and every device size):** the global listing and every
European listing show the app in **English**. The shot that proves "other languages" shows **English,
Spanish and Japanese** pages (the user's decision of 2026-11-23, Slice 433, replacing "German or French"),
never Farsi, Arabic or Chinese. A Farsi, Arabic or Chinese screen appears only in that language's
own local listing (`fa`, `ar`, `zh-*`). Other listings use the English shots with translated captions
(`docs/store-listings/screenshots/captions.json`, every listing language; `tools/emulator/store-captions.ps1`
draws them in the app's own colour and font, edge to edge, at the exact store size, and
`tools/emulator/store-size-check.ps1` must pass on every file before an upload).

**A full shelf in every shot (user, 2026-11-16):** a shot of the child's shelf or the grown-ups' pages
(Books, Store) shows enough books to fill the screen, never four lonely covers. The old shots showed four
because `skip-first-run` puts only the four test books on the shelf (`src/Dino/DinoReader.App/Testing/CLAUDE.md`).
Before the shot run, put the whole live store catalogue (all 15 books, real covers) on the shelf through a
Debug hook, never a real purchase; until that hook exists, no shelf or book shot is taken. Check each shelf
or book shot: no empty rows, no "no stories" state, only the last row may be part-filled.

### Dino (phone portrait; tablet the same screens)

| # | Screen | Caption (English) |
|---|---|---|
| 1 | A full-page picture (Three Little Pigs) | Picture books, read aloud |
| 2 | Reading page, a character's line lit | A different voice for every character |
| 3 | Record screen, "Who will you record?" | Record your voice. Be the wolf. |
| 4 | The same story page in Spanish (Japanese as an extra file) | Stories in 15 languages |
| 5 | Kids' shelf, 12 books | New books on the shelf |
| 6 | Grown-ups page (books), no prices | For ages 3 to 9. No ads. |

(Slice 433, the note `docs/plan/notes/2026-11-23-dino-store-screenshots-and-video.md`; the Play tablet set has no captions.)
Flows: `.maestro/dino/needs-file/store-shot-{phone,tablet}.yaml`, names `store-dino-<form>-<n>-<screen>`,
collected by `tools/emulator/store-screenshots.ps1` into `docs/store-listings/screenshots/dino/<form>/`.
`permissions: all: allow` at launch; the record shot is the intro page, taken before tapping Start. A Debug build sends
no usage or crashes (`worker/CLAUDE.md`); flows never tap Get, Buy or Redeem.

### RTST

Home, review, reading, settings (`.maestro/rtst/needs-file/store-shot-*.yaml`); the Windows control panel
from the VM. Same rules.

### Per store

- **Play:** phone set (2-8) at 1080x1920, tablet set (4-8) at 1440x2560 without captions; feature graphic
  1024x500 (the user asked for a new one on 2026-11-23, Slice 433: Dino plus one book picture, name centred,
  no alpha, `tools/dino/promo/store/`); otherwise it stays unless the user asks for a new one.
- **App Store:** iPhone 6.9" (1320x2868) and iPad 13" in landscape (2752x2064) from `store-screenshots.yml` (macOS simulator); same screens, plus the app preview videos (iPhone 886x1920, iPad 1600x1200).
- **Microsoft Store:** desktop shots from the Hyper-V VM (`tools/desktop-vm`).
- **Amazon Appstore / Alexa:** not live for either app; sizes in `stores.md` for when one is added.

## 3. Procedure

0. Step 0 above when it is due.
1. Read the live listing (the global `store-listing` agent, draft mode) and the app's current strings.
2. Write the English text (and any not-yet-live version) into the repo.
3. Take the shots by script (headless emulator), never by hand. Look at every shot's small copy: no
   banners, dialogs, debug marks, blank covers or prices; each shows the difference it is meant to prove.
   Check each shot's screen language against the language rule in section 2 and every shelf or book
   shot for a full screen of books; a shot that fails either is retaken, never uploaded.
4. Show the user the English text as a diff against live, plus the shot list with captions. Nothing is
   translated or pushed before their yes.
5. Translations and keywords per language, then the `store-listing` agent applies text and images and
   reads them back. Replace a store's image set only with a full set.
6. Record what went live (date, version, read-back) in the listing slice (Slice 291) and, for a feature's
   text, in that feature's slice.
