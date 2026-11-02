"""Offline tests (Slice 246): no network, no paid call. Run: python -I -m pytest tools/premade-audio  (or python -I -m unittest discover -s tools/premade-audio)."""
import base64
import contextlib
import io
import json
import os
import pathlib
import tempfile
import unittest
import urllib.error
import wave

import adapter_base
import adapter_gemini
import adapter_openai
import book_text
import clip_manifest
import render_all
from adapter_azure import Azure
from adapter_base import STATUS_OK, STATUS_SKIPPED, Adapter, Audio, Request, Result
from adapter_camb import Camb
from adapter_elevenlabs import ElevenLabs
from adapter_gemini import Gemini
from adapter_narakeet import Narakeet
from adapter_openai import OpenAi

NO_SECRETS = {"PREMADE_AUDIO_SECRETS": os.path.join(tempfile.gettempdir(), "premade-audio-no-such-file.env")}
BOOK = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "content", "dino", "three-little-pigs")


def forbid_network(request):
    raise AssertionError("network call attempted")


def with_keys(**keys):
    return {**NO_SECRETS, **keys}


class SpeakTableTests(unittest.TestCase):
    def test_speak_key_is_prefixed_and_lowercase(self):
        self.assertEqual("speak:fa", book_text.speak_key("FA"))

    def test_speak_table_wins_then_displayed_then_none(self):
        tables = {"speak:fa": {"a": "A-spoken"}, "fa": {"a": "A-shown", "b": "B-shown"}}
        self.assertEqual("A-spoken", book_text.lookup_spoken(tables, "fa", "a"))
        self.assertEqual("B-shown", book_text.lookup_spoken(tables, "fa", "b"))
        self.assertIsNone(book_text.lookup_spoken(tables, "fa", "c"))

    def test_paragraph_pick_and_speak_applied_on_the_real_book(self):
        pieces, text = book_text.listening_paragraph(BOOK, "fa", 0)
        self.assertEqual(["Once upon a time, three little pigs left home to build houses of their very own.", "Their names were Pip, Pop and Pim."], pieces)
        speak = book_text.load_json(os.path.join(BOOK, "translations", "fa.speak.json"))
        self.assertEqual(" ".join(speak[p] for p in pieces), text)
        self.assertIn("ْ", text)  # the vowel marks of the speak table (sukun) are present

    def test_pieces_in_ignores_quotes_and_keeps_order(self):
        found = book_text.pieces_in('"Yay!" said Pip. He ran.', ["Yay!", "said Pip.", "He ran.", "Other."])
        self.assertEqual(["Yay!", "said Pip.", "He ran."], found)

    def test_paragraphs_split_pages_and_blank_lines(self):
        self.assertEqual(["a", "b", "c"], book_text.paragraphs("a\n\nb\n---\nc"))


