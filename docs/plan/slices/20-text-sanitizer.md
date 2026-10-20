# Slice 20 — Text sanitizer (redact/simplify noise)

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 20 — Text sanitizer (redact/simplify noise).** ([#102](https://github.com/ashkansirous/ReadTheStupidText/issues/102)) (Decision 28) Add
      `ITextSanitizer` (Application) + a regex rule set in Infrastructure that
      rewrites URLs → `"page on host"`, passwords/tokens → `"a password"`, emails,
      long digit runs, file paths, GUIDs/hashes, and markdown/HTML noise to short
      spoken summaries. Each category an independent **default-on** setting
      (`ISettingsStore`); wire it into `ReadAloudService` so the sanitized text is
      what gets spoken (and, later, logged). Unit-test the rules (pure logic, fits
      the existing test story). End-to-end value: selecting a URL/password reads a
      clean summary instead of gibberish.

## Verification (moved from plan.md)

- **Slice 20:** `dotnet test` — sanitizer unit tests cover each category
  (URL→`page on host`, `password=…`→`a password`, email, digit runs, file path,
  GUID/hash, markdown link/`**`). Run the app: select a URL or a `token=…` string →
  it reads the clean summary, and the activity-log row text is the redacted form.
