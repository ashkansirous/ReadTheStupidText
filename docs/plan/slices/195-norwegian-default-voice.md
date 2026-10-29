### Slice 195 — Pick the default Norwegian voice by ear

*Planned 2026-10-29.* Both apps (Norwegian reads use the `piper-nb` pack on Android, iOS and Windows). Gap found in Slice 104.

**Touches:** `src/ReadTheStupidText.Application/Reading/NorwegianVoices.cs`, `.github/workflows/packs.yml` (the `piper-nb` voice order), `src/DinoReader.Core/CharacterRosterTenVoice.cs`, `tests/ReadTheStupidText.Tests/**`, `tests/DinoReader.Tests/**`, `docs/plan/slices/195-norwegian-default-voice.md`.

#### Why

The default Norwegian voice is `piper:nb-knn`, chosen as a placeholder because it is first in the list. The pack has ten speakers (knn, ksv, mmn, kon, mnn, msv, mon, mnv, kmn, knv; k = woman, m = man) and nobody has listened to them.

#### Agents

The repo agent `voice-pick` builds the listening page from the `piper-nb` pack and writes the user's picks here; `implement-slice` then reorders the ids.

#### Work

- [ ] A listening page (Artifact) with the same two Norwegian sentences in each of the ten voices; the user picks the default (and, for Dino, a woman and a man voice for the narrator and lead characters).
- [ ] Reorder `NorwegianVoices.Ids` so the pick is first, and keep `packs.yml` and `CharacterRosterTenVoice` in the same order; unit tests pin the default.
- [ ] If the order in `packs.yml` changes the pack's contents, publish a new `packs-<n>` release (standing approval) and point the apps at it.

#### Verification

- Unit tests; a Norwegian read on the emulator uses the chosen voice; iOS compile here.