class AdapterTests(unittest.TestCase):
    def test_missing_key_is_skipped_and_sends_nothing(self):
        with tempfile.TemporaryDirectory() as d:
            for cls in (ElevenLabs, Gemini, Azure, OpenAi, Narakeet, Camb):
                a = cls(environ=NO_SECRETS, transport=forbid_network)
                r = a.render("text", a.voices[0], os.path.join(d, "x"))
                self.assertEqual(STATUS_SKIPPED, r.status, cls.__name__)
                self.assertIn("skipped: no key", r.detail)
                self.assertIn(a.key_env, r.detail)
            self.assertEqual([], os.listdir(d))

    def test_azure_needs_region_too(self):
        a = Azure(environ=with_keys(AZURE_SPEECH_KEY="k"), transport=forbid_network)
        self.assertEqual(["AZURE_SPEECH_REGION"], a.missing())

    def test_request_shapes(self):
        e = ElevenLabs(environ=NO_SECRETS).build_request("سلام", "VID", "k")
        self.assertTrue(e.url.startswith("https://api.elevenlabs.io/v1/text-to-speech/VID?output_format="))
        self.assertEqual("k", e.headers["xi-api-key"])
        self.assertEqual({"eleven_v3", "fa"}, {json.loads(e.body)["model_id"], json.loads(e.body)["language_code"]})
        o = json.loads(OpenAi(environ=NO_SECRETS).build_request("x", "coral", "k").body)
        self.assertEqual(("gpt-4o-mini-tts", "coral"), (o["model"], o["voice"]))
        n = Narakeet(environ=NO_SECRETS).build_request("x", "reza", "k")
        self.assertTrue(n.url.endswith("/text-to-speech/mp3?voice=reza"))
        c = json.loads(Camb(environ=NO_SECRETS).build_request("سلام دنیا", "147320", "k").body)
        self.assertEqual(("fa-ir", 147320), (c["language"], c["voice_id"]))
        g = json.loads(Gemini(environ=NO_SECRETS).build_request("x", "Kore", "k").body)
        self.assertEqual([{"voice": "Kore"}], g["generation_config"]["speech_config"])

    def test_azure_ssml_and_url_use_region(self):
        a = Azure(environ=with_keys(AZURE_SPEECH_KEY="k", AZURE_SPEECH_REGION="westeurope"))
        r = a.build_request("a < b", "fa-IR-DilaraNeural", "k")
        self.assertEqual("https://westeurope.tts.speech.microsoft.com/cognitiveservices/v1", r.url)
        self.assertIn("name='fa-IR-DilaraNeural'>a &lt; b</voice>", r.body.decode("utf8"))

    def test_gemini_takes_last_audio_part(self):
        body = json.dumps({"steps": [{"content": [{"type": "audio", "data": base64.b64encode(b"RIFFone").decode()}]},
                                     {"content": [{"type": "text"}, {"type": "audio", "data": base64.b64encode(b"RIFFtwo").decode()}]}]}).encode()
        audio = Gemini(environ=NO_SECRETS).parse_response(body, "application/json")
        self.assertEqual((b"RIFFtwo", "wav"), (audio.data, audio.extension))

    def test_render_with_key_writes_file_through_fake_transport(self):
        sent = []

        def fake(request):
            sent.append(request)
            return b"MP3", "audio/mpeg"
        with tempfile.TemporaryDirectory() as d:
            r = OpenAi(environ=with_keys(OPENAI_API_KEY="k"), transport=fake).render("x", "coral", os.path.join(d, "o"))
            self.assertEqual(STATUS_OK, r.status)
            self.assertEqual(b"MP3", pathlib.Path(r.path).read_bytes())
            self.assertEqual(1, len(sent))

    def test_vendor_failure_is_an_error_result(self):
        def boom(request):
            raise OSError("down")
        with tempfile.TemporaryDirectory() as d:
            r = OpenAi(environ=with_keys(OPENAI_API_KEY="k"), transport=boom).render("x", "coral", os.path.join(d, "o"))
            self.assertEqual("error", r.status)

    def test_key_comes_from_secrets_file(self):
        with tempfile.TemporaryDirectory() as d:
            path = os.path.join(d, "s.env")
            with open(path, "w", encoding="utf8") as f:
                f.write("# c\nOPENAI_API_KEY=\"abc\"\n")
            self.assertEqual("abc", OpenAi(environ={"PREMADE_AUDIO_SECRETS": path}).key())


class FakeAdapter(Adapter):
    name = "fake"
    voices = ("v",)

    def __init__(self):
        super().__init__()
        self.calls = []

    def missing(self):
        return []

    def render(self, text, voice, out_path):
        self.calls.append(text)
        path = out_path + ".mp3"
        with open(path, "wb") as f:
            f.write(b"x")
        return Result(self.name, voice, STATUS_OK, "", path)


