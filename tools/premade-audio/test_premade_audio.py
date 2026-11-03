"""Offline tests for the Gemini clip generator (Slice 246 A1-A4). No network: a fake transport stands in for the vendor.

    python -I -W error -m unittest discover -s tools/premade-audio
"""
import base64
import contextlib
import io
import json
import os
import pathlib
import struct
import subprocess
import tempfile
import unittest
import unittest.mock
import urllib.error
import wave

import adapter_base
import audio_cast
import clip_manifest
import encode
import generate
import pacing
from adapter_base import HttpError
from adapter_gemini import Gemini, wav_bytes
from clip_manifest import Manifest, text_hash

REPO = pathlib.Path(__file__).resolve().parents[2]
KEY = {"GEMINI_API_KEY": "test-key"}
NARRATOR = {"voice": "Hali", "style": "warm storyteller"}
CAST_DATA = {"version": 1, "narrator": NARRATOR,
             "characters": {"Wolf": {"voice": "Elio", "style": "deep", "direction": "sly"}, "Pip": {"voice": "Cleo", "style": "bouncy"}},
             "lines": [{"direction": "huffing", "speaker": "Wolf", "source": "y"}]}


def checked(cast, sentences, language="fa"):
    """The cast bound to a dump, as generate.main does before any request."""
    cast.check(sentences, language)
    return cast


