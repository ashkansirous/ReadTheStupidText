"""Shared adapter plumbing (Slice 246): one interface, `render(text, voice, out_path, style) -> Result`, for a TTS service (today only Gemini).

An adapter builds a pure `Request` (testable offline), sends it through an injectable transport (default: urllib), turns the
response into audio bytes and writes them. With no key it returns a `skipped` Result and sends nothing.
"""
import hashlib
import os
import re
import urllib.error
import urllib.request
from dataclasses import dataclass, field

STATUS_OK = "ok"
STATUS_SKIPPED = "skipped"
STATUS_ERROR = "error"

SECRETS_FILE_ENV = "PREMADE_AUDIO_SECRETS"
DEFAULT_SECRETS_FILE = "D:/secrets/readthestupidtext/secrets.env"  # the repo's folder in the secrets store (manage-secrets)
TIMEOUT_SECONDS = 120
USER_AGENT = "ReadTheStupidText-premade-audio/1.0"
ERROR_BODY_CHARS = 500
PROBE_TEXT = "settings probe text for the cache tag"
TAG_CHARS = 8
HTTP_RATE_LIMITED = 429
FATAL_HTTP_CODES = (401, 402, 403, HTTP_RATE_LIMITED)  # a bad key, no credit or a rate limit: every further sentence would fail the same way
TEXT_BODY_STARTS = (b"<", b"{", b"[")  # real wav/mp3/ogg/flac data never starts with these
RETRY_AFTER_HEADER = "Retry-After"
DAILY_QUOTA_IN_BODY = re.compile(r"PerDay", re.IGNORECASE)  # Gemini's quotaId, e.g. GenerateRequestsPerDayPerProjectPerModel-FreeTier
RETRY_DELAY_IN_BODY =re.compile(r'"retryDelay"\s*:\s*"(\d+(?:\.\d+)?)s"')


class HttpError(RuntimeError):
    """A vendor HTTP error. The message starts "HTTP <code> <reason>:" and carries the first characters of the body; `retry_after` is the wait in seconds (header, else the body's retryDelay) or None."""

    def __init__(self, code, message, retry_after=None, daily_limit=False):
        super().__init__(message)
        self.code = code
        self.retry_after = retry_after
        self.daily_limit = daily_limit  # a 429 whose quota is per day: waiting a minute cannot help


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
    code: int = 0  # the HTTP status of a failed call, else 0
    retry_after: float = None
    daily_limit: bool = False


@dataclass
class Audio:
    data: bytes
    extension: str = field(default="wav")


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
    """The real transport: returns (response bytes, content type). Never used by tests. An HTTP error raises HttpError with the vendor's response body in the message."""
    headers = {"User-Agent": USER_AGENT, **request.headers}
    req = urllib.request.Request(request.url, data=request.body, headers=headers, method=request.method)
    try:
        with urllib.request.urlopen(req, timeout=TIMEOUT_SECONDS) as response:  # noqa: S310 - https vendor URLs only
            return response.read(), response.headers.get("Content-Type", "")
    except urllib.error.HTTPError as error:
        raise http_error(error) from error


def http_error(error):
    body = error.read()[:ERROR_BODY_CHARS * 4].decode("utf8", errors="replace")
    retry = retry_after_of(error.headers.get(RETRY_AFTER_HEADER) if error.headers else None, body)
    return HttpError(error.code, f"HTTP {error.code} {error.reason}: {body[:ERROR_BODY_CHARS]}", retry, bool(DAILY_QUOTA_IN_BODY.search(body)))


def retry_after_of(header, body):
    """Seconds to wait: the Retry-After header when it is a number, else a `"retryDelay": "12s"` in the body, else None."""
    if header and header.strip().isdigit():
        return float(header.strip())
    found = RETRY_DELAY_IN_BODY.search(body)
    return float(found.group(1)) if found else None


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
    return result.status == STATUS_SKIPPED or (result.status == STATUS_ERROR and result.code in FATAL_HTTP_CODES)


class Adapter:
    """Subclasses set `name`, `key_env` and implement `build_request` and `parse_response`."""
    name = ""
    key_env = ""

    def __init__(self, environ=None, transport=None):
        self.environ = environ
        self.transport = transport or http_send

    def key(self):
        return lookup(self.key_env, self.environ)

    def missing(self):
        """Names of the env vars that are required and unset."""
        return [] if self.key() else [self.key_env]

    def settings_tag(self, voice, style=""):
        """A short tag of everything that shapes the audio besides the text (URL, headers, model, style, voice): the hash of a request built for a fixed probe text,
        so a changed model, voice or style changes the clip cache key."""
        request = self.build_request(PROBE_TEXT, voice, "", style)
        return hashlib.sha256(request.url.encode("utf8") + repr(sorted(request.headers.items())).encode("utf8") + request.body).hexdigest()[:TAG_CHARS]

    def build_request(self, text, voice, key, style=""):
        raise NotImplementedError

    def parse_response(self, body, content_type):
        raise NotImplementedError

    def render(self, text, voice, out_path, style=""):
        missing = self.missing()
        if missing:
            return Result(self.name, voice, STATUS_SKIPPED, "skipped: no key (" + ", ".join(missing) + "; searched the environment and " + secrets_path(self.environ) + ")")
        try:
            body, content_type = self.transport(self.build_request(text, voice, self.key(), style))
            audio = self.parse_response(body, content_type)
            problem = audio_problem(audio)
            if problem:
                return Result(self.name, voice, STATUS_ERROR, f"ValueError: {problem} (content type {content_type!r}); nothing saved")
            path = f"{out_path}.{audio.extension}"
            with open(path, "wb") as f:
                f.write(audio.data)
        except HttpError as error:
            return Result(self.name, voice, STATUS_ERROR, f"HttpError: {error}", code=error.code, retry_after=error.retry_after, daily_limit=error.daily_limit)
        except Exception as error:  # a vendor failure is a result, not a crash of the run
            return Result(self.name, voice, STATUS_ERROR, f"{type(error).__name__}: {error}")
        return Result(self.name, voice, STATUS_OK, "", path)
