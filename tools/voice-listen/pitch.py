"""Python port of PitchShifter.Shift (src/DinoReader.Core/PitchShifter.cs, Slice 113): resample, then WSOLA back to the original length.
Constants match the C# so a flavour sounds as in the app. The bleat wobble is not ported: no English roster flavour uses it."""
import numpy as np

MIN_SEMITONES = 0.01
FRAME_SECONDS = 0.032
SEARCH_FRACTION = 0.25
CORRELATION_STRIDE = 4
CANDIDATE_STEP = 2
MIN_FRAME = 64
NORMALISATION_FLOOR = 1e-9
WEIGHT_FLOOR = 1e-6


def shift(samples, sample_rate, semitones):
    """A shifted copy of the samples, same length (the input itself for a shift of about zero)."""
    if len(samples) < MIN_FRAME * 2 or abs(semitones) < MIN_SEMITONES:
        return samples
    ratio = 2 ** (semitones / 12)
    return _stretch(_resample(samples, ratio), sample_rate, len(samples))


def _resample(x, ratio):
    n = max(1, int(len(x) / ratio))
    pos = np.arange(n) * ratio
    idx = pos.astype(np.int64)
    frac = (pos - idx).astype(np.float32)
    nxt = x[np.minimum(idx + 1, len(x) - 1)]
    return (x[idx] + (nxt - x[idx]) * frac).astype(np.float32)


def _stretch(x, sample_rate, target):
    frame = max(MIN_FRAME, int(sample_rate * FRAME_SECONDS)) & ~1
    hop = frame // 2
    search = int(frame * SEARCH_FRACTION)
    stretch = target / len(x)
    window = (0.5 - 0.5 * np.cos(2 * np.pi * np.arange(frame) / frame)).astype(np.float32)
    total = np.zeros(target + frame, dtype=np.float32)
    weight = np.zeros(target + frame, dtype=np.float32)
    previous = 0
    k = 0
    while k * hop < target:
        nominal = int(k * hop / stretch)
        position = 0 if k == 0 else _best(x, previous + hop, nominal, search, frame)
        chunk = np.zeros(frame, dtype=np.float32)
        part = x[position:position + frame]
        chunk[:len(part)] = part
        to = k * hop
        total[to:to + frame] += chunk * window
        weight[to:to + frame] += window
        previous = position
        k += 1
    return np.where(weight[:target] > WEIGHT_FLOOR, total[:target] / np.maximum(weight[:target], WEIGHT_FLOOR), 0).astype(np.float32)


def _best(x, continuation, nominal, search, frame):
    """The candidate near the nominal position whose frame best continues the last one (normalised cross-correlation)."""
    best = min(max(nominal, 0), max(0, len(x) - 1))
    if continuation + frame > len(x):
        return best
    ref = x[continuation:continuation + frame:CORRELATION_STRIDE]
    ref_energy = float(np.dot(ref, ref))
    best_score = -np.inf
    for cand in range(nominal - search, nominal + search + 1, CANDIDATE_STEP):
        if cand < 0 or cand >= len(x) or cand + frame > len(x):
            continue
        c = x[cand:cand + frame:CORRELATION_STRIDE]
        score = float(np.dot(ref, c)) / np.sqrt(ref_energy * float(np.dot(c, c)) + NORMALISATION_FLOOR)
        if score > best_score:
            best_score, best = score, cand
    return best
