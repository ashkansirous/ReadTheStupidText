"""Gemini TTS through the Interactions API. Doc (read 2026-11-02): https://ai.google.dev/gemini-api/docs/speech-generation
The language is auto-detected from the text (Persian listed as "Iranian Persian"); style goes in `speech_metadata.style`. The unary response is
base64 WAV (24 kHz mono s16le) in steps[].content[] with type "audio" (the docs take the last one). Not confirmed: the older generateContent shape."""
import base64
import io
import json
import wave

from adapter_base import Adapter, Audio, Request

URL = "https://generativelanguage.googleapis.com/v1beta/interactions"
MODEL = "gemini-3.8-flash-tts"
STYLE = "warm, gentle and expressive, like a parent reading a picture book to a young child"
AUDIO_TYPE = "audio"
RIFF_MAGIC = b"RIFF"
RIFF_MAGIC_LENGTH = len(RIFF_MAGIC)
PCM_CHANNELS = 1
PCM_SAMPLE_BYTES = 2
PCM_RATE = 24000


class Gemini(Adapter):
    name = "gemini-tts"
    key_env = "GEMINI_API_KEY"
    voices = ("Kore", "Leda")

    def build_request(self, text, voice, key):
        part = {"type": "text", "text": text, "annotations": [{"type": "speech_metadata", "style": STYLE}]}
        body = {"model": MODEL, "input": [{"type": "user_input", "content": [part]}],
                "response_format": {"type": "audio"}, "generation_config": {"speech_config": [{"voice": voice}]}}
        return Request(URL, {"x-goog-api-key": key, "Content-Type": "application/json"}, json.dumps(body, ensure_ascii=False).encode("utf8"))

    def parse_response(self, body, content_type):
        clips = [c["data"] for step in json.loads(body).get("steps", []) for c in step.get("content", []) if c.get("type") == AUDIO_TYPE]
        if not clips:
            raise ValueError("no audio in the response")
        return Audio(wav_bytes(base64.b64decode(clips[-1])), "wav")


def wav_bytes(data):
    """The audio as a playable WAV: data that already has a RIFF header is kept, raw 24 kHz mono s16le PCM is wrapped."""
    if data[:RIFF_MAGIC_LENGTH] == RIFF_MAGIC:
        return data
    out = io.BytesIO()
    with wave.open(out, "wb") as w:
        w.setnchannels(PCM_CHANNELS)
        w.setsampwidth(PCM_SAMPLE_BYTES)
        w.setframerate(PCM_RATE)
        w.writeframes(data)
    return out.getvalue()
