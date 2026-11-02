"""OpenAI gpt-4o-mini-tts. Doc (read 2026-11-02): https://developers.openai.com/api/reference/resources/audio/subresources/speech/methods/create
Shuts down 2027-01-06 per that page; Persian support is not stated there. Shipping the audio needs an AI-voice disclosure."""
import json

from adapter_base import Adapter, Audio, Request

URL = "https://api.openai.com/v1/audio/speech"
MODEL = "gpt-4o-mini-tts"
INSTRUCTIONS = "Speak Persian (Farsi) with a standard Tehrani accent, warmly and expressively, like a parent reading a picture book to a young child."


class OpenAi(Adapter):
    name = "openai-gpt-4o-mini-tts"
    key_env = "OPENAI_API_KEY"
    voices = ("coral",)

    def build_request(self, text, voice, key):
        body = {"model": MODEL, "voice": voice, "input": text, "instructions": INSTRUCTIONS, "response_format": "mp3"}
        return Request(URL, {"Authorization": f"Bearer {key}", "Content-Type": "application/json"}, json.dumps(body, ensure_ascii=False).encode("utf8"))

    def parse_response(self, body, content_type):
        return Audio(body, "mp3")