class ManifestTests(unittest.TestCase):
    def test_hash_is_stable_and_nfc_normalised(self):
        self.assertEqual(clip_manifest.text_hash("é"), clip_manifest.text_hash("é"))
        self.assertNotEqual(clip_manifest.text_hash("a"), clip_manifest.text_hash("b"))

    def test_manifest_shape_and_cache_rerenders_only_changed_sentence(self):
        with tempfile.TemporaryDirectory() as d:
            fake = FakeAdapter()
            clip_manifest.render_sentences(["one", "two"], "v", fake, d)
            data = json.loads(pathlib.Path(d, "manifest.json").read_text(encoding="utf8"))
            self.assertEqual(2, data["version"])
            self.assertEqual({"hash", "voice", "variant", "clip", "text"}, set(data["entries"][0]))
            self.assertEqual(["one", "two"], [e["text"] for e in data["entries"]])
            _, hits = clip_manifest.render_sentences(["one", "two edited"], "v", fake, d)
            self.assertEqual(1, hits)
            self.assertEqual(["one", "two", "two edited"], fake.calls)

    def test_missing_clip_file_is_rerendered_and_other_voice_is_separate(self):
        with tempfile.TemporaryDirectory() as d:
            fake = FakeAdapter()
            clip_manifest.render_sentences(["one"], "v", fake, d)
            clip = clip_manifest.Manifest(d).cached("one", "v", clip_manifest.variant_of(fake, "v"))["clip"]
            os.remove(os.path.join(d, clip))
            clip_manifest.render_sentences(["one"], "v", fake, d)
            clip_manifest.render_sentences(["one"], "w", fake, d)
            self.assertEqual(3, len(fake.calls))


class ManifestKeyTests(unittest.TestCase):
    def test_other_service_or_settings_never_reuse_or_overwrite_a_clip(self):
        with tempfile.TemporaryDirectory() as d:
            first, second = FakeAdapter(), FakeAdapter()
            second.name = "other"
            clip_manifest.render_sentences(["one"], "v", first, d)
            clip_manifest.render_sentences(["one"], "v", second, d)
            self.assertEqual(["one"], first.calls)
            self.assertEqual(["one"], second.calls)
            self.assertEqual(2, len({e["clip"] for e in clip_manifest.Manifest(d).entries.values()}))

    def test_settings_tag_follows_model_and_style(self):
        a = OpenAi(environ=NO_SECRETS)
        before = a.settings_tag("coral")
        self.assertEqual(before, OpenAi(environ=NO_SECRETS).settings_tag("coral"))
        self.assertNotEqual(before, a.settings_tag("sage"))
        original = adapter_openai.INSTRUCTIONS
        try:
            adapter_openai.INSTRUCTIONS = original + " slower"
            self.assertNotEqual(before, a.settings_tag("coral"))
        finally:
            adapter_openai.INSTRUCTIONS = original
        for cls in (ElevenLabs, Gemini, Azure, Narakeet, Camb):
            self.assertTrue(cls(environ=NO_SECRETS).settings_tag(cls.voices[0]), cls.__name__)

    def test_voice_is_sanitised_in_the_clip_file_name(self):
        with tempfile.TemporaryDirectory() as d:
            fake = FakeAdapter()
            clip_manifest.render_sentences(["one"], "a/b:c", fake, d)
            clip = clip_manifest.Manifest(d).entries.popitem()[1]["clip"]
            self.assertNotIn("/", clip)
            self.assertNotIn(":", clip)
            self.assertEqual([clip, "manifest.json"], sorted(os.listdir(d), key=lambda n: n == "manifest.json"))


class StopOnFatalTests(unittest.TestCase):
    def failing(self, status, detail):
        fake = FakeAdapter()
        fake.render = lambda text, voice, out: fake.calls.append(text) or Result("fake", voice, status, detail)
        return fake

    def test_missing_key_makes_one_call_for_many_sentences(self):
        with tempfile.TemporaryDirectory() as d:
            fake = self.failing("skipped", "skipped: no key")
            results, _ = clip_manifest.render_sentences(["a", "b", "c", "d"], "v", fake, d)
            self.assertEqual(1, len(results))

    def test_unauthorised_stops_but_a_transient_error_gets_three_tries(self):
        with tempfile.TemporaryDirectory() as d:
            fake = self.failing("error", "RuntimeError: HTTP 401 Unauthorized: bad key")
            clip_manifest.render_sentences(["a", "b", "c", "d"], "v", fake, d)
            self.assertEqual(["a"], fake.calls)
            fake = self.failing("error", "OSError: timed out")
            clip_manifest.render_sentences(["a", "b", "c", "d", "e"], "v", fake, d)
            self.assertEqual(["a", "b", "c"], fake.calls)


