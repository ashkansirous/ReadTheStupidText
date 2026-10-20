# Slice 11 — Overlord voice names

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 11 — Overlord voice names.** ([#46](https://github.com/ashkansirous/ReadTheStupidText/issues/46)) (Decision 19) Rename the ten
      `DisplayName`s in `SupertonicVoiceTable` to the Overlord mapping (default
      **Momonga** = M1); leave the `supertonic:` ids and sid order untouched.
      Smallest end-to-end change — the picker/menu show the new names with no
      engine change.

## Verification (moved from plan.md)

- **Slice 11:** open the Voice picker (control panel `ComboBox` + tray submenu)
  → the ten voices read **Momonga / Demiurge / Cocytus / Sebas Tian / Pandora's
  Actor** and **Albedo / Shalltear Bloodfallen / Yuri Alpha / Lupusregina Beta /
  Narberal Gamma**, default **Momonga**; a profile that had a saved voice id
  still resolves to the same style (id unchanged); a read uses the picked voice.
