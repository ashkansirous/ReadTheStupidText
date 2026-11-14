"""Builds the sherpa-onnx engines as the apps do (see SherpaEngineFactory.cs; speaker ids, rule FST order and threads match) and lists the voices tested."""
import os
import numpy as np
import sherpa_onnx as so
import textunits

THREADS = 4


def _base_config():
    return so.OfflineTtsConfig()


def build_supertonic(root):
    cfg = _base_config()
    m = cfg.model.supertonic
    m.duration_predictor = os.path.join(root, "duration_predictor.int8.onnx")
    m.text_encoder = os.path.join(root, "text_encoder.int8.onnx")
    m.vector_estimator = os.path.join(root, "vector_estimator.int8.onnx")
    m.vocoder = os.path.join(root, "vocoder.int8.onnx")
    m.tts_json = os.path.join(root, "tts.json")
    m.unicode_indexer = os.path.join(root, "unicode_indexer.bin")
    m.voice_style = os.path.join(root, "voice.bin")
    return _finish(cfg)


def build_kokoro(root, lang):
    cfg = _base_config()
    k = cfg.model.kokoro
    k.model = os.path.join(root, "model.onnx")
    k.voices = os.path.join(root, "voices.bin")
    k.tokens = os.path.join(root, "tokens.txt")
    lex = ["lexicon-us-en.txt"] + (["lexicon-zh.txt"] if lang == "zh" else [])
    k.lexicon = ",".join(os.path.join(root, f) for f in lex)
    k.data_dir = os.path.join(root, "espeak-ng-data")
    k.lang = {"en": "", "zh": "cmn"}.get(lang, lang)
    if lang == "zh":
        k.dict_dir = os.path.join(root, "dict")
        cfg.rule_fsts = ",".join(os.path.join(root, f) for f in ("date-zh.fst", "phone-zh.fst", "number-zh.fst"))
    return _finish(cfg)


def build_melo(root):
    cfg = _base_config()
    v = cfg.model.vits
    v.model = os.path.join(root, "model.onnx")
    v.lexicon = os.path.join(root, "lexicon.txt")
    v.tokens = os.path.join(root, "tokens.txt")
    v.dict_dir = os.path.join(root, "dict")
    cfg.rule_fsts = ",".join(os.path.join(root, f) for f in ("phone.fst", "date.fst", "number.fst"))
    return _finish(cfg)


def build_piper(root, voice_folder):
    cfg = _base_config()
    v = cfg.model.vits
    v.model = os.path.join(root, "voices", voice_folder, "model.onnx")
    v.tokens = os.path.join(root, "voices", voice_folder, "tokens.txt")
    v.data_dir = os.path.join(root, "espeak-ng-data")
    return _finish(cfg)


def _finish(cfg):
    cfg.model.num_threads = THREADS
    cfg.model.provider = "cpu"
    return so.OfflineTts(cfg)


def speak_unit(tts, text, sid, supertonic_lang=None, pace=1.0):
    """One engine call: returns (samples float32 ndarray, sample_rate). `pace` is the flavour's engine speed."""
    if supertonic_lang:
        gen = so.GenerationConfig()
        gen.sid = sid
        gen.speed = pace
        gen.extra = {"lang": supertonic_lang}
        audio = tts.generate(text, gen)
    else:
        audio = tts.generate(text, sid=sid, speed=pace)
    return np.asarray(audio.samples, dtype=np.float32), audio.sample_rate


def speak(tts, text, sid, supertonic_lang=None, pace=1.0):
    """Speaks a chunk the way the app does: one engine call per unit (textunits), 150 ms of silence between units."""
    parts, rate = [], 0
    for unit in textunits.units(text):
        samples, rate = speak_unit(tts, unit, sid, supertonic_lang, pace)
        if parts:
            parts.append(np.zeros(int(rate * textunits.SENTENCE_GAP_MS / 1000), dtype=np.float32))
        parts.append(samples)
    return (np.concatenate(parts) if parts else np.zeros(0, dtype=np.float32)), rate


def voice_plan(packs):
    """Yields (language, voice label, builder thunk, sid, supertonic_lang). Engines are built lazily and reused per key."""
    sup = os.path.join(packs, "supertonic-3-3", "supertonic-3")
    kok = os.path.join(packs, "kokoro-en-2", "kokoro-en")
    melo = os.path.join(packs, "melo-zh-en-1", "melo-zh-en")
    fa = os.path.join(packs, "piper-fa-2", "piper-fa")
    plan = []
    for lang in ("en", "es"):
        plan += [(lang, f"supertonic F1", ("sup",), lambda: build_supertonic(sup), 0, lang),
                 (lang, f"supertonic M1", ("sup",), lambda: build_supertonic(sup), 5, lang)]
    plan += [("en", "kokoro heart (f)", ("kok", "en"), lambda: build_kokoro(kok, "en"), 3, None),
             ("en", "kokoro michael (m)", ("kok", "en"), lambda: build_kokoro(kok, "en"), 16, None),
             ("es", "kokoro dora (f)", ("kok", "es"), lambda: build_kokoro(kok, "es"), 28, None),
             ("es", "kokoro alex (m)", ("kok", "es"), lambda: build_kokoro(kok, "es"), 29, None),
             ("zh", "kokoro xiaoxiao (f)", ("kok", "zh"), lambda: build_kokoro(kok, "zh"), 47, None),
             ("zh", "kokoro yunxi (m)", ("kok", "zh"), lambda: build_kokoro(kok, "zh"), 50, None),
             ("zh", "melo", ("melo",), lambda: build_melo(melo), 0, None)]
    for folder in ("ganji", "ganji_adabi", "gyro", "mana"):
        plan.append(("fa", f"piper {folder}", ("fa", folder), (lambda f=folder: build_piper(fa, f)), 0, None))
    return plan
