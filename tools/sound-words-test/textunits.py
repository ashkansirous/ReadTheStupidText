"""The app's text-to-units step in Python: SpeechTextChunker.SplitSentences + SentenceUnits.Split (Slice 47), so the harness
speaks exactly the strings the engine receives and puts the app's 150 ms gap between units."""
import re

MIN_UNIT_LENGTH = 15
SENTENCE_GAP_MS = 150
_CJK_BREAK = re.compile(r"([。！？]+[」』）】〉》”’\"')\]]*)")
_LATIN_BREAK = re.compile(r"(?<=[.!?؟۔])\s+|\r?\n")


def split_sentences(paragraph):
    parts = _LATIN_BREAK.split(_CJK_BREAK.sub(r"\1\n", paragraph))
    return [p.strip() for p in parts if p.strip()]


def units(chunk):
    out, pending = [], ""
    for sentence in split_sentences(chunk):
        pending = sentence if not pending else f"{pending} {sentence}"
        if len(pending) >= MIN_UNIT_LENGTH:
            out.append(pending)
            pending = ""
    if pending and out:
        out[-1] = f"{out[-1]} {pending}"
    elif pending:
        out.append(pending)
    return out or [chunk.strip()]
