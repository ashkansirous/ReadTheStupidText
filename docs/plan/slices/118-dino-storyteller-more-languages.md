# Slice 118 — Dino: storyteller voices for Spanish, French and Portuguese

*Built 2026-10-25, listening check and real-phone checks not yet done.* *Planned 2026-10-25.* Dino Reader only; RTST is not touched.

**Batch:** 24 (storyteller characters)
**Depends on:** Slice 116 (rosters per language; this slice moves three rosters onto Kokoro)
**Touches:** `src/ReadTheStupidText.Domain/Reading/KokoroVoiceTable.cs`, `src/ReadTheStupidText.Domain/Reading/KokoroFiles.cs`, `src/ReadTheStupidText.Application/Reading/PackManifest.cs`, `src/ReadTheStupidText.Application/Reading/PackConstants.cs`, `src/ReadTheStupidText.Infrastructure/Reading/SherpaEngineFactory.cs`, `src/ReadTheStupidText.Mobile.Shared/**/KokoroEngineConfig*.cs`, `src/ReadTheStupidText.Mobile.Shared/**/*Reader*.cs`, `.github/workflows/packs.yml`, `src/DinoReader.Core/CharacterRoster*.cs`, `src/DinoReader.App/StoryVoicing.cs`, `tests/ReadTheStupidText.Tests/**`, `tests/DinoReader.Tests/**`, `src/ReadTheStupidText.Infrastructure/CLAUDE.md`, `src/DinoReader.App/CLAUDE.md`, `.github/CLAUDE.md`

## Why

The `kokoro-en` pack is sherpa-onnx's `kokoro-int8-multi-lang-v1_0` with 53 voices, but Dino uses only
the 14 English ones (`KokoroVoiceTable.Serves`, line 38). Kokoro-82M v1.0 also has Spanish, French and
Portuguese voices (and Japanese and Chinese ones). The same pack may already cover them.

## Decisions (2026-10-25, agreed with the user)

- **Decision 124: the storyteller pack also serves Spanish, French and Portuguese, if the pack
  speaks them well.** Work item 1 checks whether the sherpa-onnx multi-lang front end pronounces
  these through the pack's espeak-ng data (the `lang` config field). If it does, those three
  rosters move from Supertonic-3 to Kokoro voices. If not, they stay on Supertonic and the slice
  records why. Japanese and Chinese move too only if the same check passes; otherwise they keep
  Supertonic-3 and `melo-zh-en`.
- The pack id may be renamed from `kokoro-en` to a language-neutral id (e.g. `kokoro`) if it serves
  more than English; installed `kokoro-en` folders keep working.
- **Coordinator decision (2026-10-25, after the item-1 findings):** Spanish, Portuguese and Chinese move to Kokoro; French stays on
  Supertonic-3 (Kokoro has one French voice, `ff_siwis`, too few for a roster); Japanese stays on Supertonic-3 (espeak's ja phonemes were dropped,
  so the Kokoro reading is garbage). The pack id stays `kokoro-en` (installed folders and the catalog entry keep working); its manifest and catalog
  languages become `en,es,pt,zh`, and an older install that lists only `en` keeps Spanish, Portuguese and Chinese on their old engines until it updates.
- Farsi, Arabic, Russian, Danish, Finnish, Swedish, Norwegian and Dutch are not covered by Kokoro and
  keep their current engines under the Slice 116 characters.

#### Design (Dino Reader Batch 24 mock, no frames, added 2026-10-25)

Source: `docs/design/dino-reader/Dino Reader Batch 24.dc.html`. The mock has no frame for this slice; its header says Slice 118 "changes no screens". The only mention is under frame 55: Spanish moves to Kokoro underneath only if this slice's listening check passes, and the narrator screen is the same either way. The mock agrees with the plan. No new strings, no open points.

## Work

