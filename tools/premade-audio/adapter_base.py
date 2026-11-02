"""Shared adapter plumbing (Slice 246): one interface, `render(text, voice, out_path) -> Result`, for every TTS service.

An adapter builds a pure `Request` (testable offline), sends it through an injectable transport (default: urllib), turns the
response into audio bytes and writes them. With no key it returns a `skipped` Result and sends nothing.
"""
import hashlib
import os
import urllib.error
import urllib.request
from dataclasses import dataclass, field

STATUS_OK = "ok"
STATUS_SKIPPED = "skipped"
STATUS_ERROR = "error"
STATUS_UNSUPPORTED = "unsupported"

SECRETS_FILE_ENV = "PREMADE_AUDIO_SECRETS"
DEFAULT_SECRETS_FILE = "D:/secrets/readthestupidtext/secrets.env"  # the repo's folder in the secrets store (manage-secrets)
TIMEOUT_SECONDS = 120
USER_AGENT = "ReadTheStupidText-premade-audio/1.0"
ERROR_BODY_CHARS = 500
PROBE_TEXT = "settings probe text for the cache tag"
TAG_CHARS = 8
FATAL_HTTP_CODES = (401, 402, 403, 429)  # a bad key, no credit or a rate limit: every further sentence would fail the same way
TEXT_BODY_STARTS = (b"<", b"{", b"[")  # real mp3/wav/ogg/flac data never starts with these
FATAL_PREFIXES = tuple(f"RuntimeError: HTTP {code} " for code in FATAL_HTTP_CODES)


class Unsupported(ValueError):
    """Raised by build_request when the service cannot take this text at all."""


@dataclass
class Request:
    url: str
    headers: dict
    body: bytes
    method: str = "POST"


@dataclass
class Result:
    service: str
    voice: str
    status: str
    detail: str = ""
    path: str = ""


@dataclass
class Audio:
    data: bytes
    extension: str = field(default="mp3")


def read_secrets_file(path):
    """KEY=VALUE lines of the secrets file (comments and blanks ignored); an absent file is an empty dict."""
    values = {}
    if not os.path.isfile(path):
        return values
    with open(path, encoding="utf8") as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith("#") and "=" in line:
                name, _, value = line.partition("=")
                values[name.strip()] = value.strip().strip('"').strip("'")
    return values


def lookup(name, environ=None):
    """The value of `name`: the environment first, then the secrets file (see secrets_path)."""
    environ = os.environ if environ is None else environ
    if environ.get(name):
        return environ[name]
    return read_secrets_file(secrets_path(environ)).get(name, "")


def secrets_path(environ=None):
    """The secrets file searched: $PREMADE_AUDIO_SECRETS, else D:/secrets/readthestupidtext/secrets.env."""
    environ = os.environ if environ is None else environ
    return environ.get(SECRETS_FILE_ENV, DEFAULT_SECRETS_FILE)


def http_send(request):
    """The real transport: returns (response bytes, content type). Never used by tests. An HTTP error raises with the vendor's response body in the message."""
    headers = {"User-Agent": USER_AGENT, **request.headers}
    req = urllib.request.Request(request.url, data=request.body, headers=headers, method=request.method)
    try:
        with urllib.request.urlopen(req, timeout=TIMEOUT_SECONDS) as response:  # noqa: S310 - https vendor URLs only
            return response.read(), response.headers.get("Content-Type", "")
    except urllib.error.HTTPError as error:
        raise RuntimeError(http_error_message(error)) from error


def http_error_message(error):
    detail = error.read()[:ERROR_BODY_CHARS].decode("utf8", errors="replace")
    return f"HTTP {error.code} {error.reason}: {detail}"


def audio_problem(audio):
    """Why these bytes are not audio (empty, or an HTML/JSON/text error page sent with status 200), else ''."""
    data = audio.data
    if not data:
        return "empty audio body"
    if data.lstrip()[:1] in TEXT_BODY_STARTS:
        return "body looks like text (HTML/JSON error), not audio"
    return ""


def is_fatal(result):
    """True when retrying other sentences with this service is pointless: no key, or a 401/402/403/429 answer."""
    return result.status == STATUS_SKIPPED or (result.status == STATUS_ERROR and result.detail.startswith(FATAL_PREFIXES))


class Adapter:
    """Subclasses set `name`, `key_env`, `voices` and implement `build_request` and `parse_response`."""
    name = ""
    key_env = ""
    extra_env = ()
    voices = ()

    def __init__(self, environ=None, transport=None):
        self.environ = environ
        self.transport = transport or http_send

    def key(self):
        return lookup(self.key_env, self.environ)

    def missing(self):
        """Names of the env vars that are required and unset (the key first)."""
        return [n for n in (self.key_env, *self.extra_env) if not lookup(n, self.environ)]

    def settings_tag(self, voice):
        """A short tag of everything that shapes the audio besides the text (URL, headers such as the output format, model, style, settings): the hash of a request built for a fixed probe text,
        so a changed model or style changes the clip cache key. Empty when no request can be built."""
        try:
            request = self.build_request(PROBE_TEXT, voice, "")
        except (NotImplementedError, ValueError):
            return ""
        return hashlib.sha256(request.url.encode("utf8") + repr(sorted(request.headers.items())).encode("utf8") + request.body).hexdigest()[:TAG_CHARS]

    def build_request(self, text, voice, key):
        raise NotImplementedError

    def parse_response(self, body, content_type):
        raise NotImplementedError

    def render(self, text, voice, out_path):
        missing = self.missing()
        if missing:
            return Result(self.name, voice, STATUS_SKIPPED, "skipped: no key (" + ", ".join(missing) + "; searched the environment and " + secrets_path(self.environ) + ")")
        try:
            body, content_type = self.transport(self.build_request(text, voice, self.key()))
            audio = self.parse_response(body, content_type)
            problem = audio_problem(audio)
            if problem:
                return Result(self.name, voice, STATUS_ERROR, f"ValueError: {problem} (content type {content_type!r}); nothing saved")
            path = f"{out_path}.{audio.extension}"
            with open(path, "wb") as f:
                f.write(audio.data)
        except Unsupported as error:  # this service cannot take this text (e.g. too long): not a failure of the service
            return Result(self.name, voice, STATUS_UNSUPPORTED, f"unsupported: {error}")
        except Exception as error:  # a vendor failure is a result, not a crash of render_all
            return Result(self.name, voice, STATUS_ERROR, f"{type(error).__name__}: {error}")
        return Result(self.name, voice, STATUS_OK, "", path)
