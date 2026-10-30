### Slice 195 — Pick the default Norwegian voice by ear

*Built (wave 2026-10-30), PR #592.* *Planned 2026-10-29.* Both apps (Norwegian reads use the `piper-nb` pack on Android, iOS and Windows). Gap found in Slice 104.

**Touches:** `src/ReadTheStupidText.Application/Reading/NorwegianVoices.cs`, `.github/workflows/packs.yml` (the `piper-nb` voice order), `src/DinoReader.Core/CharacterRosterTenVoice.cs`, `tests/ReadTheStupidText.Tests/**`, `tests/DinoReader.Tests/**`, `docs/plan/slices/195-norwegian-default-voice.md`.

#### Why

The default Norwegian voice is `piper:nb-knn`, chosen as a placeholder because it is first in the list. The pack has ten speakers (knn, ksv, mmn, kon, mnn, msv, mon, mnv, kmn, knv; k = woman, m = man) and nobody has listened to them.

#### Claim notes (2026-10-30)

- User, 2026-10-30: "You pick". The coordinator picks from measured candidates (no human ear tonight) and records it as the coordinator's pick; the listening page stays up so the user can change it.

#### Agents

The repo agent `voice-pick` builds the listening page from the `piper-nb` pack and writes the user's picks here; `implement-slice` then reorders the ids.

#### Work

- [x] A listening page (Artifact) with the same two Norwegian sentences in each of the ten voices; the user picks the default (and, for Dino, a woman and a man voice for the narrator and lead characters).
- [x] Reorder `NorwegianVoices.Ids` so the pick is first, and keep `packs.yml` and `CharacterRosterTenVoice` in the same order; unit tests pin the default.
- [x] If the order in `packs.yml` changes the pack's contents, publish a new `packs-<n>` release (standing approval) and point the apps at it. Not needed: `packs.yml` is unchanged.

#### Verification

- Unit tests; a Norwegian read on the emulator uses the chosen voice; iOS compile here.

#### Decisions (coordinator's pick, measured, not heard, 2026-10-30)

Listening page (change the pick here): https://claude.ai/artifact/MzbkLDQjGvihQa7dcWrteL

Method: `piper-nb-2` pack (packs-10), `engines.build_piper`, sherpa-onnx 1.13.3, two lines (a plain everyday sentence and a children's-story line with dialogue), 4 takes each, round-tripped through Whisper small (`language=no`). Score = mean word error rate over both lines and all takes, lower is better. Whisper small is a weak Norwegian recogniser (even the best voice scores ~40%), so differences under about 5 points are noise. Speaking rate is 20-25 characters per second for all; no clipping, no silence problems (lead gap under 0.2 s, tail under 0.25 s). Raw output peaks are low (0.03-0.11); `kon` and `mon` are quietest at 0.03, so they would need gain in the app.

| Voice | Sex | Mean WER | Chars/s | Raw peak |
| --- | --- | --- | --- | --- |
| knv | woman | 40% | 21.6 | 0.09 |
| kmn | woman | 40% | 20.8 | 0.10 |
| mmn | man | 43% | 22.8 | 0.09 |
| mnn | man | 49% | 20.4 | 0.09 |
| knn | woman | 54% | 21.4 | 0.09 |
| mnv | man | 54% | 24.9 | 0.11 |
| msv | man | 57% | 23.2 | 0.08 |
| ksv | woman | 58% | 21.9 | 0.07 |
| kon | woman | 59% | 25.0 | 0.03 |
| mon | man | 69% | 25.0 | 0.03 |

Recommendation (coordinator's pick, measured, not heard):
- Default Norwegian voice: `piper:nb-knv` (best score, and equally good on both lines; `kmn` ties on average but is worse on the story line). The current placeholder `knn` is mid-table.
- Dino woman (narrator and lead): `piper:nb-knv` (alternative `kmn`).
- Dino man: `piper:nb-mmn` (best man; `mon`, `kon` and `msv` are the weakest).
- Not yet confirmed by an ear; the user may change any of these on the page.

#### Decision: Dino split (2026-10-30, caller)

Default Norwegian voice `piper:nb-knv`. Dino: narrator (granny, F1) `nb-knv`, woman lead (mummy, F2) `nb-kmn`, man lead and narrator (grandad, M1) `nb-mmn`. To make room, `knn` and `ksv` moved to F4 and F5. `NorwegianVoices.Ids` starts with knv; each voice keeps its upstream speaker id. The shown voice number (position) of the Norwegian voices shifts by one.

packs.yml order unchanged on purpose: the order lives in NorwegianVoices.Ids; no packs release.

Known behaviour: Dino keeps the pre-195 rule that every language maps a stored narrator outside the roster to the roster narrator of the same gender, so the Narrator page always has a selected row. A stored Norwegian narrator `nb-knn` (the old default, almost never an explicit pick) therefore now maps to the roster default `nb-knv`.

#### Changed while building

The order lives in code, not in the pack: `NorwegianVoices.Ids` plus `InCodeOrder`, applied in `VoiceModelPaths.AvailableVoices`. The default, the Dino narrator and the picker follow it with the existing upstream-order pack. `packs.yml` is unchanged and there is no packs release. Stored picks keep their string ids.

#### From review

- High pass: 5 of 5 findings applied.
- Medium pass: 1 applied, 1 not possible (`StoryVoicing` is MAUI-only).

#### Verified

- RTST 1665 and Dino 1804 tests pass; both Android builds; both iOS compiles.
- S21 (arm64 .dbg, 2026-10-30): with no saved voice, switching RTST to Norsk downloaded piper-nb; the read hook logged `first unit (read, voice piper:nb-knv)`, then READ_START/READ_END; the picker lists voice 1 (knv) first, checked. Dino narrator on device not run.

#### For CLAUDE.md (lifted 2026-10-30)

- The Norwegian voice order lives in code (`NorwegianVoices.Ids` and `InCodeOrder`, applied in `VoiceModelPaths.AvailableVoices`), not in the `piper-nb` pack order in `packs.yml`; reorder it there and no packs release is needed. (Slice 195: docs/plan/slices/195-norwegian-default-voice.md)

#### Not yet verified

- Hearing the picks: the default `nb-knv` is measured, not heard; the user may change it on the listening page.
- iOS TestFlight run of the new order.
- Windows head.
- Dino narrator on a device (the S21 run covered RTST only).
