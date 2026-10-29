### Slice 184 — Windows: an English-to-Japanese translation pack

*Closed 2026-10-29 (PR #548): no acceptable model; nothing built. Decision for the user: allow CC-BY-SA for a downloaded pack?*

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

#### Outcome (2026-10-29): no acceptable model, nothing shipped

- [x] Model search done; `packs.yml`, `TranslationPacks.cs` and the tests are deliberately unchanged.
- **Only permissive candidate:** `Xenova/opus-mt-en-jap` (rev `9d418190be3aa945eae5bab1bd96bc5e349ad784`, ONNX of Helsinki-NLP `opus-mt-en-jap`, Apache-2.0, quantized encoder 43 MB + merged decoder 51 MB, no target token). It fits the build layout, but quality fails. Run locally with onnxruntime (greedy, int8 files) on everyday sentences it returns Bible-style, archaic, space-separated Japanese that does not match the input; its only benchmark is `bible-uedin` (BLEU 42). Examples: "Hello, how are you today?" gave "あなた は どう し て 陰府 を 立て る の か ." ("why do you set up Hades?"); "The meeting has been moved to Thursday afternoon." gave "わたし たち の 受 く べ き 日 に は 激流 さ れ て い た ." Shipping it would be badly translating, which `TranslationPacks` already avoids for Persian.
- **Better models exist but fail the licence rule (MIT/Apache/CC-BY only, Decision 16):** `staka/fugumt-en-ja` and `Mitsua/elan-mt-bt-en-ja` are CC-BY-SA-4.0 (ShareAlike). `Helsinki-NLP/opus-mt-en-mul` (Apache-2.0) is a multi-target model of far lower quality for Japanese and has no ONNX conversion.
- **To revisit:** a permissively licensed en-ja Marian/OPUS model with a general-domain benchmark, or a decision to accept CC-BY-SA for a downloaded pack (the weights are not in the repo; ask the user, it changes Decision 16's scope). Until then English to Japanese stays unsupported on Windows.