- [ ] 1. `library-docs`: sherpa-onnx 1.13.3 Kokoro multi-lang `lang` handling and which languages the
  v1.0 multi-lang archive's front end supports; list the es/fr/pt/ja/zh voice ids in `voices.bin`.
  Generate one sample sentence per language and the user listens.
  **Findings (2026-10-25, sherpa-onnx 1.13.3 in a scratch venv, archive `kokoro-int8-multi-lang-v1_0`, model.int8.onnx):**
  - Front end is `kokoro-multi-lang-lexicon.cc`: Chinese runs through `lexicon-zh.txt` + the zh fsts; every other run is
    "Non-Chinese" and goes to espeak-ng (`espeak-ng-data` is in the pack, `has_espeak=1`), with the espeak voice chosen by `lang`.
    With `lang=""` espeak uses the English voice, so Spanish text is phonemised as English (different token ids; 4.2 s vs 3.5 s);
    `lang="es"|"fr"|"pt"` switches espeak to that language and gives native phonemes. Only `lexicon-us-en`, `-gb-en` and `-zh` ship:
    there is no es/fr/pt/ja lexicon, so those languages rely entirely on espeak (no dictionary overrides).
  - Speaker ids in voices.bin (README: 54 voices; ids 0-52 as published, `em_santa` = 53): es `ef_dora`=28, `em_alex`=29, `em_santa`=53;
    fr `ff_siwis`=30 (the only French voice); pt `pf_dora`=42, `pm_alex`=43, `pm_santa`=44; it `if_sara`=35, `im_nicola`=36;
    hi `hf_alpha`=31, `hf_beta`=32, `hm_omega`=33, `hm_psi`=34; ja `jf_alpha`=37, `jf_gongitsune`=38, `jf_nezumi`=39,
    `jf_tebukuro`=40, `jm_kumo`=41; zh `zf_xiaobei`=45, `zf_xiaoni`=46, `zf_xiaoxiao`=47, `zf_xiaoyi`=48, `zm_yunjian`=49,
    `zm_yunxi`=50, `zm_yunxia`=51, `zm_yunyang`=52.
  - Synthesis with `lang` set: es, fr, pt, ja, zh all produced audio. Warnings: fr skipped one phoneme (hyphen U+002D); ja skipped
    many phonemes (U+0308, U+031E: espeak's ja output, there is no Japanese dictionary frontend), so ja is doubtful. Without `lang`
    ja produced 17.4 s for a 3 s sentence (garbage). zh is the supported Chinese path and `lang` does not change it.
  - WAVs: `<scratchpad>\wav\{es,fr,pt,ja,zh}-lang.wav` (the ones to judge) and `-nolang.wav` (control: English phonemes), scratchpad =
    `C:\Users\ashka\AppData\Local\Temp\claude\D--ReadTheStupidText\79206b78-34d3-4af8-8cda-a6d0d584a09b\scratchpad`.
  - Not yet done: the user's listening check. For item 2 the engine config must set `lang` (es/fr/pt); today it is empty.
- [x] 2. `KokoroVoiceTable` voices and `Serves` for es (ef_dora 28, em_alex 29, em_santa 53), pt (pf_dora 42, pm_alex 43, pm_santa 44) and zh (8 voices,
  45-52); `VoicesFor(language)`. One Kokoro engine per language: `lang` (espeak voice, empty for English) is set per engine, Chinese adds `lexicon-zh.txt`,
  the jieba `dict` folder and the three rule FSTs (`KokoroFiles`); `EngineCache` keys a Kokoro engine by language too. Same config in
  `SherpaEngineFactory` (Infrastructure) and `KokoroEngineConfig` (Android and iOS).
- [x] 3. Rosters for es, pt (3 voices, 18 characters) and zh (8 voices, 18 characters, 3 narrators) are Kokoro's (`CharacterRosterKokoroLanguages.cs`);
  the Melo Chinese roster is gone, `melo:zh` only remains as the fallback voice (Adapt maps the roster onto it). `CharacterRoster.Choices` keeps a gender's
  only voice for its characters when that voice narrates (Spanish and Portuguese have one woman, Dora, the default narrator). The "ready" sheet and
  its sample name the narrator of the grown-up's language when the pack speaks it (`GalleryPage.StorytellerLanguage`).
- [x] 4. `packs.yml`: `KOKORO_LANGUAGES: 'en,es,pt,zh'` in pack.json and the catalog entry, the zh files copied into the pack, `kokoro_pack_version` default 2.
  Run: packs-7 released (run 40215938305; `kokoro-en-2.zip` 131,413,882 bytes, cap 150 MiB), `PackConstants.CatalogUrl` points at packs-7; self-check synthesizes every sid. Rules in the Infrastructure, App and `.github` `CLAUDE.md` files are left to `update-docs`.
- Review fixes (high pass): espeak voices are `cmn` for Chinese (espeak has no `zh`; a Latin word in a Chinese sentence failed) and `pt-br` for Portuguese (the voices are Brazilian), both self-checked in packs.yml; `melo:` ids count as pool-switch voices so a Melo Chinese cast is recast to Kokoro (and back); a pack update that adds languages recasts the library and re-raises the ready sheet; at most one Kokoro engine stays resident (the previous language is evicted) and the failed-model key includes the language; the Persian Choices change (Mana keeps the girls when she narrates) is deliberate and tested; the ready sheet names the language the installed pack speaks.
- [ ] 5. The user's listening check of es, pt and zh (WAVs of item 1), then the packs workflow run and the emulator check below.

## Verification

- `dotnet test tests/ReadTheStupidText.Tests/ReadTheStupidText.Tests.csproj` and
  `dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj`.
- Android emulator: a Spanish, French and Portuguese starter read with Kokoro characters.
- Kill criterion: a language whose Kokoro reading is worse than Supertonic-3 in the listening check
  stays on Supertonic.

**Changed while building:** the scope grew from es/fr/pt to es, pt and zh on Kokoro. fr stays on Supertonic (Kokoro has one French voice) and ja stays too (espeak phonemes are dropped). The Chinese cast moved from Melo to Kokoro: `melo:zh` casts are recast to Kokoro and back. Spanish and Portuguese girls share Dora, the only female voice and also the narrator. A language the storyteller pack serves counts as ready (no melo download). A pack v2 update recasts the library and shows the ready sheet; storyteller pack updates use `StorytellerNetworkPolicy`. Files outside Touches: `EngineCache`, `InstalledVoiceCatalog`, `IVoiceModelService`, `GalleryPage.xaml.cs`, `StorytellerDefault`, `LanguageChoice`/installer files, `CharacterRoster*`. KokoroVoiceTable: es 28/29/53, pt 42/43/44, zh 45-52 (voices.bin verified: 54 voices).

**From review:** high pass 1-9 applied; medium 1, 3, 4 applied, 2 left (correct, leak-free).

**Verified on the emulator (packs-7):** v2 picked up, ready sheet shown, es/pt/zh casts and audio (first chunk 77-136 s on the emulator CPU), en and fr still read, zh language row shows ready.

**Note (pre-existing, not changed, RTST out of scope):** RTST's shared `PackUpdatePolicy` refuses automatic pack updates whenever cellular is listed, even on Wi-Fi.

#### For CLAUDE.md

- `src/ReadTheStupidText.Infrastructure/CLAUDE.md`: Kokoro is a per-language engine (`lang` "" for en, `es`, `pt-br`, `cmn`), and only one stays resident (`EngineCache`, `ModelKey` IsKokoro plus language). Keep the three engine builders in step (`SherpaEngineFactory`, Android and iOS `KokoroEngineConfig`) and the three readers' model keys. (Slice 118: docs/plan/slices/118-dino-storyteller-more-languages.md)
- `.github/CLAUDE.md`: the `kokoro-en` v2 pack ships the zh lexicon, dict and rule fsts; the packs.yml self-check synthesizes every sid; the current packs release is packs-7 (`PackConstants.CatalogUrl`). (Slice 118: docs/plan/slices/118-dino-storyteller-more-languages.md)

#### Not yet verified

- The user's listening check of the es, pt and zh WAVs (`C:\Users\ashka\AppData\Local\Temp\claude\D--ReadTheStupidText\79206b78-34d3-4af8-8cda-a6d0d584a09b\scratchpad\wav\`).
- Real-phone speed: Kokoro is about 9x slower than Supertonic on the emulator.
- A Chinese sentence with Latin words on a device.
- The Wi-Fi plus cellular pack-update path on a device.