class TransportTests(unittest.TestCase):
    def test_http_error_message_carries_the_vendor_body(self):
        error = urllib.error.HTTPError("https://x", 422, "Unprocessable", {}, io.BytesIO(b'{"detail":"locale fa-ir not allowed"}'))
        self.addCleanup(error.close)
        message = adapter_base.http_error_message(error)
        self.assertIn("HTTP 422", message)
        self.assertIn("locale fa-ir not allowed", message)

    def test_http_send_sends_a_user_agent_and_raises_with_the_body(self):
        error = urllib.error.HTTPError("https://x", 401, "Unauthorized", {}, io.BytesIO(b"bad key"))
        self.addCleanup(error.close)
        seen = []

        def fake_open(req, timeout):
            seen.append(req)
            raise error
        original = adapter_base.urllib.request.urlopen
        adapter_base.urllib.request.urlopen = fake_open
        try:
            with self.assertRaises(RuntimeError) as caught:
                adapter_base.http_send(Request("https://x", {"X-A": "1"}, b"{}"))
        finally:
            adapter_base.urllib.request.urlopen = original
        self.assertIn("bad key", str(caught.exception))
        self.assertEqual(adapter_base.USER_AGENT, seen[0].get_header("User-agent"))


class ReviewFixTests(unittest.TestCase):
    def test_camb_short_text_is_unsupported_not_an_error(self):
        result = Camb(with_keys(CAMB_API_KEY="k"), forbid_network).render("Oh", "147320", os.path.join(tempfile.gettempdir(), "x"))
        self.assertEqual(adapter_base.STATUS_UNSUPPORTED, result.status)

    def test_unsupported_sentences_do_not_stop_the_batch(self):
        class Fake:
            name = "fake"
            calls = []

            def settings_tag(self, voice):
                return "t"

            def render(self, text, voice, out_path):
                self.calls.append(text)
                return Result("fake", voice, adapter_base.STATUS_UNSUPPORTED, "unsupported: x")
        with tempfile.TemporaryDirectory() as d:
            fake = Fake()
            clip_manifest.render_sentences(["a", "b", "c", "d", "e"], "v", fake, d)
            self.assertEqual(["a", "b", "c", "d", "e"], fake.calls)

    def test_manifest_save_is_atomic_and_leaves_no_temp_file(self):
        with tempfile.TemporaryDirectory() as d:
            m = clip_manifest.Manifest(d)
            m.add("hi", "v", "s-1", "c.mp3")
            m.save()
            self.assertEqual(["manifest.json"], os.listdir(d))
            self.assertEqual(1, len(clip_manifest.Manifest(d).entries))

    def test_settings_tag_changes_with_a_header(self):
        class A(Adapter):
            fmt = "a"

            def build_request(self, text, voice, key):
                return Request("https://x", {"fmt": self.fmt}, b"{}")
        one = A().settings_tag("v")
        a = A()
        a.fmt = "b"
        self.assertNotEqual(one, a.settings_tag("v"))

    def test_empty_speak_entry_leaves_no_double_space(self):
        with tempfile.TemporaryDirectory() as d:
            os.makedirs(os.path.join(d, "translations"))
            with open(os.path.join(d, "story.md"), "w", encoding="utf8") as f:
                f.write("One. Two. Three.")
            with open(os.path.join(d, "translations", "pieces.json"), "w", encoding="utf8") as f:
                json.dump(["One.", "Two.", "Three."], f)
            with open(os.path.join(d, "translations", "fa.speak.json"), "w", encoding="utf8") as f:
                json.dump({"One.": "a", "Two.": "", "Three.": "c"}, f)
            _, text = book_text.listening_paragraph(d, "fa", 0)
            self.assertEqual("a c", text)


