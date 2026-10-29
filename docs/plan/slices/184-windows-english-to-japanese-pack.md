### Slice 184 — Windows: an English-to-Japanese translation pack

*In progress (reopened 2026-10-29 after the user allowed CC-BY-SA for downloaded packs). First pass closed in PR #548 with no acceptable model.*

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

#### Reopened (2026-10-29): CC-BY-SA allowed for downloaded packs

**Decision (user, 2026-10-29; lift into `docs/plan/decisions.md` as the next number in the index pass):** a model under CC-BY-SA-4.0 may ship as a **downloaded pack only** (never in the repo, the MSIX or an app package), the same scope as the espeak-ng GPL exception (Decisions 59-71, 108, 119). The pack carries the licence name, link, the author credit and a "converted to ONNX" change note. The app's code licence is unaffected, so a later move to commercial, closed-source or a private repo stays possible; anyone who gets the pack may pass the model file on (ShareAlike), which is fine since it is free on the Hub anyway. Decision 16's MIT/Apache/CC-BY list stays the rule for everything else.

**Model (changed 2026-10-29): `Mitsua/elan-mt-bt-en-ja`** (ElanMT, Marian, CC-BY-SA-4.0) at Hub revision `02c48e7031386cd2d41974b0ff1aaf52f010c5fa`, **converted to ONNX by `packs.yml` itself** because no ONNX build exists on the Hub (`optimum-cli export onnx`, task `text2text-generation-with-past`, opset 17; encoder and both decoder branches dynamic-int8 quantised with onnxruntime, then merged with `optimum.onnx.merge_decoders`, because the quantiser skips the subgraphs of an already merged decoder and leaves it ~233 MB). Tools pinned for Python 3.12: torch 2.8.0 (CPU), optimum 1.27.0, transformers 4.53.3, onnx 1.18.0, onnxruntime 1.22.0, numpy 2.5.4. The inputs `model.safetensors`, `source.spm`, `vocab.json`, `config.json` are pinned by revision and sha256; converted hashes are not reproducible, so only the size check guards the output. Table layout `convert` marks this row (enc column = sha256 of `model.safetensors`, dec column `-`). Single target, no target token. Encoder 36 MB + decoder 59 MB, zip about 68 MB.

**Why FuguMT (`staka/fugumt-en-ja`, first choice of this pass) was dropped:** it is likely trained on JParaCrawl, whose terms of use forbid any non-research use, including selling translators trained on the data. ElanMT's card says web-crawled and other machine-translated corpora are not used.

**Quality gate result (ElanMT, 2026-10-29): pass**, int8 and fp32 alike (greedy, same 7 sentences). Examples: "Where is the nearest train station?" -> 最寄りの駅はどこですか。; "I would like to order a cup of coffee and a sandwich, please." -> コーヒーとサンドイッチをお願いします。; "The new software update fixes several security problems and makes the app faster." -> 新しいソフトウェアアップデートは、いくつかのセキュリティ上の問題を修正し、アプリを高速化します。 Known behaviour: "Hello, how are you today?" -> もしもし、今日のご機嫌いか? (a stiff greeting).

**Work (second pass):**
- [x] Quality gate first: run the pinned int8 files locally with onnxruntime (as the first pass did for `Xenova/opus-mt-en-jap`) on the same everyday sentences ("Hello, how are you today?", "The meeting has been moved to Thursday afternoon.", plus 3-4 more). Accept only if the output is natural modern Japanese matching the input. If it fails, try the fp32 or fp16 files from the same revision; if those fail too, stop and record it here.
- [x] `packs.yml`: add `en-ja` to `TRANSLATION_PAIRS` and a table row (repo, revision, sha256 of `model.safetensors`, `-`, `source.spm`, `vocab.json`, `config.json`, token `-`, layout `convert`, licence `cc-by-sa-4.0`) plus a `convert_marian` conversion step; add `cc-by-sa-4.0` to the licence `case` (`https://creativecommons.org/licenses/by-sa/4.0/`); the LICENSE text must credit the real upstream (Mitsua Inc. / ElanMT, https://huggingface.co/Mitsua/elan-mt-bt-en-ja, "Credit") rather than Helsinki-NLP for this pair, and say ShareAlike applies to the model files; update any pack-count comments.
- [x] `TranslationPacks.cs`: add `"ja"` to `FromEnglish`, fix the comment; update `TranslationPacksTests` (en->ja and fr->ja now supported) and any pack counts.
- [x] Check `PackTranslator` decodes fugumt's output correctly (Japanese is in `UnspacedLanguages`; vocab/spm handling as for `en-zh`).
- [x] `STORE.md` / third-party notices: list the en-ja model and its licence wherever the other translation models are listed.
- [x] Published `packs-9` from this branch (run 40854887119; every pack at its old version, Kokoro carried over from `packs-8`; `translation-en-ja-1.zip` 65.8 MB, CI smoke: "Where is the nearest train station?" -> 最寄りの駅はどこにありますか。) and pointed `PackConstants.CatalogUrl` at it.
- **Not yet verified:** a real English-to-Japanese translation in the Windows app (download the pack, translate a paragraph in the control panel); the C# tokenizer path has not run against this model.
- (Superseded) The first quality gate in this pass passed for FuguMT; it was then dropped for the licence reason above. `PackTranslator` needs no change for the Marian layout: decoder start id = pad id 32000 is in `vocab.json`, a leading `▁` becomes a space that `Decode` trims, and `ja` is in `UnspacedLanguages` like `zh`; the ElanMT vocab has the same 32001 entries and ids.
- Also changed outside Touches: `TranslationStreamingTests` now expects the `ja` translate toggle to be available; docs (`docs/voice-packs.md`, `src/ReadTheStupidText.Infrastructure/CLAUDE.md`, `ITranslator.cs` comment, `PRIVACY.md`, `site/privacy.html`) no longer say there is no English to Japanese.
