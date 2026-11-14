"""Request pacing and budget for the generator (Slice 246 A3).

`Pacer` keeps requests at most `rpm` a minute (by spacing them), at most `rpd` in any rolling 24 hours (the log is kept in `usage.json` so a later run
continues where the day left off; rolling is never looser than the vendor's calendar-day reset) and at most `max_requests` in this run.
Clock and sleep are injected so tests never wait.
"""
import json
import os
import time

SECONDS_PER_MINUTE = 60.0
SECONDS_PER_DAY = 24 * 60 * 60.0
USAGE_NAME = "usage.json"
FREE_RPM = 3  # free tier of every Gemini TTS model, read off the AI Studio Rate Limit page on 2026-11-03 (Slice 246 A0)
FREE_RPD = 10
DEFAULT_RPM = 10  # placeholder for the paid tier: read the real value off the Rate Limit page once billing is on and pass --rpm / --rpd
NO_LIMIT = 0
DEFAULT_MAX_REQUESTS = 200  # a safety cap for a run that was given no --max-requests (0 lifts it): a typo like --rpm 0 on a long book cannot run unbounded on a paid key
LOCK_NAME = ".run.lock"

REASON_RUN_BUDGET = "--max-requests budget reached"
REASON_DAY_LIMIT = "daily request limit reached"


class UsageLog:
    """Timestamps (epoch seconds) of the requests sent in the last 24 hours, saved after every request."""

    def __init__(self, path, clock=time.time):
        self.path = path
        self.clock = clock
        self.stamps = self.load()

    def load(self):
        try:
            with open(self.path, encoding="utf8") as f:
                return self.recent([float(s) for s in json.load(f)["requests"]])
        except (OSError, ValueError, KeyError, TypeError):
            return []

    def recent(self, stamps):
        cutoff = self.clock() - SECONDS_PER_DAY
        return [s for s in stamps if s > cutoff]

    def record(self):
        self.stamps = self.recent(self.stamps) + [self.clock()]
        os.makedirs(os.path.dirname(os.path.abspath(self.path)), exist_ok=True)
        temp = self.path + ".tmp"
        with open(temp, "w", encoding="utf8") as f:
            json.dump({"requests": self.stamps}, f)
        os.replace(temp, self.path)

    def count(self):
        self.stamps = self.recent(self.stamps)
        return len(self.stamps)

    def frees_at(self):
        """When the oldest counted request leaves the 24-hour window (epoch seconds), or None when there are none."""
        return min(self.stamps) + SECONDS_PER_DAY if self.stamps else None


class Pacer:
    def __init__(self, log, rpm, rpd, max_requests, clock=time.time, sleep=time.sleep):
        self.log = log
        self.rpm = rpm
        self.rpd = rpd
        self.max_requests = max_requests
        self.clock = clock
        self.sleep = sleep
        self.sent = 0
        self.last = max(log.stamps) if log.stamps else None  # the previous run's last request counts for the per-minute spacing

    def stop_reason(self):
        """Why no further request may be sent (a clean end of the run), else ''."""
        if self.max_requests != NO_LIMIT and self.sent >= self.max_requests:
            return f"{REASON_RUN_BUDGET} ({self.max_requests})"
        if self.rpd != NO_LIMIT and self.log.count() >= self.rpd:
            return f"{REASON_DAY_LIMIT} ({self.rpd} in 24 hours); the next slot opens at {time.strftime('%Y-%m-%d %H:%M:%S', time.localtime(self.log.frees_at()))} local time, run again then"
        return ""

    def wait_turn(self):
        """Sleeps until the per-minute spacing allows another request; returns the seconds slept."""
        if self.rpm == NO_LIMIT or self.last is None:
            return 0.0
        wait = self.last + SECONDS_PER_MINUTE / self.rpm - self.clock()
        if wait > 0:
            self.sleep(wait)
        return max(wait, 0.0)

    def sent_one(self):
        self.sent += 1
        self.last = self.clock()
        self.log.record()


def usage_path(out_root):
    return os.path.join(out_root, USAGE_NAME)


class RunLocked(RuntimeError):
    """Another run holds the output folder."""


class RunLock:
    """An exclusive OS lock on `<out>/.run.lock` for the life of a run, so two runs never share one usage.json and manifest. The OS drops it when the process
    dies, so a crash leaves nothing to clean up."""

    def __init__(self, out_root):
        self.path = os.path.join(out_root, LOCK_NAME)
        self.handle = None

    def __enter__(self):
        os.makedirs(os.path.dirname(os.path.abspath(self.path)), exist_ok=True)
        self.handle = open(self.path, "a+b")
        try:
            lock_file(self.handle)
        except OSError as error:
            self.handle.close()
            raise RunLocked(f"another run is using {os.path.dirname(self.path)} (lock {self.path})") from error
        return self

    def __exit__(self, *_):
        unlock_file(self.handle)
        self.handle.close()


def lock_file(handle):
    if os.name == "nt":
        import msvcrt
        msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
    else:
        import fcntl
        fcntl.flock(handle.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)


def unlock_file(handle):
    if os.name == "nt":
        import msvcrt
        handle.seek(0)
        msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)
    else:
        import fcntl
        fcntl.flock(handle.fileno(), fcntl.LOCK_UN)
