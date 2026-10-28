### Slice 184 — Windows: an English-to-Japanese translation pack

*Planned.*

**Touches:** `src/ReadTheStupidText.Infrastructure/Translation/TranslationPacks.cs`, `.github/workflows/packs.yml`, `tests/ReadTheStupidText.Tests/*TranslationPack*`, `STORE.md`, `docs/plan/slices/184-windows-english-to-japanese-pack.md`.

#### Why

Found while planning Slice 106 and still true on `main` 2026-10-28: `TranslationPacks.FromEnglish` (`TranslationPacks.cs:20`) lists `fr es pt zh ar ru da fi sv nb nl de` with no `ja`, and `packs.yml` (`TRANSLATION_PAIRS`, line ~161, and the model table ~726-749) has `ja-en` but no `en-ja`. So on Windows, English can't be translated into Japanese offline, though Japanese is an app language and the phone (ML Kit) can.

#### Work

- [ ] Pick an `en-ja` model the way the other pairs were picked (MarianMT/OPUS-MT family, converted the same way): check it exists, its licence (MIT/Apache/CC-BY only, no GPL; Decision 16), size and quality on a few sentences. If no acceptable model exists, stop and record that in this file instead.
- [ ] Add `en-ja` to `TRANSLATION_PAIRS` and the model table in `packs.yml` with a pinned Hub repo, revision and hashes (plus a `>>jpn<<` target token if the model needs one, as `en-zh`/`en-ar` do), and the "packs" comment count.
- [ ] Add `"ja"` to `FromEnglish`; update the TranslationPacks tests and pack counts.
- [ ] Publish a new `packs-<n>` release (standing-approved) and point the app at it.

#### Verification

- `dotnet test tests/ReadTheStupidText.Tests`.
- `packs.yml` run builds the `en-ja` pack; its hash matches.
- Windows VM: translate an English paragraph to Japanese offline in the RTST control panel; the result reads as Japanese (a native read stays open).
