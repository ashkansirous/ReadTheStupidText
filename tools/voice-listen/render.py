"""Slice 153: renders one short line from every English roster character to WAV (as cast, and again with no flavour) plus the
Ugly Duckling cast as the store book stores it, and writes a listening page. Local only; nothing is published.

  python tools/voice-listen/render.py --packs <packs> --roster <roster.json> --out tools/voice-listen/out

roster.json comes from (the VoiceListenExport test; no variable, no file):
  DINO_VOICE_LISTEN_OUT=<file> dotnet test tests/DinoReader.Tests/DinoReader.Tests.csproj --filter-class DinoReader.Tests.VoiceListenExport
Packs and venv as in tools/sound-words-test/README.md (supertonic-3-3 and kokoro-en-2 unzipped into <packs>/<zip name>/)."""
import argparse
import html
import json
import os
import sys
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "sound-words-test"))
import engines  # noqa: E402
import pitch  # noqa: E402

LINE = "Hello there, little friend! Shall we go down to the pond together?"
KOKORO_PREFIX = "kokoro:"
SUPERTONIC_PREFIX = "supertonic:"
# Speaker ids of KokoroVoiceTable.EnglishEntries.
KOKORO_SIDS = {"af_heart": 3, "af_bella": 2, "af_nicole": 6, "af_sky": 10, "af_nova": 7, "bf_emma": 21, "bf_lily": 23, "bf_alice": 20,
               "am_puck": 18, "am_santa": 19, "bm_fable": 25, "am_liam": 15, "am_michael": 16, "bm_daniel": 24, "af_sarah": 9, "af_kore": 5, "af_aoede": 1}
FEMALE_STYLES = 5
NO_SHAPE = {"pace": 1.0, "semitones": 0.0}


def supertonic_sid(voice_id):
    style = voice_id[len(SUPERTONIC_PREFIX):]
    return (int(style[1:]) - 1) + (0 if style[0] == "F" else FEMALE_STYLES)


def write_wav(path, samples, rate):
    pcm = (np.clip(samples, -1, 1) * 32767).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(pcm.tobytes())


class Voices:
    """The two engines, built on first use, speaking LINE in a voice with a flavour's pace and pitch."""

    def __init__(self, packs):
        self.packs = packs
        self.kok = None
        self.sup = None

    def say(self, voice_id, shape):
        if voice_id.startswith(KOKORO_PREFIX):
            if self.kok is None:
                self.kok = engines.build_kokoro(os.path.join(self.packs, "kokoro-en-2", "kokoro-en"), "en")
            tts, sid, lang = self.kok, KOKORO_SIDS[voice_id[len(KOKORO_PREFIX):]], None
        else:
            if self.sup is None:
                self.sup = engines.build_supertonic(os.path.join(self.packs, "supertonic-3-3", "supertonic-3"))
            tts, sid, lang = self.sup, supertonic_sid(voice_id), "en"
        samples, rate = engines.speak(tts, LINE, sid, lang, shape["pace"])
        return pitch.shift(samples, rate, shape["semitones"]), rate


def render_roster(voices, roster, wavs):
    for c in roster:
        samples, rate = voices.say(c["voiceId"], c)
        write_wav(os.path.join(wavs, f"{c['id']}-cast.wav"), samples, rate)
        plain, rate = voices.say(c["voiceId"], NO_SHAPE)
        write_wav(os.path.join(wavs, f"{c['id']}-plain.wav"), plain, rate)
        print("rendered", c["id"], flush=True)


def render_stored(voices, cast, shapes, wavs):
    for i, m in enumerate(cast):
        samples, rate = voices.say(m["storedVoiceId"], shapes.get(m["storedFlavour"], NO_SHAPE))
        write_wav(os.path.join(wavs, f"duckling-stored-{i}.wav"), samples, rate)
        m["storedWav"] = f"wav/duckling-stored-{i}.wav"
        print("rendered stored", m["name"], flush=True)


def audio(src):
    return f'<audio controls preload="none" src="{src}"></audio>'


def roster_row(c, ugly):
    ugly_for = ", ".join(u["name"] for u in ugly if u["rosterId"] == c["id"])
    note = f' <b class="cast">cast in the Ugly Duckling as {html.escape(ugly_for)}</b>' if ugly_for else ""
    return (f'<tr><td><b>{html.escape(c["name"])}</b>{note}<br><small>{c["id"]} - {html.escape(c["kind"])}, {html.escape(c["gender"])}</small></td>'
            f'<td>{html.escape(c["voiceId"])}<br>{html.escape(c["flavour"])} ({c["semitones"]:+g} st, pace {c["pace"]:g})</td>'
            f'<td>{audio("wav/" + c["id"] + "-cast.wav")}</td><td>{audio("wav/" + c["id"] + "-plain.wav")}</td></tr>')


def stored_row(u):
    return (f'<tr><td><b>{html.escape(u["name"])}</b></td><td>{html.escape(u["storedVoiceId"])}, {u["storedFlavour"]}</td>'
            f'<td>{audio(u["storedWav"])}</td><td>played by roster character: {html.escape(u["rosterId"] or "-")} ({html.escape(u["flavour"])})</td></tr>')


def page(roster, ugly):
    rows = "".join(roster_row(c, ugly) for c in roster)
    stored = "".join(stored_row(u) for u in ugly)
    return f"""<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Dino voices listening</title>
<style>:root{{color-scheme:light dark}}body{{font:16px system-ui;margin:16px auto;max-width:960px;padding:0 16px}}table{{border-collapse:collapse;width:100%}}
td,th{{border-bottom:1px solid #8884;padding:8px;vertical-align:middle;text-align:left}}small{{opacity:.7}}.cast{{color:#b35900}}audio{{width:240px}}</style></head><body>
<h1>Dino voices: English roster ({len(roster)} characters)</h1>
<p>Every character says the same line: <i>{html.escape(LINE)}</i> <b>As cast</b> is the voice with the flavour's pitch and pace, as the app plays it;
<b>No flavour</b> is the same voice untouched, so a robotic sound heard only in the first column comes from the flavour. Note the names that sound robotic.</p>
<table><tr><th>Character</th><th>Voice and flavour</th><th>As cast</th><th>No flavour</th></tr>{rows}</table>
<h2>The Ugly Duckling, as the store book stores it</h2>
<p>The book stores these Supertonic voices; the app plays each as the roster character on the right (its row above).</p>
<table><tr><th>Character</th><th>Stored voice</th><th>Stored cast</th><th>Roster</th></tr>{stored}</table></body></html>"""


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--packs", required=True)
    ap.add_argument("--roster", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()
    with open(args.roster, encoding="utf-8") as f:
        data = json.load(f)
    wavs = os.path.join(args.out, "wav")
    os.makedirs(wavs, exist_ok=True)
    voices = Voices(args.packs)
    render_roster(voices, data["roster"], wavs)
    shapes = {c["flavour"]: c for c in data["roster"]}
    render_stored(voices, data["uglyDuckling"], shapes, wavs)
    index = os.path.join(args.out, "index.html")
    with open(index, "w", encoding="utf-8") as f:
        f.write(page(data["roster"], data["uglyDuckling"]))
    print("page:", os.path.abspath(index))


if __name__ == "__main__":
    main()
