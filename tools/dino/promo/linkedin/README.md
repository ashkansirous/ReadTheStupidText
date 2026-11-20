# Dino Reader LinkedIn films

Short films for the user's LinkedIn posts, made with HyperFrames (HTML rendered to video). One folder per film; the
three storylines and their order are in `b-bedtime/STORYLINES.md`.

| Folder | Film | Slice |
| --- | --- | --- |
| `b-bedtime/` | B, "One bedtime, three languages": what Dino is, why it was built, what it does | 426 |
| (next) | C, "Five weeks, fast-forward": how it was built | 427 |
| (next) | A, "The feature I deleted" | 428 |

## Working on a film

Run the HyperFrames CLI through the plugin launcher, inside the film's folder (the `hyperframes` skill's
`references/plugin-installation.md`), for example:

```bash
node "<SKILLS>/hyperframes/scripts/plugin-cli.mjs" check --json
node "<SKILLS>/hyperframes/scripts/plugin-cli.mjs" snapshot --review
node "<SKILLS>/hyperframes/scripts/plugin-cli.mjs" preview --background
```

Each film folder holds `index.html` (the root, voices and effects), `compositions/` (one scene per file), `assets/`
(pictures, the app's clips, fonts, effects), `SPEC.md` (the shot list), `REVIEW.md` (the last review) and `POST.md`
(the post text the user posts themselves).

- Voices are the app's own pre-made clips, copied from `content/dino/<book>/audio/<lang>/` (Git LFS: read them with
  `git lfs smudge < <pointer file> > <out>`; the clip for a sentence is in that folder's `manifest.json`).
- Pictures come from `content/dino/<book>/high-res/` (LFS too), scaled to 1080 px wide.
- Screens are real: the store screenshots in `docs/store-listings/screenshots/dino/` (made by
  `tools/emulator/store-screenshots.ps1`) or an emulator `screenrecord` of the Debug app.
- Raw captures and renders go in the film's `raw/` and `renders/` folders, which git ignores.
