"""Renders the listening paragraph of a Dino book through every service adapter plus the piper-fa mana baseline, and writes a listening-page skeleton (Slice 246).

    python tools/premade-audio/render_all.py --out <dir> [--book content/dino/three-little-pigs] [--paragraph 0] [--piper-fa-root <piper-fa folder>]

Services without a key are listed as "skipped: no key". Nothing is sent to a vendor unless its key is set. Run with plain `python` (not -I): the
piper baseline imports tools/sound-words-test/engines.py (needs sherpa-onnx 1.13.8, numpy). --piper-fa-root may come from RTST_PIPER_FA_ROOT; it is the
folder holding voices/mana (the unzipped piper-fa pack).
"""
import argparse
import html
import json
import os
import sys
import wave

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", "sound-words-test"))

from adapter_azure import Azure  # noqa: E402
from adapter_base import STATUS_ERROR, STATUS_OK, STATUS_SKIPPED, Result  # noqa: E402
from adapter_camb import Camb  # noqa: E402
from adapter_elevenlabs import ElevenLabs  # noqa: E402
from adapter_gemini import Gemini  # noqa: E402
from adapter_narakeet import Narakeet  # noqa: E402
from adapter_openai import OpenAi  # noqa: E402
import book_text  # noqa: E402

ADAPTERS = (ElevenLabs, Gemini, Azure, OpenAi, Narakeet, Camb)
PIPER_ROOT_ENV = "RTST_PIPER_FA_ROOT"
PIPER_VOICE = "mana"
PIPER_SERVICE = "piper-fa"
DEFAULT_BOOK = os.path.join(HERE, "..", "..", "content", "dino", "three-little-pigs")
RESULTS_NAME = "results.json"
PAGE_NAME = "index.html"


def render_piper(text, root, out_stem):
    """The on-device baseline: piper-fa mana, speaker 0, as the app speaks it (engines.speak). Skipped when the pack or sherpa-onnx is absent."""
    if not root or not os.path.isdir(os.path.join(root, "voices", PIPER_VOICE)):
        return Result(PIPER_SERVICE, PIPER_VOICE, STATUS_SKIPPED, "skipped: piper-fa pack not on disk (--piper-fa-root / " + PIPER_ROOT_ENV + ")")
    try:
        import numpy as np
        import engines
        samples, rate = engines.speak(engines.build_piper(root, PIPER_VOICE), text, 0)
        path = out_stem + ".wav"
        with wave.open(path, "wb") as w:
            w.setnchannels(1)
            w.setsampwidth(2)
            w.setframerate(rate)
            w.writeframes((np.clip(samples, -1, 1) * 32767).astype("<i2").tobytes())
    except ImportError as error:
        return Result(PIPER_SERVICE, PIPER_VOICE, STATUS_SKIPPED, f"skipped: {error}")
    except Exception as error:
        return Result(PIPER_SERVICE, PIPER_VOICE, STATUS_ERROR, f"{type(error).__name__}: {error}")
    return Result(PIPER_SERVICE, PIPER_VOICE, STATUS_OK, "", path)


def safe_name(*parts):
    return "-".join("".join(c if c.isalnum() or c in "-_" else "_" for c in p) for p in parts)


def render_all(text, out_dir, piper_root, adapters=None):
    """One Result per (service, voice); keyless services are skipped, never an error."""
    os.makedirs(out_dir, exist_ok=True)
    results = [render_piper(text, piper_root, os.path.join(out_dir, safe_name(PIPER_SERVICE, PIPER_VOICE)))]
    for adapter in adapters if adapters is not None else [a() for a in ADAPTERS]:
        for voice in adapter.voices:
            results.append(adapter.render(text, voice, os.path.join(out_dir, safe_name(adapter.name, voice))))
    return results


def listening_page(text, results):
    """A static page skeleton: the paragraph, then one entry per service and voice (audio, or why it is missing). The voice-pick agent publishes it once keys exist."""
    rows = []
    for r in results:
        body = (f'<audio controls src="{html.escape(os.path.basename(r.path))}"></audio>' if r.status == STATUS_OK
                else f"<em>{html.escape(r.detail or r.status)}</em>")
        rows.append(f"<section><h2>{html.escape(r.service)} - {html.escape(r.voice)}</h2>{body}<p>Pick / notes: accent, emotion, sukun words</p></section>")
    return ('<!doctype html><meta charset="utf-8"><title>Premade audio listening test</title>'
            f'<h1>Premade audio listening test</h1><p dir="rtl" lang="fa">{html.escape(text)}</p>' + "".join(rows))


def write_outputs(text, results, out_dir):
    with open(os.path.join(out_dir, RESULTS_NAME), "w", encoding="utf8") as f:
        json.dump({"text": text, "results": [r.__dict__ for r in results]}, f, ensure_ascii=False, indent=1)
    with open(os.path.join(out_dir, PAGE_NAME), "w", encoding="utf8") as f:
        f.write(listening_page(text, results))


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    p.add_argument("--out", required=True)
    p.add_argument("--book", default=DEFAULT_BOOK)
    p.add_argument("--language", default="fa")
    p.add_argument("--paragraph", type=int, default=0)
    p.add_argument("--piper-fa-root", default=os.environ.get(PIPER_ROOT_ENV, ""))
    args = p.parse_args(argv)
    _, text = book_text.listening_paragraph(args.book, args.language, args.paragraph)
    results = render_all(text, args.out, args.piper_fa_root)
    write_outputs(text, results, args.out)
    for r in results:
        print(f"{r.service:28} {r.voice:22} {r.status:8} {r.detail}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
