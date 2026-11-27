"""Offline tests for make_voice.py: no key, no network, no ffmpeg run. python -I -W error -m unittest discover -s tools/dino/promo/youtube -p "test_*.py" """
import os
import sys
import tempfile
import unittest
from types import SimpleNamespace

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_voice  # noqa: E402

LINES = [
    {"language": "en", "key": "grown-ups", "text": "Grown-ups: more stories.", "book": None},
    {"language": "fa", "key": "title-lion-and-mouse", "text": "x", "book": "lion-and-mouse"},
]


class NoKeyAdapter:
    def render(self, text, voice, out_path, style=""):
        return SimpleNamespace(status="skipped", detail="skipped: no key (GEMINI_API_KEY)", path="")


class CountingAdapter:
    def __init__(self, result):
        self.result = result
        self.calls = 0

    def render(self, text, voice, out_path, style=""):
        self.calls += 1
        return self.result


class MakeVoiceTests(unittest.TestCase):
    def test_grown_ups_line_uses_the_lion_narrator(self):
        self.assertEqual(make_voice.book_for(LINES[0]), make_voice.GROWN_UPS_BOOK)
        self.assertEqual(make_voice.book_for(LINES[1]), "lion-and-mouse")

    def test_farsi_gets_the_accent_line_and_english_does_not(self):
        voice, style_en = make_voice.narrator_delivery("lion-and-mouse", "en")
        _, style_fa = make_voice.narrator_delivery("lion-and-mouse", "fa")
        self.assertEqual(voice, "Hali")
        self.assertTrue(style_fa.startswith(style_en))
        self.assertTrue(style_fa.endswith("standard Tehrani accent"))

    def test_target_path_is_voice_lang_key(self):
        self.assertEqual(make_voice.target_path(LINES[1], "v"), os.path.join("v", "fa", "title-lion-and-mouse.m4a"))

    def test_lines_missing_a_field_are_refused_before_any_request(self):
        make_voice.check_lines(LINES)
        for bad in ([{"language": "en", "key": "k"}], [{"key": "k", "text": "t"}], ["x"], {"a": 1}):
            with self.assertRaises(ValueError):
                make_voice.check_lines(bad)

    def test_a_per_minute_429_is_waited_out_then_the_line_is_made(self):
        limited = SimpleNamespace(status="error", detail="HTTP 429", path="", code=429, retry_after=20.0, daily_limit=False)
        ok = SimpleNamespace(status="ok", detail="", path="clip.wav", code=0)
        results = [limited, limited, ok]

        class Sequence:
            calls = 0

            def render(self, text, voice, out_path, style=""):
                self.calls += 1
                return results.pop(0)

        adapter, slept = Sequence(), []
        original = make_voice.encode.to_m4a
        make_voice.encode.to_m4a = lambda *args, **kwargs: None
        try:
            with tempfile.TemporaryDirectory() as folder:
                code = make_voice.render_line(LINES[0], adapter, "ffmpeg", lambda _: None, folder, slept.append)
        finally:
            make_voice.encode.to_m4a = original
        self.assertEqual(code, make_voice.EXIT_OK)
        self.assertEqual(adapter.calls, 3)
        self.assertEqual(slept, [20.0, 20.0])

    def test_a_per_minute_429_stops_after_three_waits_and_caps_each_wait(self):
        limited = SimpleNamespace(status="error", detail="HTTP 429", path="", code=429, retry_after=9999.0, daily_limit=False)
        adapter, slept = CountingAdapter(limited), []
        with tempfile.TemporaryDirectory() as folder:
            code = make_voice.run(LINES, adapter, "ffmpeg", log=lambda _: None, voice_folder=folder, sleep=slept.append)
        self.assertEqual(code, make_voice.EXIT_QUOTA)
        self.assertEqual(adapter.calls, make_voice.RATE_WAITS + 1)
        self.assertEqual(slept, [make_voice.MAX_WAIT_SECONDS] * make_voice.RATE_WAITS)

    def test_a_daily_quota_429_stops_at_once_without_waiting(self):
        daily = SimpleNamespace(status="error", detail="HTTP 429", path="", code=429, retry_after=30.0, daily_limit=True)
        adapter, slept = CountingAdapter(daily), []
        with tempfile.TemporaryDirectory() as folder:
            code = make_voice.run(LINES, adapter, "ffmpeg", log=lambda _: None, voice_folder=folder, sleep=slept.append)
        self.assertEqual(code, make_voice.EXIT_QUOTA)
        self.assertEqual(adapter.calls, 1)
        self.assertEqual(slept, [])

    def test_no_key_stops_at_once_and_sends_nothing(self):
        logs = []
        with tempfile.TemporaryDirectory() as folder:
            code = make_voice.run(LINES, NoKeyAdapter(), "ffmpeg", log=logs.append, voice_folder=folder)
        self.assertEqual(code, make_voice.EXIT_NO_KEY)
        self.assertEqual(len(logs), 2)
        self.assertIn("1 lines left", logs[1])

    def test_quota_error_stops_the_run_and_counts_the_lines_left(self):
        adapter = CountingAdapter(SimpleNamespace(status="error", detail="HttpError: HTTP 429", path="", code=429))
        logs = []
        with tempfile.TemporaryDirectory() as folder:
            code = make_voice.run(LINES, adapter, "ffmpeg", log=logs.append, voice_folder=folder)
        self.assertEqual(code, make_voice.EXIT_QUOTA)
        self.assertEqual(adapter.calls, 1)
        self.assertIn("1 lines left", logs[-1])

    def test_a_non_fatal_error_goes_on_to_the_next_line(self):
        adapter = CountingAdapter(SimpleNamespace(status="error", detail="HttpError: HTTP 500", path="", code=500))
        with tempfile.TemporaryDirectory() as folder:
            code = make_voice.run(LINES, adapter, "ffmpeg", log=lambda _: None, voice_folder=folder)
        self.assertEqual(code, make_voice.EXIT_FAILED)
        self.assertEqual(adapter.calls, 2)

    def test_an_encode_failure_is_a_fail_line_and_the_run_goes_on(self):
        adapter = CountingAdapter(SimpleNamespace(status="ok", detail="", path="clip.wav", code=0))
        logs = []
        original = make_voice.encode.to_m4a
        make_voice.encode.to_m4a = lambda *args, **kwargs: (_ for _ in ()).throw(make_voice.encode.EncodeError("boom"))
        try:
            with tempfile.TemporaryDirectory() as folder:
                code = make_voice.run(LINES, adapter, "ffmpeg", log=logs.append, voice_folder=folder)
        finally:
            make_voice.encode.to_m4a = original
        self.assertEqual(code, make_voice.EXIT_FAILED)
        self.assertEqual(adapter.calls, 2)
        self.assertEqual(sum(line.startswith("FAIL") for line in logs), 2)

    def test_a_silent_clip_is_requested_once_more_then_fails(self):
        adapter = CountingAdapter(SimpleNamespace(status="ok", detail="", path="clip.wav", code=0))
        original = make_voice.encode.to_m4a
        make_voice.encode.to_m4a = lambda *args, **kwargs: (_ for _ in ()).throw(make_voice.encode.SilentClipError("silent"))
        try:
            with tempfile.TemporaryDirectory() as folder:
                code = make_voice.render_line(LINES[0], adapter, "ffmpeg", lambda _: None, folder)
        finally:
            make_voice.encode.to_m4a = original
        self.assertEqual(code, make_voice.EXIT_FAILED)
        self.assertEqual(adapter.calls, make_voice.SILENT_ATTEMPTS)

    def test_dry_run_sends_nothing(self):
        logs = []
        with tempfile.TemporaryDirectory() as folder:
            self.assertEqual(make_voice.run(LINES, None, None, dry_run=True, log=logs.append, voice_folder=folder), make_voice.EXIT_OK)
        self.assertTrue(all(line.startswith("would make") for line in logs))

    def test_a_book_without_a_cast_is_a_fail_line_and_the_run_goes_on(self):
        lines = [{"language": "en", "key": "title-no-such-book", "text": "x", "book": "no-such-book"}, LINES[0]]
        for dry_run in (True, False):
            logs = []
            with tempfile.TemporaryDirectory() as folder:
                code = make_voice.run(lines, CountingAdapter(SimpleNamespace(status="skipped", detail="no key", path="")), None, dry_run=dry_run, log=logs.append, voice_folder=folder)
            self.assertTrue(logs[0].startswith("FAIL en/title-no-such-book"), logs)
            self.assertEqual(len(logs) > 1, True)
            if dry_run:
                self.assertEqual(code, make_voice.EXIT_FAILED)

    def test_an_existing_clip_is_kept_and_not_requested_again(self):
        with tempfile.TemporaryDirectory() as folder:
            path = os.path.join(folder, "en", "grown-ups.m4a")
            os.makedirs(os.path.dirname(path))
            open(path, "wb").close()
            logs = []
            code = make_voice.render_line(LINES[0], None, None, logs.append, folder)
        self.assertEqual(code, make_voice.EXIT_OK)
        self.assertTrue(logs[0].startswith("keep"))


if __name__ == "__main__":
    unittest.main()
