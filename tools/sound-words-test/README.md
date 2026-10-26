# sound-words-test (Slice 145)

Offline harness: speaks sound-word spellings ("Dang!", "Dang! Dang! Dang!") with the apps' own voice packs and judges them
without listening: speech length against a baseline spelling, dropped words, silent gaps, and a Whisper round trip (a weak
signal). Every clip is synthesised several times (5 by default) because the engines start from noise, so a difference only
counts beyond the spread of the takes. It also speaks each sound next to a short sentence, split into units the way the app does
(`textunits.py`), and the Farsi voices at the character flavour paces. Results and verdict:
`docs/plan/notes/2026-10-26-sound-words-aloud.md`.

Nothing large is committed: packs, the Whisper model and the WAVs live outside the repo. Make the venv outside the repo too.

```bash
python -m venv <tmp>/venv && <tmp>/venv/Scripts/python -m pip install -r tools/sound-words-test/requirements.txt
# packs: gh release download packs-7 -p supertonic-3-3.zip -p kokoro-en-2.zip -p piper-fa-2.zip -p melo-zh-en-1.zip
#        unzip each into <packs>/<zip name>/   (so <packs>/kokoro-en-2/kokoro-en/model.onnx exists)
# asr:   gh release download asr-models -R k2-fsa/sherpa-onnx -p sherpa-onnx-whisper-small.tar.bz2, extract into <asr>
python tools/sound-words-test/run.py --packs <packs> --asr <asr> --out <out>/en --only en    # one process per language (and
#   --voices ganji,ganji_adabi for the Farsi half); about an hour of CPU in all for 5 takes
python tools/sound-words-test/report.py <out>/*/results.json > <out>/report.md              # the tables of the note
python tools/sound-words-test/shortlist.py <listen dest> <out>/*                             # first-take WAVs for a human listen
```

Spellings, the baseline of each sound (the first spelling), the words an ASR may write (exact tokens) and the recommended set live
in `spellings.py`; engine setup mirrors `SherpaEngineFactory.cs` in `engines.py`. Run with plain `python` (not `-I`) so the sibling
modules import. German is not tested: no shipped engine serves it. The flavour pitch shift is not simulated: it runs on finished
samples and cannot make the engine drop a word.
