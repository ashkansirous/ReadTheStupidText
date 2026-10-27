"""Run from tools/sound-words-test: python -m unittest test_finglish_to_marks"""
import unittest

from finglish_to_marks import FATHA, KASRA, DAMMA, SHADDA, SUKUN, ZWNJ, finglish_to_marks, finglish_to_marks_split


class FinglishToMarksTests(unittest.TestCase):
    def check(self, word, finglish, expected):
        self.assertEqual((expected, None), finglish_to_marks(word, finglish))

    def test_ye_gets_kasra_on_yeh(self):
        self.check("یه", "ye", "ی" + KASRA + "ه")

    def test_khune_is_unmarked(self):
        self.check("خونه", "khune", "خون" + KASRA + "ه")

    def test_bebakhshid_is_marked(self):
        self.check("ببخشید", "bebakhshid", "ب" + KASRA + "ب" + FATHA + "خ" + SUKUN + "شید" + SUKUN)

    def test_short_vowels_on_consonants(self):
        self.check("مدرسه", "madrase", "م" + FATHA + "د" + SUKUN + "ر" + FATHA + "س" + KASRA + "ه")

    def test_initial_alef_takes_the_vowel(self):
        self.check("اگر", "agar", "ا" + FATHA + "گ" + FATHA + "ر" + SUKUN)

    def test_doubled_consonant_gets_shadda(self):
        self.check("محمد", "mohammad", "م" + DAMMA + "ح" + FATHA + "م" + SHADDA + FATHA + "د" + SUKUN)

    def test_o_matches_waw_when_the_letter_is_there(self):
        self.check("خودش", "khodash", "خود" + FATHA + "ش" + SUKUN)

    def test_ezafe_kasra_on_last_consonant(self):
        self.check("کتاب", "ketaabe", "ک" + KASRA + "تاب" + KASRA)

    def test_ezafe_after_final_he_adds_yeh(self):
        self.check("خونه", "khuneye", "خون" + KASRA + "ه" + ZWNJ + "ی")

    def test_final_ayn_is_silent_when_finglish_omits_it(self):
        self.check("شروع", "shoru", "ش" + DAMMA + "روع")

    def test_ezafe_after_final_yeh_marks_the_yeh(self):
        self.check("چوبی", "chubiye", "چوبی" + KASRA)

    def test_initial_ayn_takes_the_vowel_and_final_he_is_the_vowel(self):
        self.check("عجله", "ajale", "ع" + FATHA + "ج" + FATHA + "ل" + KASRA + "ه")

    def test_he_before_zwnj_is_the_vowel(self):
        self.check("خونه" + ZWNJ + "شون", "khunashun", "خون" + FATHA + "ه" + ZWNJ + "شون" + SUKUN)

    def test_he_before_zwnj_with_long_o_ending(self):
        self.check("خونه" + ZWNJ + "شو", "khunasho", "خون" + FATHA + "ه" + ZWNJ + "شو")

    def test_he_vowel_marks_the_letter_before(self):
        self.check("خونه" + ZWNJ + "شو", "khunasho", "خون" + FATHA + "ه" + ZWNJ + "شو")
        self.check("خونه", "khune", "خون" + KASRA + "ه")
        self.check("عجله", "ajale", "ع" + FATHA + "ج" + FATHA + "ل" + KASRA + "ه")

    def test_silent_waw_after_khe_is_skipped(self):
        self.check("می" + ZWNJ + "خواست", "mikhaast", "می" + ZWNJ + "خواس" + SUKUN + "ت" + SUKUN)

    def test_space_hyphen_or_nothing_for_zwnj(self):
        expected = "ی" + KASRA + "ه" + ZWNJ + "ن" + FATHA + "ف" + FATHA + "س" + SUKUN
        for finglish in ("ye nafas", "ye-nafas", "yenafas"):
            self.check("یه" + ZWNJ + "نفس", finglish, expected)

    def test_boundary_needs_a_break_in_the_farsi(self):
        marked, _ = finglish_to_marks("خونه", "khu ne")
        self.assertIsNone(marked)

    def test_khunasho(self):
        expected = "خون" + FATHA + "ه" + ZWNJ + "شو"
        self.check("خونه" + ZWNJ + "شو", "khunasho", expected)
        self.check("خونه" + ZWNJ + "شو", "khuna sho", expected)

    def test_natural_finglish_spellings(self):
        self.check("جاده", "jadde", "جاد" + KASRA + "ه")
        self.check("میومد", "miyoomad", "می" + "وم" + FATHA + "د" + SUKUN)
        self.check("بیام", "biyaam", "بیام" + SUKUN)
        self.check("بیاین", "biyayin", "بیاین" + SUKUN)
        self.check("اومد", "umad", "او" + "م" + FATHA + "د" + SUKUN)

    def test_cluster_consonant_gets_sukun(self):
        self.check("ساختن", "saakhtane", "ساخ" + SUKUN + "ت" + FATHA + "ن" + KASRA)

    def test_waw_stands_for_vaa(self):
        self.check("هیچ" + ZWNJ + "وقت", "hichvaaght", "هیچ" + SUKUN + ZWNJ + "وق" + SUKUN + "ت" + SUKUN)

    def test_tanwin_stands_for_an(self):
        self.check("اصلاً", "aslan", "ا" + FATHA + "ص" + SUKUN + "لاً")

    def test_u_before_final_m_is_waw(self):
        self.check("کوچولوم", "kuchulum", "کوچولوم" + SUKUN)
        marked, problem = finglish_to_marks("کوچولوم", "kuchulm")
        self.assertIsNone(marked)
        self.assertIn("cannot align", problem)

    def test_split_separates_the_ezafe_the_plain_word_does_not_spell(self):
        self.assertEqual(("گُرْگْ", "گُرْگِ", None), finglish_to_marks_split("گرگ", "gorge"))
        self.assertEqual(("چوبی", "چوبیِ", None), finglish_to_marks_split("چوبی", "chubiye"))

    def test_split_keeps_an_ezafe_the_plain_word_spells(self):
        base, full, _ = finglish_to_marks_split("گنده‌ی", "gondeye")
        self.assertEqual(base, full)
        self.assertTrue(full.endswith("ی" + KASRA))

    def test_unalignable_is_reported(self):
        marked, problem = finglish_to_marks("خونه", "mikonam")
        self.assertIsNone(marked)
        self.assertIn("cannot align", problem)

    def test_unknown_character_is_reported(self):
        marked, problem = finglish_to_marks("خونه", "khun3")
        self.assertIsNone(marked)
        self.assertIn("unknown", problem)


if __name__ == "__main__":
    unittest.main()
