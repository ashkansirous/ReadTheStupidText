# voice-listen (Slice 153)

Dev tool, not app code. Renders one line from every English roster character (Kokoro voices with the flavour's pitch and pace,
as cast, and again untouched) and the Ugly Duckling's stored Supertonic cast, then writes `out/index.html` to listen to. `out/` is
git-ignored; nothing is published.

```bash
# 1. roster + Ugly Duckling cast, from the code (does nothing without the variable)
DINO_VOICE_LISTEN_OUT=<abs>/tools/voice-listen/out/roster.json dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj --filter-class DinoReader.Tests.VoiceListenExport
# 2. venv and packs as in tools/sound-words-test/README.md (supertonic-3-3, kokoro-en-2); then
python tools/voice-listen/render.py --packs <packs> --roster tools/voice-listen/out/roster.json --out tools/voice-listen/out
```

`pitch.py` is a port of `PitchShifter.Shift` (same constants); engines come from `tools/sound-words-test/engines.py`.
