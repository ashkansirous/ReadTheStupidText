# Dino YouTube read-aloud videos (Slice 435)

Renders the store's free books (read live from `https://dinoreader.sirous.uk/api/stories`, never a fixed list) as read-aloud videos,
one per book per language, 3840 x 2160, 30 fps, H.264 + AAC, straight from `content/dino/<stem>/` (read only): the page pictures
(`high-res/`), the manifests (`audio/<lang>/manifest.json`, one clip per spoken piece) and the clips, all read through Git LFS.
YouTube rules checked for this are in `docs/plan/notes/2026-11-24-youtube-kids-policy.md`.

```bash
npm ci                                    # once, in this folder (qrcode)
node --test "*.test.mjs"                  # fast tests, no Chrome/ffmpeg/network
node render.mjs --out ../../../../tmp/youtube --book lion-and-mouse --lang en            # one video
node render.mjs --out ../../../../tmp/youtube --book lion-and-mouse --lang en --max-pages 2   # a short sample
node render.mjs --out <folder>            # all free books x 15 languages: one PASS/FAIL line per video, one contact sheet per book
node upload.mjs --out <folder> --book lion-and-mouse --lang en                            # DRY RUN (the default): sends nothing
node upload.mjs --out <folder> --book lion-and-mouse --lang en --yes                      # uploads, unlisted, made for kids
```

Needs `ffmpeg`/`ffprobe` and Chrome or Edge on the PATH or in `$FFMPEG`, `$FFPROBE`, `$CHROME`. Put `--out` under the worktree's `tmp/` (git-ignored), never in an Actions artifact.

## How a video is built

`lib/pages.mjs` finds each spoken piece's page; `lib/timeline.mjs` lays out the audio (the listening track's gaps: 150 ms between pieces, 900 ms at a page turn) and one still per piece,
frame-exact from clip lengths read with ffprobe; `lib/frame.mjs` is the HTML of each still (picture, page text at 62-40 px on a 1080 px page = TV-readable, the spoken sentence on a highlighter band, as the app does);
Chrome captures each at 2x; ffmpeg turns each page into a clip with a fade through the paper colour at the page turn, joins them and adds one audio track (clips decoded to PCM and cut to their exact length).
`lib/check.mjs` then checks every highlighted sentence's on-screen text against its manifest entry, the clip played at that moment, the order, and the video's size, frame count and length.
Each language's videos carry that language's cards, title, description, playlist title and thumbnail (`lib/strings.mjs`, `lib/metadata.mjs`). Farsi text appears only in the Farsi video.

The highlight is the spoken piece (one manifest entry = one clip), the app's own sentence unit; there are no word timings.

## Spoken card lines (optional files)

The opening card says the title and the closing card says the grown-ups line when `voice/<lang>/title-<stem>.m4a` and `voice/<lang>/grown-ups.m4a` exist (made with the same Gemini TTS voice as the book's narration;
the texts are the book's title in that language and `STRINGS[lang].grownUps`). Without them the card is silent and the run line says `no spoken card lines`.

Make them (needs the Gemini key; `D:/secrets/readthestupidtext/secrets.env` or the environment, and ffmpeg):

    node voice-lines.mjs --book all --lang all > ../../../../tmp/lines.json
    python make_voice.py --lines ../../../../tmp/lines.json [--dry-run]

`voice-lines.mjs` lists the lines; `make_voice.py` speaks each with the book's narrator voice (every grown-ups line with the Lion and the Mouse narrator) through premade-audio's Gemini adapter.
An existing clip is kept, never a second paid request (delete it to remake). With no key it stops at once, and a 401/402/403/429 (daily cap) stops the run and prints how many lines are left; rerun later.
Tests (offline): `node --test` and `python -I -m unittest discover -s . -p "test_*.py"` in this folder.

## Upload

`upload.mjs` is a dry run unless `--yes`. `--authorize` is the owner's one-time sign-in as the channel's Google account (client in `D:\secrets\dinoreader\youtube-oauth-client.json`);
it saves the refresh token to `D:\secrets\dinoreader\youtube-token.json` (commit with `manage-secrets`). Uploads are unlisted and made for kids; `--privacy public` needs `--owner-approved`
(the owner's yes on the first video, recorded in Slice 435). `--ai-disclosure` sets the synthetic-media flag (default off, see the policy note).
