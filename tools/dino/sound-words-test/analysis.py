"""Objective checks on one synthesised clip: lengths, silence, clipping, and an ASR round trip."""
import re
import unicodedata
import numpy as np
import sherpa_onnx as so

SILENCE_FLOOR = 0.015       # absolute amplitude under which a sample counts as silence
MIN_ACTIVE_MS = 120         # shorter than this the word was skipped or clipped away
GAP_MS = 250                # a silent run this long inside the clip is a gap
ASR_PAD_SECONDS = 0.4
WHISPER_LANG = {"en": "en", "es": "es", "zh": "zh", "fa": "fa"}


def measure(samples, rate):
    """Returns total, active, lead, tail and longest inner gap in ms, plus peak and clipped-sample count."""
    x = np.asarray(samples, dtype=np.float32)
    if len(x) == 0 or rate <= 0:  # the engine refused the text and returned nothing
        return dict(total=0, active=0, lead=0, tail=0, gap=0, peak=0.0, clipped=0)
    loud = np.abs(x) > SILENCE_FLOOR
    ms = 1000.0 / rate
    if not loud.any():
        return dict(total=len(x) * ms, active=0, lead=len(x) * ms, tail=0, gap=0, peak=0.0, clipped=0)
    first, last = np.argmax(loud), len(x) - 1 - np.argmax(loud[::-1])
    return dict(total=len(x) * ms, active=(last - first) * ms, lead=first * ms, tail=(len(x) - 1 - last) * ms,
                gap=_longest_gap(loud[first:last + 1]) * ms, peak=float(np.abs(x).max()),
                clipped=int((np.abs(x) >= 0.999).sum()))


def _longest_gap(loud):
    best = run = 0
    for v in loud:
        run = 0 if v else run + 1
        best = max(best, run)
    return best


def build_asr(model_dir, size, language):
    """One recognizer per language: Whisper's language guess turns a lone sound word into English or Latin letters."""
    return so.OfflineRecognizer.from_whisper(
        encoder=f"{model_dir}/{size}-encoder.int8.onnx", decoder=f"{model_dir}/{size}-decoder.int8.onnx",
        tokens=f"{model_dir}/{size}-tokens.txt", language=language, task="transcribe", num_threads=4)


def transcribe(asr_by_lang, lang, samples, rate):
    pad = np.zeros(int(rate * ASR_PAD_SECONDS), dtype=np.float32)
    stream = asr_by_lang[lang].create_stream()
    stream.accept_waveform(rate, np.concatenate([pad, np.asarray(samples, dtype=np.float32), pad]))
    asr_by_lang[lang].decode_stream(stream)
    return stream.result.text.strip()


def _normal(token):
    """Lower-case, accents off, letter runs squeezed ("Muuu" and "mu" are the same token), so a match is exact but not fussy."""
    plain = "".join(c for c in unicodedata.normalize("NFD", token.lower()) if not unicodedata.combining(c))
    return re.sub(r"(.)\1+", r"\1", plain)


def _tokens(text, lang):
    clean = re.sub(r"[^\w؀-ۿ一-鿿]+", " ", text.lower())
    return list(clean.replace(" ", "")) if lang == "zh" else clean.split()


def word_survived(text, roots, lang):
    """True when some ASR token equals one of the expected roots after normalising both (no substring, no fuzzy match)."""
    if lang == "zh":
        return any(tok in roots for tok in _tokens(text, lang))
    wanted = {_normal(r) for r in roots}
    return any(_normal(tok) in wanted for tok in _tokens(text, lang))


def letter_by_letter(text, lang):
    """English/Spanish: three or more single-letter tokens means the voice spelled the word out."""
    if lang not in ("en", "es"):
        return False
    return sum(1 for t in re.findall(r"\b\w\b", text) if t.isalpha()) >= 3


def spread(values):
    """Mean, population standard deviation, min and max of a list of numbers."""
    return dict(mean=float(np.mean(values)), sd=float(np.std(values)), lo=float(min(values)), hi=float(max(values)))
