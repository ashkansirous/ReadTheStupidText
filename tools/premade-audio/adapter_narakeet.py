"""Narakeet short-content API. Docs (read 2026-11-02): https://www.narakeet.com/docs/automating/text-to-speech-api/ and
https://www.narakeet.com/languages/persian-text-to-speech/ . The short API takes 1 KB of text and answers binary audio; longer text needs the polling API
(not built here: a listening paragraph fits). Voice name casing (lowercase) is unconfirmed."""
import urllib.parse

from adapter_base import Adapter, Audio, Request, Unsupported

URL = "https://api.narakeet.com/text-to-speech/mp3"
SHORT_LIMIT_BYTES = 1024


class Narakeet(Adapter):
    name = "narakeet"
    key_env = "NARAKEET_API_KEY"
    voices = ("reza", "shohreh")

    def build_request(self, text, voice, key):
        data = text.encode("utf8")
        if len(data) > SHORT_LIMIT_BYTES:
            raise Unsupported(f"text is {len(data)} UTF-8 bytes; the short API takes {SHORT_LIMIT_BYTES} (the long polling API is not built)")
        headers = {"x-api-key": key, "Content-Type": "text/plain", "accept": "application/octet-stream"}
        return Request(f"{URL}?{urllib.parse.urlencode({'voice': voice})}", headers, data)

    def parse_response(self, body, content_type):
        return Audio(body, "mp3")
