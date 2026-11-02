"""CAMB.AI MARS8 streaming TTS. Doc (read 2026-11-02): https://docs.camb.ai/api-reference/endpoint/create-tts-stream.md
The body of the response is the audio (no task id). text must be 3-3000 characters; voice_id is an integer from /list-voices. Not confirmed:
that fa-ir works on every speech_model (a 422 lists the allowed locales)."""
import json

from adapter_base import Adapter, Audio, Request, Unsupported

URL = "https://client.camb.ai/apis/tts-stream"
LANGUAGE = "fa-ir"
SPEECH_MODEL = "mars-8.1-flash-beta"
DEFAULT_VOICE_ID = "147320"
MIN_CHARS, MAX_CHARS = 3, 3000


class Camb(Adapter):
    name = "camb-mars8"
    key_env = "CAMB_API_KEY"
    voices = (DEFAULT_VOICE_ID,)

    def build_request(self, text, voice, key):
        if not MIN_CHARS <= len(text) <= MAX_CHARS:
            raise Unsupported(f"text length {len(text)} is outside {MIN_CHARS}-{MAX_CHARS}")
        body = {"text": text, "language": LANGUAGE, "voice_id": int(voice), "speech_model": SPEECH_MODEL,
                "output_configuration": {"format": "mp3", "sample_rate": 24000}, "voice_settings": {"speaking_rate": 1}}
        return Request(URL, {"x-api-key": key, "Content-Type": "application/json"}, json.dumps(body, ensure_ascii=False).encode("utf8"))

    def parse_response(self, body, content_type):
        return Audio(body, "mp3")