def tone_wav(seconds=0.5, silence=0.3):
    """A WAV: `silence` s of silence, `seconds` of a 440 Hz-ish square tone, `silence` s of silence (24 kHz mono 16-bit)."""
    rate = 24000
    quiet = b"\x00\x00" * int(rate * silence)
    tone = b"".join(struct.pack("<h", 8000 if (i // 27) % 2 else -8000) for i in range(int(rate * seconds)))
    out = io.BytesIO()
    with wave.open(out, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(quiet + tone + quiet)
    return out.getvalue()


def gemini_body(wav=None):
    data = base64.b64encode(wav or tone_wav()).decode()
    return json.dumps({"steps": [{"type": "model_output", "content": [{"type": "audio", "data": data}]}]}).encode()


class FakeTransport:
    """Answers each call from `answers` (a list of bytes or exceptions; the last repeats) and records the requests."""

    def __init__(self, answers=None):
        self.answers = answers or [gemini_body()]
        self.requests = []

    def __call__(self, request):
        self.requests.append(request)
        answer = self.answers[min(len(self.requests) - 1, len(self.answers) - 1)]
        if isinstance(answer, Exception):
            raise answer
        return answer, "application/json"


class Clock:
    def __init__(self):
        self.now = 1000.0
        self.slept = []

    def __call__(self):
        return self.now

    def sleep(self, seconds):
        self.slept.append(seconds)
        self.now += seconds


def sentence(index, text, speaker=None):
    return {"index": index, "sentence": index, "text": text, "source": text, "speaker": speaker, "narrator": speaker is None}


def http_error(code, retry_header=None, body="{}", daily=False):
    return HttpError(code, f"HTTP {code} X: {body}", adapter_base.retry_after_of(retry_header, body), daily)


class Base(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.tmp = self._tmp.name
        self.clock = Clock()

    def make(self, transport=None, rpm=0, rpd=0, max_requests=0, fmt=generate.FORMAT_WAV, ffmpeg=None, cast=None):
        self.transport = transport or FakeTransport()
        adapter = Gemini(environ=KEY, transport=self.transport)
        log = pacing.UsageLog(os.path.join(self.tmp, "usage.json"), clock=self.clock)
        pacer = pacing.Pacer(log, rpm, rpd, max_requests, clock=self.clock, sleep=self.clock.sleep)
        folder = os.path.join(self.tmp, "book", "fa")
        return generate.Generator(adapter, cast or audio_cast.AudioCast(dict(CAST_DATA, lines=[])), folder, pacer, "book", "fa", fmt, ffmpeg, log=lambda *_: None)


class GeminiTests(unittest.TestCase):
    def test_request_has_style_voice_and_plain_text(self):
        request = Gemini(environ=KEY).build_request("سلام", "Hali", "k", "warm")
        body = json.loads(request.body)
        part = body["input"][0]["content"][0]
        self.assertEqual("سلام", part["text"])
        self.assertEqual([{"type": "speech_metadata", "style": "warm"}], part["annotations"])
        self.assertEqual([{"voice": "Hali"}], body["generation_config"]["speech_config"])
        self.assertEqual("k", request.headers["x-goog-api-key"])

    def test_no_style_means_no_annotation(self):
        part = json.loads(Gemini(environ=KEY).build_request("a", "Hali", "k").body)["input"][0]["content"][0]
        self.assertNotIn("annotations", part)

    def test_tag_follows_voice_and_style(self):
        g = Gemini(environ=KEY)
        self.assertEqual(g.settings_tag("Hali", "a"), g.settings_tag("Hali", "a"))
        self.assertNotEqual(g.settings_tag("Hali", "a"), g.settings_tag("Hali", "b"))
        self.assertNotEqual(g.settings_tag("Hali", "a"), g.settings_tag("Cleo", "a"))

    def test_raw_pcm_is_wrapped_and_riff_kept(self):
        self.assertEqual(b"RIFF", wav_bytes(b"\x00\x01" * 100)[:4])
        riff = tone_wav()
        self.assertEqual(riff, wav_bytes(riff))

    def test_missing_key_is_skipped_and_sends_nothing(self):
        transport = FakeTransport()
        with tempfile.TemporaryDirectory() as tmp:
            result = Gemini(environ={adapter_base.SECRETS_FILE_ENV: os.path.join(tmp, "none.env")}, transport=transport).render("a", "Hali", os.path.join(tmp, "x"))
        self.assertEqual(adapter_base.STATUS_SKIPPED, result.status)
        self.assertEqual([], transport.requests)

    def test_http_error_carries_code_and_retry_after(self):
        error = urllib.error.HTTPError("u", 429, "Too Many", {"Retry-After": "17"}, io.BytesIO(b'{"error": "slow"}'))
        converted = adapter_base.http_error(error)
        self.assertEqual((429, 17.0), (converted.code, converted.retry_after))
        body_only = urllib.error.HTTPError("u", 429, "Too Many", {}, io.BytesIO(b'{"details":[{"retryDelay": "12s"}]}'))
        self.assertEqual(12.0, adapter_base.http_error(body_only).retry_after)
        error.close()
        body_only.close()

    def test_error_page_with_status_200_is_an_error_and_saves_nothing(self):
        with tempfile.TemporaryDirectory() as tmp:
            transport = FakeTransport([b'{"steps": []}'])
            result = Gemini(environ=KEY, transport=transport).render("a", "Hali", os.path.join(tmp, "x"))
            self.assertEqual(adapter_base.STATUS_ERROR, result.status)
            self.assertEqual([], os.listdir(tmp))


class CastTests(unittest.TestCase):
    def test_narrator_character_and_direction_precedence(self):
        dump = [sentence(1, "z", "Wolf"), sentence(3, "y", "Wolf")]
        cast = checked(audio_cast.AudioCast(CAST_DATA), dump)
        narration = cast.delivery(sentence(0, "x"))
        self.assertEqual(("Hali", "warm storyteller"), (narration.voice, narration.prompt))
        self.assertEqual("deep; sly", cast.delivery(dump[0]).prompt)
        self.assertEqual("deep; huffing", cast.delivery(dump[1]).prompt)  # the line's direction beats the character's
        by_hash = dict(CAST_DATA, directions={text_hash("y"): "whispering"})
        with self.assertRaises(audio_cast.CastError):
            checked(audio_cast.AudioCast(by_hash), dump)  # hash and line entry on one piece: no silent winner
        only_hash = dict(by_hash, lines=[])
        self.assertEqual("deep; whispering", checked(audio_cast.AudioCast(only_hash), dump).delivery(dump[1]).prompt)

    def test_unknown_speaker_and_bad_keys_fail_before_any_request(self):
        with self.assertRaises(audio_cast.CastError):
            audio_cast.AudioCast(CAST_DATA).check([sentence(0, "x", "Troll")], "fa")
        for bad_directions in ({"abc": "x"}, {"3": "huffing"}):  # an index key is no longer a thing
            with self.assertRaises(audio_cast.CastError):
                audio_cast.AudioCast(dict(CAST_DATA, directions=bad_directions))
        with self.assertRaises(audio_cast.CastError):
            audio_cast.AudioCast(dict(CAST_DATA, narrator={"voice": "Hali"}))

    def test_a_line_entry_that_matches_no_piece_fails_before_any_request(self):
        cast = audio_cast.AudioCast(CAST_DATA)
        checked(cast, [sentence(3, "y", "Wolf")])
        for moved in ([sentence(3, "other", "Wolf")], [sentence(3, "y", "Pip")], []):
            with self.assertRaises(audio_cast.CastError):
                cast.check(moved, "fa")

    def test_a_line_entry_follows_its_line_to_any_position(self):
        cast = audio_cast.AudioCast(CAST_DATA)
        for index in (3, 9):
            self.assertEqual("deep; huffing", checked(cast, [sentence(index, "y", "Wolf")]).delivery(sentence(index, "y", "Wolf")).prompt)

    def test_occurrence_picks_the_nth_repeat_and_is_required_for_repeats(self):
        repeats = [sentence(0, "y", "Wolf"), sentence(1, "y", "Wolf"), sentence(2, "y", "Wolf")]
        entry = {"direction": "huffing", "speaker": "Wolf", "source": "y"}
        with self.assertRaises(audio_cast.CastError):
            checked(audio_cast.AudioCast(dict(CAST_DATA, lines=[entry])), repeats)  # three matches, no occurrence
        cast = checked(audio_cast.AudioCast(dict(CAST_DATA, lines=[dict(entry, occurrence=2)])), repeats)
        self.assertEqual(["deep; sly", "deep; huffing", "deep; sly"], [cast.delivery(s).prompt for s in repeats])
        with self.assertRaises(audio_cast.CastError):
            checked(audio_cast.AudioCast(dict(CAST_DATA, lines=[dict(entry, occurrence=4)])), repeats)
        with self.assertRaises(audio_cast.CastError):
            audio_cast.AudioCast(dict(CAST_DATA, lines=[dict(entry, occurrence=0)]))

    def test_one_field_of_a_piece_cannot_be_set_twice(self):
        entry = {"direction": "huffing", "speaker": "Wolf", "source": "y"}
        with self.assertRaises(audio_cast.CastError):
            checked(audio_cast.AudioCast(dict(CAST_DATA, lines=[entry, dict(entry, direction="other")])), [sentence(3, "y", "Wolf")])

    def test_a_say_applies_only_to_its_language_and_needs_one(self):
        entry = {"say": "سِه", "lang": "fa", "speaker": None, "source": "x"}
        with self.assertRaises(audio_cast.CastError):
            audio_cast.AudioCast(dict(CAST_DATA, lines=[{"say": "s", "speaker": None, "source": "x"}]))
        cast = audio_cast.AudioCast(dict(CAST_DATA, lines=[entry]))
        self.assertEqual("سِه", checked(cast, [sentence(0, "x")], "fa").delivery(sentence(0, "x")).say)
        self.assertEqual("", checked(cast, [sentence(0, "x")], "nb").delivery(sentence(0, "x")).say)  # not applied to another language
        with self.assertRaises(audio_cast.CastError):
            checked(cast, [sentence(0, "other")], "nb")  # but still matched, so a stale entry fails in every language

    def test_a_line_with_direction_and_say_keeps_its_direction_in_other_languages(self):
        entry = {"speaker": None, "source": "x", "direction": "d", "say": "y", "lang": "fa"}
        cast = audio_cast.AudioCast(dict(CAST_DATA, lines=[entry]))
        self.assertEqual("warm storyteller; d", checked(cast, [sentence(0, "x")], "nb").delivery(sentence(0, "x")).prompt)
        self.assertEqual("warm storyteller; d", checked(cast, [sentence(0, "x")], "fa").delivery(sentence(0, "x")).prompt)
        self.assertEqual("y", cast.delivery(sentence(0, "x")).say)

    def test_delivery_before_check_and_a_malformed_language_fail_loudly(self):
        cast = audio_cast.AudioCast(CAST_DATA)
        with self.assertRaises(audio_cast.CastError):
            cast.delivery(sentence(3, "y", "Wolf"))
        for bad in (None, "FA", "fa-IR", ""):
            with self.assertRaises(audio_cast.CastError):
                cast.check([sentence(3, "y", "Wolf")], bad)
        with self.assertRaises(audio_cast.CastError):
            audio_cast.AudioCast(dict(CAST_DATA, lines=[{"say": "s", "lang": "FA", "speaker": None, "source": "x"}]))

    def test_a_line_entry_cannot_target_an_animal_sound(self):
        sound = dict(sentence(4, "Oink!", "Pip"), source="Oink!", sound=True)
        cast = audio_cast.AudioCast(dict(CAST_DATA, lines=[{"speaker": "Pip", "source": "Oink!", "direction": "squeal"}]))
        with self.assertRaises(audio_cast.CastError):
            checked(cast, [sound])

    def test_a_spoken_line_sharing_speaker_and_source_with_a_sound_is_selectable(self):
        sound = dict(sentence(4, "Achoo!", "Wolf"), sound=True)
        spoken = sentence(5, "Achoo!", "Wolf")
        entry = {"speaker": "Wolf", "source": "Achoo!", "direction": "sneezing"}
        cast = checked(audio_cast.AudioCast(dict(CAST_DATA, lines=[entry])), [sound, spoken])
        self.assertEqual("deep; sneezing", cast.delivery(spoken).prompt)
        self.assertEqual("deep; sly", cast.delivery(sound).prompt)
        more = [sound, spoken, sentence(6, "Achoo!", "Wolf")]  # occurrence counts spoken lines only
        picked = checked(audio_cast.AudioCast(dict(CAST_DATA, lines=[dict(entry, occurrence=2)])), more)
        self.assertEqual("deep; sneezing", picked.delivery(more[2]).prompt)
        self.assertEqual("deep; sly", picked.delivery(spoken).prompt)

    def test_the_real_pigs_directions_land_on_the_same_speaker_in_every_dumped_language(self):
        dumps = [pathlib.Path(__file__).parent / "testdata" / f"pigs-{lang}.json" for lang in ("en", "fa")]  # dump-sentences three-little-pigs --lang en|fa, checked in
        seen = []
        for path in dumps:
            dump = json.loads(path.read_text(encoding="utf8"))
            cast = audio_cast.load(str(REPO / "content" / "dino" / "three-little-pigs" / "audio-cast.json"))
            cast.check(dump["sentences"], dump["language"])
            by_index = {s["index"]: s for s in dump["sentences"]}
            seen.append([(by_index[i]["speaker"], by_index[i]["source"], b["direction"]) for i, b in sorted(cast.bound.items())])
        self.assertEqual(seen[0], seen[1])
        self.assertEqual(len(audio_cast.load(str(REPO / "content" / "dino" / "three-little-pigs" / "audio-cast.json")).lines), len(seen[0]))

    def test_the_real_pigs_cast_is_valid_and_covers_every_speaker(self):
        cast = audio_cast.load(str(REPO / "content" / "dino" / "three-little-pigs" / "audio-cast.json"))
        dump = json.loads((pathlib.Path(__file__).parent / "testdata" / "pigs-en.json").read_text(encoding="utf8"))
        checked(cast, dump["sentences"], "en")
        for i, speaker in enumerate((None, "Pip", "Pop", "Pim", "Wolf")):
            cast.delivery(sentence(i, "t", speaker))
        self.assertEqual("Hali", cast.narrator["voice"])


class ManifestTests(Base):
    def test_round_trip_prunes_indexes_past_the_end(self):
        folder = os.path.join(self.tmp, "m")
        manifest = Manifest(folder, "book", "fa")
        delivery = audio_cast.Delivery("Hali", "s", "")
        manifest.set(sentence(0, "a"), delivery, "v", "a.wav")
        manifest.set(sentence(5, "b"), delivery, "v", "b.wav")
        manifest.save({0})
        data = json.loads(pathlib.Path(folder, "manifest.json").read_text(encoding="utf8"))
        self.assertEqual((3, "book", "fa", [0]), (data["version"], data["book"], data["language"], [e["index"] for e in data["entries"]]))
        self.assertEqual(text_hash("a"), data["entries"][0]["hash"])
        self.assertEqual({0}, set(Manifest(folder, "book", "fa").entries))

    def test_a_dry_run_never_renames_an_unreadable_manifest(self):
        folder = os.path.join(self.tmp, "m")
        os.makedirs(folder)
        pathlib.Path(folder, "manifest.json").write_text('{"version": 2, "entries": []}', encoding="utf8")
        with contextlib.redirect_stderr(io.StringIO()):
            manifest = Manifest(folder, "book", "fa", readonly=True)
        self.assertEqual({}, manifest.entries)
        self.assertEqual(["manifest.json"], os.listdir(folder))

    def test_an_entry_whose_clip_file_is_gone_is_dropped(self):
        texts = GeneratorTests.TEXTS
        self.make().run(texts)
        clips = [n for n in os.listdir(os.path.join(self.tmp, "book", "fa")) if n.endswith(".wav")]
        os.remove(os.path.join(self.tmp, "book", "fa", clips[0]))
        self.make(FakeTransport([http_error(401)])).run(texts)  # ends at the first request; the surviving entries are those with a file
        data = json.loads(pathlib.Path(self.tmp, "book", "fa", "manifest.json").read_text(encoding="utf8"))
        self.assertEqual(2, len(data["entries"]))

    def test_old_version_is_kept_as_bak_and_starts_empty(self):
        folder = os.path.join(self.tmp, "m")
        os.makedirs(folder)
        pathlib.Path(folder, "manifest.json").write_text('{"version": 2, "entries": []}', encoding="utf8")
        with contextlib.redirect_stderr(io.StringIO()):
            manifest = Manifest(folder, "book", "fa")
        self.assertEqual({}, manifest.entries)
        self.assertTrue(os.path.isfile(os.path.join(folder, "manifest.json.bak")))

    def test_text_hash_is_nfc_normalised(self):
        self.assertEqual(text_hash("é"), text_hash("é"))


class GeneratorTests(Base):
    TEXTS = [sentence(0, "یک"), sentence(1, "دو", "Pip"), sentence(2, "سه")]

    def test_renders_each_sentence_once_then_the_cache_serves_the_rerun(self):
        summary = self.make().run(self.TEXTS)
        self.assertEqual((3, 0, ""), (summary.rendered, summary.cached, summary.stopped))
        again = self.make().run(self.TEXTS)
        self.assertEqual((0, 3, 0), (again.rendered, again.cached, len(self.transport.requests)))

    def test_an_edited_sentence_renders_alone(self):
        self.make().run(self.TEXTS)
        edited = [self.TEXTS[0], sentence(1, "دو، ویرایش‌شده", "Pip"), self.TEXTS[2]]
        summary = self.make().run(edited)
        self.assertEqual((1, 2, 1), (summary.rendered, summary.cached, len(self.transport.requests)))

    def test_a_changed_direction_renders_only_that_line(self):
        self.make().run(self.TEXTS)
        changed = checked(audio_cast.AudioCast(dict(CAST_DATA, lines=[{"direction": "whispering", "speaker": "Pip", "source": "دو"}])), self.TEXTS)
        summary = self.make(cast=changed).run(self.TEXTS)
        self.assertEqual((1, 2), (summary.rendered, summary.cached))
        self.assertIn("whispering", json.loads(self.transport.requests[0].body)["input"][0]["content"][0]["annotations"][0]["style"])

    @staticmethod
    def say_cast(says):
        """A cast whose narrator lines 0 and 2 carry the given `say` (index -> text)."""
        entries = [{"say": say, "lang": "fa", "speaker": None, "source": GeneratorTests.TEXTS[i]["source"]} for i, say in says.items()]
        return checked(audio_cast.AudioCast(dict(CAST_DATA, lines=entries)), GeneratorTests.TEXTS)

    def sent_texts(self):
        return [json.loads(r.body)["input"][0]["content"][0]["text"] for r in self.transport.requests]

    def test_say_replaces_the_request_text_and_the_manifest_keeps_the_book_text(self):
        generator = self.make(cast=self.say_cast({2: "سِه"}))
        generator.run(self.TEXTS)
        self.assertEqual(["یک", "دو", "سِه"], self.sent_texts())
        entry = generator.manifest.entries[2]
        self.assertEqual(("سه", text_hash("سه"), "سِه"), (entry["text"], entry["hash"], entry["say"]))
        self.assertNotIn("say", generator.manifest.entries[0])

    def test_a_changed_say_renders_only_that_line(self):
        self.make(cast=self.say_cast({2: "سِه"})).run(self.TEXTS)
        summary = self.make(cast=self.say_cast({2: "سیه"})).run(self.TEXTS)
        self.assertEqual((1, 2, ["سیه"]), (summary.rendered, summary.cached, self.sent_texts()))

    def test_adding_a_say_re_renders_that_line_and_removing_it_returns_to_the_cached_clip(self):
        self.make().run(self.TEXTS)
        self.assertEqual(1, self.make(cast=self.say_cast({0: "یِک"})).run(self.TEXTS).rendered)
        back = self.make().run(self.TEXTS)
        self.assertEqual((0, 3), (back.rendered, back.cached))

    def test_lines_with_the_same_say_voice_and_style_share_one_clip(self):
        twice = [sentence(0, "الف"), sentence(1, "ب")]
        cast = checked(audio_cast.AudioCast(dict(CAST_DATA, lines=[
            {"say": "همان", "lang": "fa", "speaker": None, "source": "الف"}, {"say": "همان", "lang": "fa", "speaker": None, "source": "ب"}])), twice)
        generator = self.make(cast=cast)
        summary = generator.run(twice)
        self.assertEqual((1, 1, 1), (summary.rendered, summary.cached, len(self.transport.requests)))
        self.assertEqual(generator.manifest.entries[0]["clip"], generator.manifest.entries[1]["clip"])

    def test_a_say_whose_line_moved_fails_before_any_request(self):
        cast = self.say_cast({2: "سِه"})
        for moved in (sentence(2, "other"), sentence(2, "سه", "Pip")):
            with self.assertRaises(audio_cast.CastError):
                cast.check([moved], "fa")
        cast.check([sentence(7, "سه")], "fa")  # a different position is fine

    def test_variants_write_one_file_each_and_leave_the_manifest_alone(self):
        generator = self.make(cast=self.say_cast({2: "سِه"}))
        generator.run(self.TEXTS)
        manifest_path = pathlib.Path(generator.folder, "manifest.json")
        before = manifest_path.read_bytes()
        args = unittest.mock.Mock(variants=2, say=["سه‌ی", "سُه"])
        with contextlib.redirect_stdout(io.StringIO()):
            code = generate.run_variants(args, generator, self.TEXTS)
        folder = pathlib.Path(self.tmp, "book", "fa", "variants", "2")
        self.assertEqual((0, ["1.wav", "2.wav"]), (code, sorted(p.name for p in folder.iterdir())))
        self.assertEqual(["یک", "دو", "سِه", "سه‌ی", "سُه"], self.sent_texts())  # the line's own say is not sent for a candidate
        self.assertEqual(before, manifest_path.read_bytes())
        self.assertEqual(["سِه"], [e["say"] for e in generator.manifest.entries.values() if "say" in e])

    def test_variants_count_against_the_request_budget(self):
        generator = self.make(max_requests=1)
        args = unittest.mock.Mock(variants=0, say=["a", "b", "c"])
        with contextlib.redirect_stdout(io.StringIO()):
            generate.run_variants(args, generator, self.TEXTS)
        self.assertEqual(1, len(self.transport.requests))
        self.assertEqual(["1.wav"], sorted(p.name for p in pathlib.Path(self.tmp, "book", "fa", "variants", "0").iterdir()))

    def test_max_requests_ends_cleanly_and_the_next_run_resumes(self):
        first = self.make(max_requests=2).run(self.TEXTS)
        self.assertEqual((2, "--max-requests budget reached (2)"), (first.rendered, first.stopped))
        rest = self.make().run(self.TEXTS)
        self.assertEqual((1, 2), (rest.rendered, rest.cached))

    def test_the_day_limit_stops_and_survives_a_new_run(self):
        first = self.make(rpd=2).run(self.TEXTS)
        self.assertEqual(2, first.rendered)
        self.assertIn(pacing.REASON_DAY_LIMIT, first.stopped)
        blocked = self.make(rpd=2).run(self.TEXTS)
        self.assertEqual((0, 2, 0), (blocked.rendered, blocked.cached, len(self.transport.requests)))
        self.assertIn(pacing.REASON_DAY_LIMIT, blocked.stopped)
        self.clock.now += pacing.SECONDS_PER_DAY + 1
        self.assertEqual(1, self.make(rpd=2).run(self.TEXTS).rendered)

    def test_no_balance_or_forbidden_ends_the_run_with_one_call(self):
        for code in (402, 403, 401):
            generator = self.make(FakeTransport([http_error(code)]))
            summary = generator.run(self.TEXTS)
            self.assertEqual((0, 1), (summary.rendered, len(self.transport.requests)), code)
            self.assertIn(f"HTTP {code}", summary.stopped)

    def test_first_429_waits_the_retry_after_once_then_continues(self):
        transport = FakeTransport([http_error(429, "30"), gemini_body()])
        summary = self.make(transport).run(self.TEXTS)
        self.assertEqual((3, ""), (summary.rendered, summary.stopped))
        self.assertEqual([30.0], self.clock.slept)

    def test_second_429_ends_the_run(self):
        transport = FakeTransport([gemini_body(), http_error(429, "5"), http_error(429, "5")])
        summary = self.make(transport).run(self.TEXTS)
        self.assertEqual(1, summary.rendered)
        self.assertIn("429", summary.stopped)
        self.assertEqual([5.0], self.clock.slept)

    def test_a_per_day_429_ends_the_run_at_once_with_no_wait_and_no_retry(self):
        summary = self.make(FakeTransport([http_error(429, "30", daily=True)])).run(self.TEXTS)
        self.assertEqual(([], 0, 1), (self.clock.slept, summary.rendered, len(self.transport.requests)))
        self.assertEqual(generate.EXIT_RATE_LIMITED, summary.exit_code)

    def test_the_http_error_reads_a_per_day_quota_from_the_body(self):
        body = b'{"error": {"details": [{"violations": [{"quotaId": "GenerateRequestsPerDayPerProjectPerModel-FreeTier"}]}]}}'
        with urllib.error.HTTPError("u", 429, "Too Many", {}, io.BytesIO(body)) as day_error:
            per_day = adapter_base.http_error(day_error)
        with urllib.error.HTTPError("u", 429, "Too Many", {}, io.BytesIO(b'{"quotaId": "RequestsPerMinute"}')) as minute_error:
            per_minute = adapter_base.http_error(minute_error)
        self.assertEqual((True, False), (per_day.daily_limit, per_minute.daily_limit))

    def test_a_429_with_a_huge_wait_ends_without_waiting(self):
        summary = self.make(FakeTransport([http_error(429, "7200")])).run(self.TEXTS)
        self.assertEqual(([], 0), (self.clock.slept, summary.rendered))
        self.assertIn("429", summary.stopped)

    def test_three_errors_in_a_row_end_the_run_but_one_is_survived(self):
        boom = RuntimeError("boom")
        self.assertEqual(2, self.make(FakeTransport([boom, gemini_body()])).run(self.TEXTS).rendered)
        fresh = [sentence(i + 10, f"تازه {i}") for i in range(4)]
        summary = self.make(FakeTransport([boom])).run(fresh)
        self.assertEqual((0, 3), (summary.rendered, len(self.transport.requests)))
        self.assertIn("3 errors in a row", summary.stopped)

    def test_requests_per_minute_spaces_the_calls(self):
        self.make(rpm=3).run(self.TEXTS)
        self.assertEqual([20.0, 20.0], [round(s, 3) for s in self.clock.slept])

    def test_free_preset_values(self):
        args = generate.parse_args(["--sentences", "x", "--free"])
        self.assertEqual((3, 10), generate.limits(args))
        self.assertEqual((7, 0), generate.limits(generate.parse_args(["--sentences", "x", "--rpm", "7"])))

    def test_manifest_lists_every_sentence_with_its_voice_and_clip(self):
        self.make().run(self.TEXTS)
        data = json.loads(pathlib.Path(self.tmp, "book", "fa", "manifest.json").read_text(encoding="utf8"))
        self.assertEqual([0, 1, 2], [e["index"] for e in data["entries"]])
        self.assertEqual(["Hali", "Cleo", "Hali"], [e["voice"] for e in data["entries"]])
        self.assertTrue(all(os.path.isfile(os.path.join(self.tmp, "book", "fa", e["clip"])) for e in data["entries"]))


@unittest.skipUnless(encode.find_ffmpeg(), "ffmpeg is not installed")
class EncodeTests(Base):
    def duration(self, path):
        probe = subprocess.run([encode.find_ffmpeg(), "-hide_banner", "-i", path], capture_output=True, text=True, check=False).stderr
        stamp = probe.split("Duration: ")[1].split(",")[0]
        hours, minutes, seconds = stamp.split(":")
        return int(hours) * 3600 + int(minutes) * 60 + float(seconds)

    def test_m4a_is_aac_and_silence_is_trimmed(self):
        wav = os.path.join(self.tmp, "a.wav")
        pathlib.Path(wav).write_bytes(tone_wav(0.5, 0.3))
        out = os.path.join(self.tmp, "a.m4a")
        encode.to_m4a(encode.find_ffmpeg(), wav, out)
        self.assertEqual(b"ftyp", pathlib.Path(out).read_bytes()[4:8])
        self.assertLess(self.duration(out), 0.8)  # 1.1 s in, about 0.5 s of tone plus the two 0.05 s pads
        self.assertGreater(self.duration(out), 0.45)

    def test_a_silent_clip_is_an_error_and_the_sentence_is_requested_again(self):
        wav = os.path.join(self.tmp, "s.wav")
        pathlib.Path(wav).write_bytes(tone_wav(0.0, 0.5))
        with self.assertRaises(encode.SilentClipError):
            encode.to_m4a(encode.find_ffmpeg(), wav, os.path.join(self.tmp, "s.m4a"))
        self.assertEqual([], [n for n in os.listdir(self.tmp) if n.endswith(".m4a")])
        silent = FakeTransport([gemini_body(tone_wav(0.0, 0.5)), gemini_body()])
        generator = self.make(silent, fmt=generate.FORMAT_M4A, ffmpeg=encode.find_ffmpeg())
        summary = generator.run(self.TEXTS[:1])
        self.assertEqual((0, 1), (summary.rendered, len(summary.errors)))
        self.assertEqual(1, self.make(silent, fmt=generate.FORMAT_M4A, ffmpeg=encode.find_ffmpeg()).run(self.TEXTS[:1]).rendered)

    def test_a_short_sound_word_clip_passes_and_silent_clips_never_end_the_run(self):
        sound = dict(sentence(1, "اینک!", "Pip"), sound=True)
        speech = sentence(1, "اینک!", "Pip")
        ffmpeg = encode.find_ffmpeg()
        with unittest.mock.patch.object(encode, "duration_seconds", return_value=0.05):  # shorter than speech may be, longer than nothing
            self.assertEqual(1, self.make(FakeTransport(), fmt=generate.FORMAT_M4A, ffmpeg=ffmpeg).run([sound]).rendered)
            self.setUp()
            self.assertEqual(0, self.make(FakeTransport(), fmt=generate.FORMAT_M4A, ffmpeg=ffmpeg).run([speech]).rendered)
        self.setUp()
        quiet, loud = gemini_body(tone_wav(0.0, 0.5)), gemini_body()
        texts = [sentence(i, f"خط {i}") for i in range(6)]
        summary = self.make(FakeTransport([quiet, quiet, loud, quiet, quiet, loud]), fmt=generate.FORMAT_M4A, ffmpeg=ffmpeg).run(texts)
        self.assertEqual((2, 4, ""), (summary.rendered, len(summary.errors), summary.stopped))

    def test_silent_clips_end_the_run_after_three_in_a_row_or_five_in_all(self):
        ffmpeg = encode.find_ffmpeg()
        quiet, loud = gemini_body(tone_wav(0.0, 0.5)), gemini_body()
        texts = [sentence(i, f"خط {i}") for i in range(8)]
        summary = self.make(FakeTransport([quiet] * 8), fmt=generate.FORMAT_M4A, ffmpeg=ffmpeg).run(texts)
        self.assertEqual((0, 3, generate.EXIT_ERRORS), (summary.rendered, len(summary.errors), summary.exit_code))
        self.assertIn("silent clips in a row", summary.stopped)
        self.setUp()
        summary = self.make(FakeTransport([quiet, quiet, loud, quiet, quiet, loud, quiet, loud]), fmt=generate.FORMAT_M4A, ffmpeg=ffmpeg).run(texts)
        self.assertEqual((2, 5, generate.EXIT_ERRORS), (summary.rendered, len(summary.errors), summary.exit_code))
        self.assertIn("in this run", summary.stopped)

    def test_a_locked_silent_wav_does_not_crash_the_run(self):
        quiet = gemini_body(tone_wav(0.0, 0.5))
        real_remove = os.remove

        def locked_remove(path):
            if path.endswith(".wav"):
                raise PermissionError("locked")
            real_remove(path)

        with unittest.mock.patch.object(os, "remove", side_effect=locked_remove):
            summary = self.make(FakeTransport([quiet]), fmt=generate.FORMAT_M4A, ffmpeg=encode.find_ffmpeg()).run(self.TEXTS[:1])
        self.assertEqual((0, 1), (summary.rendered, len(summary.errors)))

    def test_generator_encodes_and_removes_the_wav(self):
        summary = self.make(fmt=generate.FORMAT_M4A, ffmpeg=encode.find_ffmpeg()).run(self.TEXTS[:1])
        self.assertEqual(1, summary.rendered)
        self.assertEqual([".json", ".m4a"], sorted(os.path.splitext(n)[1] for n in os.listdir(os.path.join(self.tmp, "book", "fa"))))

    TEXTS = GeneratorTests.TEXTS


class MainTests(Base):
    def files(self):
        sentences = os.path.join(self.tmp, "s.json")
        cast = os.path.join(self.tmp, "cast.json")
        pathlib.Path(sentences).write_text(json.dumps({"book": "b", "language": "fa", "sentences": GeneratorTests.TEXTS}, ensure_ascii=False), encoding="utf8")
        pathlib.Path(cast).write_text(json.dumps(dict(CAST_DATA, lines=[])), encoding="utf8")
        return sentences, cast

    def test_dry_run_counts_pending_sentences_and_sends_nothing(self):
        sentences, cast = self.files()
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = generate.main(["--sentences", sentences, "--cast", cast, "--out", self.tmp, "--format", "wav", "--dry-run", "--free"])
        self.assertEqual(0, code)
        self.assertIn("3 sentences, 3 need a request", out.getvalue())
        self.assertIn("3/min, 10/day", out.getvalue())

    def test_a_speaker_without_a_voice_fails_before_any_request(self):
        sentences, cast = self.files()
        pathlib.Path(sentences).write_text(json.dumps({"book": "b", "language": "fa", "sentences": [sentence(0, "x", "Troll")]}), encoding="utf8")
        with self.assertRaises(audio_cast.CastError):
            generate.main(["--sentences", sentences, "--cast", cast, "--out", self.tmp, "--format", "wav", "--dry-run"])

    def test_missing_ffmpeg_is_a_clear_exit_not_a_crash(self):
        sentences, cast = self.files()
        with unittest.mock.patch.dict(os.environ, {encode.FFMPEG_ENV: "", "PATH": ""}):
            err = io.StringIO()
            with contextlib.redirect_stderr(err):
                code = generate.main(["--sentences", sentences, "--cast", cast, "--out", self.tmp])
        self.assertEqual(generate.EXIT_NO_FFMPEG, code)
        self.assertIn("--format wav", err.getvalue())

    def test_dry_run_needs_no_ffmpeg(self):
        sentences, cast = self.files()
        with unittest.mock.patch.dict(os.environ, {encode.FFMPEG_ENV: "", "PATH": ""}), contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, generate.main(["--sentences", sentences, "--cast", cast, "--out", self.tmp, "--dry-run"]))

    def test_a_second_run_on_the_same_folder_is_refused(self):
        sentences, cast = self.files()
        with pacing.RunLock(self.tmp), contextlib.redirect_stderr(io.StringIO()):
            code = generate.main(["--sentences", sentences, "--cast", cast, "--out", self.tmp, "--format", "wav"])
        self.assertEqual(generate.EXIT_LOCKED, code)
        with pacing.RunLock(self.tmp):  # released when the first run ended
            pass

    def test_a_say_guard_mismatch_fails_before_any_request_and_variants_need_both_options(self):
        sentences, cast = self.files()
        bad = dict(CAST_DATA, lines=[{"say": "سِه", "lang": "fa", "speaker": None, "source": "not the source"}])
        pathlib.Path(cast).write_text(json.dumps(bad, ensure_ascii=False), encoding="utf8")
        with unittest.mock.patch.object(generate, "Gemini", side_effect=AssertionError("no request may be prepared")):
            with self.assertRaises(audio_cast.CastError):
                generate.main(["--sentences", sentences, "--cast", cast, "--out", self.tmp, "--format", "wav"])
        for lone in (["--variants", "1"], ["--say", "x"]):
            with self.assertRaises(SystemExit), contextlib.redirect_stderr(io.StringIO()):
                generate.parse_args(["--sentences", sentences] + lone)
        self.assertEqual(["x"], generate.parse_args(["--sentences", sentences, "--variants", "1", "--say", "x"]).say)

    def test_an_empty_or_blank_say_option_is_rejected_before_any_request(self):
        sentences, _ = self.files()
        for blank in ("", "   "):
            with self.assertRaises(SystemExit), contextlib.redirect_stderr(io.StringIO()):
                generate.parse_args(["--sentences", sentences, "--variants", "1", "--say", "ok", "--say", blank])

    def test_a_say_is_nfc_normalised_in_the_option_and_the_cast(self):
        decomposed, composed = "é", "é"
        sentences, _ = self.files()
        args = generate.parse_args(["--sentences", sentences, "--variants", "1", "--say", decomposed])
        self.assertEqual([composed], args.say)
        entry = {"say": decomposed, "lang": "fa", "speaker": None, "source": "x"}
        cast = checked(audio_cast.AudioCast(dict(CAST_DATA, lines=[entry])), [sentence(2, "x")])
        self.assertEqual(composed, cast.say_for({"index": 2}))

    def test_a_run_has_a_default_request_cap(self):
        sentences, _ = self.files()
        self.assertEqual(pacing.DEFAULT_MAX_REQUESTS, generate.parse_args(["--sentences", sentences]).max_requests)
        self.assertEqual(0, generate.parse_args(["--sentences", sentences, "--max-requests", "0"]).max_requests)


class RobustnessTests(Base):
    TEXTS = GeneratorTests.TEXTS

    def test_a_stale_entry_for_edited_text_is_dropped_even_when_the_run_stops_before_it(self):
        self.make().run(self.TEXTS)
        edited = [sentence(0, "یک، ویرایش‌شده"), self.TEXTS[1], self.TEXTS[2]]
        self.make(FakeTransport([http_error(401)])).run(edited)  # ends at the first sentence, before reaching the rest
        data = json.loads(pathlib.Path(self.tmp, "book", "fa", "manifest.json").read_text(encoding="utf8"))
        self.assertEqual([1, 2], [e["index"] for e in data["entries"]])

    def test_stale_entries_are_saved_at_once_not_only_at_the_end(self):
        self.make().run(self.TEXTS)
        edited = [sentence(0, "یک، ویرایش‌شده"), self.TEXTS[1], self.TEXTS[2]]
        generator = self.make()
        with unittest.mock.patch.object(generator, "step", side_effect=KeyboardInterrupt), self.assertRaises(KeyboardInterrupt):
            generator.run(edited)  # killed before the first sentence is handled
        data = json.loads(pathlib.Path(self.tmp, "book", "fa", "manifest.json").read_text(encoding="utf8"))
        self.assertEqual([1, 2], [e["index"] for e in data["entries"]])

    def test_exit_codes_tell_the_causes_apart(self):
        missing = Gemini(environ={adapter_base.SECRETS_FILE_ENV: os.path.join(self.tmp, "none.env")}, transport=FakeTransport())
        generator = self.make()
        generator.adapter = missing
        summary = generator.run(self.TEXTS)
        self.assertEqual(generate.EXIT_NO_KEY, summary.exit_code)
        self.assertEqual(generate.EXIT_AUTH, self.make(FakeTransport([http_error(403)])).run(self.TEXTS).exit_code)
        self.assertEqual(generate.EXIT_RATE_LIMITED, self.make(FakeTransport([http_error(429, "7200")])).run(self.TEXTS).exit_code)
        self.assertEqual(generate.EXIT_OK, self.make(max_requests=1).run(self.TEXTS).exit_code)

    def test_an_encode_failure_keeps_the_wav_and_the_rerun_encodes_it_without_a_request(self):
        with unittest.mock.patch.object(encode, "to_m4a", side_effect=encode.EncodeError("no aac")):
            first = self.make(fmt=generate.FORMAT_M4A, ffmpeg="ffmpeg").run(self.TEXTS[:1])
        self.assertEqual((generate.EXIT_ENCODE, 1), (first.exit_code, len(self.transport.requests)))

        def fake_encode(_ffmpeg, _wav, out, _min_seconds):
            pathlib.Path(out).write_bytes(b"m4a")

        with unittest.mock.patch.object(encode, "to_m4a", side_effect=fake_encode):
            second = self.make(fmt=generate.FORMAT_M4A, ffmpeg="ffmpeg").run(self.TEXTS[:1])
        self.assertEqual((1, 0), (second.rendered, len(self.transport.requests)))

    def test_a_hung_ffmpeg_is_an_encode_failure_not_a_crash(self):
        with unittest.mock.patch.object(encode, "to_m4a", side_effect=subprocess.TimeoutExpired("ffmpeg", 1)):
            summary = self.make(fmt=generate.FORMAT_M4A, ffmpeg="ffmpeg").run(self.TEXTS[:1])
        self.assertEqual(generate.EXIT_ENCODE, summary.exit_code)
        self.assertTrue(os.path.isfile(os.path.join(self.tmp, "book", "fa", "manifest.json")))

    def test_the_first_request_of_a_run_waits_for_the_previous_runs_last(self):
        self.make(rpm=3, max_requests=1).run(self.TEXTS)
        self.clock.slept.clear()
        self.clock.now += 5
        self.make(rpm=3, max_requests=1).run(self.TEXTS)
        self.assertEqual([15.0], [round(s, 3) for s in self.clock.slept])

    def test_a_429_that_used_the_last_request_does_not_wait_for_a_retry_it_cannot_send(self):
        summary = self.make(FakeTransport([http_error(429, "30")]), max_requests=1).run(self.TEXTS)
        self.assertEqual(([], 0), (self.clock.slept, summary.rendered))
        self.assertEqual(generate.EXIT_OK, summary.exit_code)


if __name__ == "__main__":
    unittest.main()