class SecretsPathTests(unittest.TestCase):
    def test_default_is_the_repo_secrets_folder_and_is_named_when_skipped(self):
        self.assertEqual("D:/secrets/readthestupidtext/secrets.env", adapter_base.DEFAULT_SECRETS_FILE)
        self.assertEqual(adapter_base.DEFAULT_SECRETS_FILE, adapter_base.secrets_path({}))
        result = OpenAi(environ=NO_SECRETS).render("x", "coral", "unused")
        self.assertIn(NO_SECRETS["PREMADE_AUDIO_SECRETS"], result.detail)

    def test_environment_wins_over_the_file(self):
        with tempfile.TemporaryDirectory() as d:
            path = os.path.join(d, "s.env")
            with open(path, "w", encoding="utf8") as f:
                f.write("OPENAI_API_KEY=file\n")
            self.assertEqual("env", OpenAi(environ={"PREMADE_AUDIO_SECRETS": path, "OPENAI_API_KEY": "env"}).key())


class GeminiWavTests(unittest.TestCase):
    def test_raw_pcm_is_wrapped_and_riff_is_kept(self):
        pcm = b"\x01\x00" * 100
        wrapped = adapter_gemini.wav_bytes(pcm)
        with wave.open(io.BytesIO(wrapped)) as w:
            self.assertEqual((1, 2, 24000, 100), (w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()))
        self.assertEqual(wrapped, adapter_gemini.wav_bytes(wrapped))

    def test_parse_response_wraps_raw_pcm(self):
        body = json.dumps({"steps": [{"content": [{"type": "audio", "data": base64.b64encode(b"\x00\x00" * 4).decode()}]}]}).encode()
        self.assertEqual(b"RIFF", Gemini(environ=NO_SECRETS).parse_response(body, "").data[:4])


class NarakeetLimitTests(unittest.TestCase):
    def test_long_text_is_unsupported_not_an_error(self):
        with tempfile.TemporaryDirectory() as d:
            a = Narakeet(environ=with_keys(NARAKEET_API_KEY="k"), transport=forbid_network)
            r = a.render("ب" * 600, "reza", os.path.join(d, "o"))
            self.assertEqual("unsupported", r.status)
            self.assertIn("1024", r.detail)
            self.assertFalse(clip_manifest.is_fatal(r))


class LanguageKeyTests(unittest.TestCase):
    def test_language_is_normalised_like_bundled_translations_key(self):
        for given in ("fa-IR", "FA", " fa ", "fa_IR"):
            self.assertEqual("fa", book_text.language_key(given), given)
        self.assertEqual("nb", book_text.language_key("no"))
        self.assertEqual("nb", book_text.language_key("nn-NO"))
        self.assertEqual("en", book_text.language_key(""))
        self.assertEqual("xx-yy", book_text.language_key(" XX-yy "))  # unsupported: kept trimmed and lower-cased
        self.assertEqual("speak:fa", book_text.speak_key("fa-IR"))

    def test_fa_ir_loads_the_real_farsi_tables(self):
        _, text = book_text.listening_paragraph(BOOK, "fa-IR", 0)
        _, plain = book_text.listening_paragraph(BOOK, "fa", 0)
        self.assertEqual(plain, text)

    def test_empty_speak_entry_is_returned_as_is_and_text_is_not_replaced(self):
        tables = {"speak:fa": {"a": ""}, "fa": {"a": "shown"}}
        self.assertEqual("", book_text.lookup_spoken(tables, "fa-IR", "a"))


