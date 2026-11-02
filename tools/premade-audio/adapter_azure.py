"""Azure Speech REST TTS, fa-IR. Docs (read 2026-11-02): https://learn.microsoft.com/en-us/azure/ai-services/speech-service/rest-text-to-speech
and https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support?tabs=tts (fa-IR has only Dilara and Farid, no styles)."""
from xml.sax.saxutils import escape

from adapter_base import Adapter, Audio, Request, lookup

OUTPUT_FORMAT = "audio-24khz-96kbitrate-mono-mp3"
USER_AGENT = "premade-audio"
LOCALE = "fa-IR"


class Azure(Adapter):
    name = "azure-fa-IR"
    key_env = "AZURE_SPEECH_KEY"
    extra_env = ("AZURE_SPEECH_REGION",)
    voices = ("fa-IR-DilaraNeural", "fa-IR-FaridNeural")

    def ssml(self, text, voice):
        return (f"<speak version='1.0' xml:lang='{LOCALE}'><voice xml:lang='{LOCALE}' name='{voice}'>{escape(text)}</voice></speak>")

    def build_request(self, text, voice, key):
        region = lookup("AZURE_SPEECH_REGION", self.environ)
        headers = {"Ocp-Apim-Subscription-Key": key, "Content-Type": "application/ssml+xml",
                   "X-Microsoft-OutputFormat": OUTPUT_FORMAT, "User-Agent": USER_AGENT}
        return Request(f"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1", headers, self.ssml(text, voice).encode("utf8"))

    def parse_response(self, body, content_type):
        return Audio(body, "mp3")
