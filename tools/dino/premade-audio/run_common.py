"""Names shared by generate.py (one request per sentence) and batch.py (Batch API): the run summary, exit codes and outcome markers.

They live here so batch.py never imports generate.py: run as a script, generate.py is `__main__`, and a second import of it would create a second copy of every class.
"""

FORMAT_WAV = "wav"
OUTCOME_SILENT = "silent-clip"  # a clip that failed the silence check: recorded, the run goes on until the silent caps
MAX_CONSECUTIVE_SILENT = 3
MAX_SILENT_PER_RUN = 5
EXIT_OK = 0
EXIT_ERRORS = 1  # sentences failed and nothing was rendered, or three errors in a row
EXIT_NO_FFMPEG = 2
EXIT_NO_KEY = 3
EXIT_AUTH = 4  # 401 / 402 / 403: a bad key, no balance or no access
EXIT_ENCODE = 5  # ffmpeg failed or hung (the vendor WAV is kept and encoded on the next run, with no new request)
EXIT_RATE_LIMITED = 6  # a second 429, or one with a wait that is too long
EXIT_LOCKED = 7  # another run is using the output folder


class Summary:
    def __init__(self):
        self.rendered = 0
        self.cached = 0
        self.stopped = ""
        self.exit_code = EXIT_OK
        self.errors = []
        self.held = set()  # stems held back by an earlier run (silent, or failed in several batch results): the only missing clips a rerun may exit 0 with
        self.notes = []  # things to tell the user that are not failures (clips held back by an earlier run): they do not change the exit code

    def stop(self, reason, exit_code):
        self.stopped = reason
        self.exit_code = exit_code

    def line(self, total):
        text = f"{self.rendered} rendered, {self.cached} already had a clip, {total - self.rendered - self.cached} left"
        return text + (f"; stopped: {self.stopped}" if self.stopped else "")