class EveryParagraphTests(unittest.TestCase):
    def test_every_paragraph_of_the_real_book_is_fully_covered_by_pieces(self):
        with open(os.path.join(BOOK, "story.md"), encoding="utf8") as f:
            all_paragraphs = book_text.paragraphs(f.read())
        pieces = book_text.load_json(os.path.join(BOOK, "translations", "pieces.json"))
        for i, paragraph in enumerate(all_paragraphs):
            found = book_text.pieces_in(paragraph, pieces)
            plain = paragraph.translate({ord(c): " " for c in book_text.QUOTES})
            self.assertEqual(plain.split(), " ".join(found).split(), f"paragraph {i}")

    def test_quoted_pigs_paragraphs_match_without_gluing_words(self):
        with open(os.path.join(BOOK, "story.md"), encoding="utf8") as f:
            quoted = [p for p in book_text.paragraphs(f.read()) if any(q in p for q in book_text.QUOTES)]
        pieces = book_text.load_json(os.path.join(BOOK, "translations", "pieces.json"))
        self.assertTrue(quoted)
        for paragraph in quoted:
            joined = " ".join(book_text.pieces_in(paragraph, pieces))
            self.assertEqual(paragraph.translate({ord(c): " " for c in book_text.QUOTES}).split(), joined.split(), paragraph[:40])

    def test_quote_without_space_does_not_glue_words(self):
        self.assertEqual(["Yay!", "said Pip."], book_text.pieces_in('"Yay!"said Pip.', ["Yay!", "said Pip.", "Yay!said Pip."]))


class AudioValidationTests(unittest.TestCase):
    def render(self, body, content_type="audio/mpeg"):
        with tempfile.TemporaryDirectory() as d:
            r = OpenAi(environ=with_keys(OPENAI_API_KEY="k"), transport=lambda request: (body, content_type)).render("x", "coral", os.path.join(d, "o"))
            return r, os.listdir(d)

    def test_empty_and_error_page_bodies_are_errors_and_nothing_is_saved(self):
        for body in (b"", b"<html>Bad gateway</html>", b' {"error": "quota"}'):
            r, files = self.render(body)
            self.assertEqual(("error", []), (r.status, files), body)

    def test_unreadable_manifest_is_kept_as_bak_and_starts_empty(self):
        with tempfile.TemporaryDirectory() as d:
            for content in ("{not json", '{"entries": [{"hash": "h"}]}'):
                pathlib.Path(d, "manifest.json").write_text(content, encoding="utf8")
                with contextlib.redirect_stderr(io.StringIO()) as err:
                    self.assertEqual({}, clip_manifest.Manifest(d).entries)
                self.assertIn("WARNING", err.getvalue())
                self.assertEqual(content, pathlib.Path(d, "manifest.json.bak").read_text(encoding="utf8"))
                self.assertFalse(os.path.exists(os.path.join(d, "manifest.json")))

    def test_repeated_piece_does_not_drop_the_rest(self):
        pieces = ["Yay!", "said Pip.", "said Pop."]
        self.assertEqual(["Yay!", "said Pip."], book_text.pieces_in('"Yay!" said Pip.', pieces))
        self.assertEqual(["Yay!", "said Pop.", "Yay!"], book_text.pieces_in('"Yay!" said Pop. "Yay!"', pieces))

    def test_unmatched_text_raises_instead_of_dropping(self):
        with self.assertRaises(ValueError):
            book_text.pieces_in("Known. Unknown.", ["Known."])


class RenderAllTests(unittest.TestCase):
    def test_all_skipped_without_keys_and_page_lists_every_service(self):
        adapters = [cls(environ=NO_SECRETS, transport=forbid_network) for cls in render_all.ADAPTERS]
        with tempfile.TemporaryDirectory() as d:
            results = render_all.render_all("متن", d, "", adapters)
            self.assertTrue(all(r.status == STATUS_SKIPPED for r in results))
            self.assertIn("piper-fa pack not on disk", results[0].detail)
            self.assertEqual(1 + sum(len(a.voices) for a in adapters), len(results))
            render_all.write_outputs("متن", results, d)
            page = pathlib.Path(d, "index.html").read_text(encoding="utf8")
            for a in adapters:
                self.assertIn(a.name, page)
            self.assertIn("skipped: no key", page)


if __name__ == "__main__":
    unittest.main()
