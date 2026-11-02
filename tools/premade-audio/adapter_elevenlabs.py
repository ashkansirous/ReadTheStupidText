"""ElevenLabs v3. Docs (read 2026-11-02): https://elevenlabs.io/docs/api-reference/text-to-speech/convert and https://elevenlabs.io/docs/overview/models"""
import json

from adapter_base import Adapter, Audio, Request

MODEL_ID = "eleven_v3"
LANGUAGE_CODE = "fa"
OUTPUT_FORMAT = "mp3_44100_128"
# A premade ElevenLabs voice as a placeholder; the listening test picks real Persian-capable voices per account.
DEFAULT_VOICE_ID = "21m00Tcm4TlvDq8ikWAM"
MAX_CHARS = 5000


class ElevenLabs(Adapter):
    name = "elevenlabs-v3"
    key_env = "ELEVENLABS_API_KEY"
    voices = (DEFAULT_VOICE_ID,)

    def build_request(self, text, voice, key):
        body = {"text": text, "model_id": MODEL_ID, "language_code": LANGUAGE_CODE,
                "voice_settings": {"stability": 0.5, "similarity_boost": 0.75, "style": 0, "speed": 1, "use_speaker_boost": True}}
        url = f"https://api.elevenlabs.io/v1/text-to-speech/{voice}?output_format={OUTPUT_FORMAT}"
        return Request(url, {"xi-api-key": key, "Content-Type": "application/json"}, json.dumps(body, ensure_ascii=False).encode("utf8"))

    def parse_response(self, body, content_type):
        return Audio(body, "mp3")
