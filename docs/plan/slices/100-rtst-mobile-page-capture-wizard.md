# Slice 100 — RTST phone: "Take a photo of each page", wizard page 1

*Planned 2026-10-24*, from the user's request: "The text from picture button lets people take a picture
and add more, until they are done. It should be clear that it expects multiple pages."

**Batch:** 19 (RTST phone overhaul for first-time users)
**Depends on:** Slice 99 (the **Text from pictures** button), Slice 45 (scanner), Slice 52/84 (OCR, `ScanPackFlow`)
**Touches:** `src/ReadTheStupidText.Mobile/CapturePage.xaml` (new), `src/ReadTheStupidText.Mobile/CapturePage.xaml.cs` (new), `src/ReadTheStupidText.Mobile/ReaderPage.xaml.cs`, `src/ReadTheStupidText.Mobile/AppShell.xaml.cs`, `src/ReadTheStupidText.Mobile/MauiProgram.cs`, `src/ReadTheStupidText.Mobile.Shared/Platforms/Android/AndroidDocumentScanner.cs`, `src/ReadTheStupidText.Mobile.Shared/IDocumentScanner.cs`, `src/ReadTheStupidText.Application/**/ScannedText.cs`, `src/ReadTheStupidText.Mobile/Resources/Strings/*.resx`, `src/ReadTheStupidText.Mobile/CLAUDE.md`, `tests/ReadTheStupidText.Tests/**`

## Why

Today Scan opens Google's ML Kit Document Scanner, which can take up to 20 pages, but its "add page"
control lives inside Google's own UI. The test users never found it, so they only ever captured one page.

## Decisions (2026-10-24, agreed with the user)

- **Decision 104: multi-page capture is our own screen.** **Text from pictures** opens wizard page 1,
  titled "Take a photo of each page".
  - It shows the pages captured so far as numbered thumbnails ("Page 1", "Page 2", …) and two big buttons:
    **"+ Add another page"** and **"Done – show me the text"**.
  - Each **Add** captures **one** page with the platform's existing capture: the ML Kit scanner limited to
    one page, with its auto-crop; the one-photo `MediaPicker` fallback without Play services; iOS's current
    capture. Then it comes back to this screen.
  - The first **Add** happens straight away when the screen opens, so the camera is the first thing they
    see. With zero pages, Done is disabled.
  - A thumbnail can be tapped to view it larger and remove it.
  - Pages can also be added from the photo gallery (a small "Choose from photos" link), several at once.
- **OCR runs in the background** as each page arrives, so **Done** is quick. Each thumbnail shows a small
  "reading…" mark until its text is ready. `ScanPackFlow` (asking the page language and downloading a
  pack) still runs per page as today. Pages are joined in order (`ScannedText.JoinPages`); a page that
  failed is reported by number on page 2.
- **Done** opens wizard page 2 (Slice 101) with the joined text. Until Slice 101 lands, Done puts the text
  in the main page and reads it, as Scan does today.
- **Back** from page 1 with pages captured asks "Discard these pages?" before leaving. Nothing is kept
  after the wizard ends (images stay in the cache, deleted after OCR, offline as today).

#### Design (RTST Phone Batch 19 mock, screens R4 and R5, added 2026-10-24)

Source: `docs/design/rtst-phone/RTST Phone Batch 19.dc.html`. Two frames at 360x760: three pages taken (R4, lines 106-126) and the discard dialog (R5, lines 128-146). The mock agrees with the plan. The striped thumbnails are placeholders for real page photos, not UI.

- **Page chrome.** As Slice 99: `#f4f5f9`, gradient header padding 8/18/8/4, back arrow 48x48, title "Take a photo of each page" 18/600.
- **Page 1 (R4).** Body padding 16/14/14, gap 12. Hint 15/1.45 `rgba(0,0,0,.6)`: "Add one page at a time. Tap **Done** when you have them all." ("Done" bold `#1f2027`). Thumbnail grid: 3 columns, gap 10, each aspect 3/4, radius 10, 1px `rgba(0,0,0,.08)`; label under it "Page 1", "Page 2", "Page 3" 14/600 centred, gap 6. A page still being read shows a pill at its bottom (left/right/bottom 6, height 26, radius 13, `rgba(31,32,39,.75)`, white 11.5/600) with a spinner (r 9 ring, 3px, white arc) and "reading…".
- **Buttons (R4), pinned to the bottom (`margin-top:auto`), gap 10.** "Add another page": height 60, radius 16, white, 2px `#5B57E8` border, `#4A46D6` 17/600, plus icon (stroke 2.6) left, gap 10. "Done – show me the text": height 60, radius 16, header gradient fill, white 17/600, shadow `0 4px 14px rgba(91,87,232,.32)`. "Choose from photos": text link, height 44, `#4A46D6` 15/600, centred.
- **Discard dialog (R5).** Scrim `rgba(20,22,30,.5)` over the page, card white, radius 22, padding 22/20/18, gap 12, side margin 24. Title "Discard these pages?" 20/600; body "You took 3 pages. They won’t be kept if you go back." 15.5/1.45 `rgba(0,0,0,.6)`. Buttons gap 8, each 52 high, radius 14, 16/600: "Keep taking pages" (gradient fill, white) and "Discard and go back" (no fill, `#C62828`). The back arrow is hidden behind the dialog (empty 48x48 slot).
- **Behaviour (notes).** The camera opens straight away on entry; each **Add another page** captures one page (with auto-crop) and returns here. Tap a page to see it larger and remove it. **Choose from photos** adds several at once. With no pages, Done is disabled. The discard dialog is asked only when at least one page has been taken, and the big button is the safe choice. Pictures are never kept after the wizard ends, deleted after OCR, offline.

New strings (nine locales): "Take a photo of each page", "Add one page at a time. Tap Done when you have them all.", "Page {n}", "reading…", "Add another page", "Done – show me the text", "Choose from photos", "Discard these pages?", "You took {n} pages. They won’t be kept if you go back.", "Keep taking pages", "Discard and go back".

## Work

- [ ] 1. **Smallest end-to-end:** `CapturePage` with Add (one-page scanner), the thumbnail strip, Done
  joining the OCR text into the main page; `IDocumentScanner` gains a page-limit option.
- [ ] 2. Background OCR per page with the "reading…" mark, `ScanPackFlow` per page, failed pages reported.
- [ ] 3. Remove a page, choose from photos, the discard prompt on Back, and auto-open the camera on entry.
- [ ] 4. Strings in nine languages; rule in `src/ReadTheStupidText.Mobile/CLAUDE.md` replacing the "no
  ScanPage" line (written at planning; check it still matches).

## Verification

- Unit: the page list model (add, remove, order, all-pages-done) and the join with a failed page.
- Emulator (device-test): capture three pages one by one, remove page 2, Done shows pages 1 and 3 in
  order. Back with pages asks first. Choosing two photos from the gallery adds two pages.
- iOS compile check.
