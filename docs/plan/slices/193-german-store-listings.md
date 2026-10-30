### Slice 193 — German store listings for both apps

*Drafted 2026-10-30 (PR #589); apply waits for the user's approval of the German text.* *Planned 2026-10-29.* Store text only, no app code. Gap found in Slice 168 (both apps read German since then).

**Touches:** `docs/store-listings/de.md` (new, Read The Stupid Text), `docs/store-listings/dino/de.md` (new, Dino), `STORE.md` (where listings are kept), `docs/plan/slices/193-german-store-listings.md`.

#### Why

Neither app has a German listing on Google Play or the App Store, and Read The Stupid Text has none on the Microsoft Store. Dino's Microsoft Store listing already has German (first submission, 2026-10-29).

#### Claim notes (2026-10-30)

- User, 2026-10-30: draft only tonight; nothing goes live until the user approves the German text.

#### Agents

The `store-listing` agent (global, Sonnet): **draft** writes both German files and diffs them against the live listings; after the user approves, **apply** pushes them through the Play Developer API and the App Store Connect API (keys in `D:\secrets`) and Partner Center (Chrome up to the final Submit if no API path).

#### Work

- [x] Write the German listing for Read The Stupid Text from `docs/store-listings/en.md` (title, short and full description, keywords, what's new). Drafted in `docs/store-listings/de.md`, 2026-10-30.
- [x] Write the German listing for Dino from its live Play listing text, updated for today (15 languages, ages 3-9). Drafted in `docs/store-listings/dino/de.md`, 2026-10-30; Dino's Microsoft Store German listing is already live and stays untouched.
- [ ] Fill them in the user's signed-in consoles: Play (both apps), App Store Connect (both iOS apps), Microsoft Partner Center (Read The Stupid Text). The user presses Save/Submit where a console asks for it.

#### Draft result (2026-10-30)

Live read (read-only): Play has only `en-US` (RTST) and `en-GB` (Dino); App Store Connect has only `en-US` for both, version 1.0 in `PREPARE_FOR_SUBMISSION` with empty description and keywords (German can be staged there); Microsoft Store: Dino already has a live German listing, RTST has only the stale neutral English one. **Apply waits for the user's approval.**

#### Not yet verified

- Native German read of both files (machine-written). Apply to Play, App Store Connect and Partner Center not done.

#### Verification

- Each console shows a German listing; the public Play and App Store pages in German show the new text after review.
- Machine-written: a native German read stays on *Outstanding work*.
