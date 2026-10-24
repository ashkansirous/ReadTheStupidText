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
