"""Finglish -> Farsi diacritics (Slice 154 review page).

Given a plain Farsi word and the user's Finglish pronunciation of it, aligns the consonants and adds the marks the
Finglish shows: fatha (a), kasra (e), damma (o) on the consonant before the vowel, shadda for a doubled consonant, and the
ezafe kasra on the last letter for a trailing "e". Long vowels (aa, i, u/o) are matched to the letters alef, yeh, waw and get
no mark. Deterministic (first alignment found, options tried in a fixed order); when nothing aligns it returns a reason
instead of guessing. Run with plain `python`:

    python finglish_to_marks.py <farsi word> <finglish>     prints the marked word, or what could not be aligned
    python finglish_to_marks.py --json words.json           [{"word": ..., "finglish": ...}, ...]
"""
import json
import re
import sys

FATHA, KASRA, DAMMA, SHADDA, ZWNJ = "َ", "ِ", "ُ", "ّ", "‌"
SUKUN, TANWIN_FATHA = "ْ", "ً"
SHORT_VOWEL_MARK = {"a": FATHA, "e": KASRA, "o": DAMMA}
SHORT_VOWELS = "aeo"
HE_VOWELS = "ae"
DIGRAPHS = ("kh", "sh", "ch", "gh", "zh", "aa", "oo", "ou", "ee", "ii")
VOWEL_LETTERS = "اویآ"
BOUNDARY = "|"
WORD_BREAKS = ZWNJ + " "
FINAL_HE = "ه"
AYN = "ع"
SILENT_WAW_AFTER = "خ"
EZAFE_YE = ["y", "e"]

CONSONANTS = {
    "ب": ["b"], "پ": ["p"], "ت": ["t"], "ط": ["t"], "ث": ["s"], "س": ["s"], "ص": ["s"], "ج": ["j"], "چ": ["ch"],
    "ح": ["h"], "ه": ["h"], "خ": ["kh"], "د": ["d"], "ذ": ["z"], "ز": ["z"], "ض": ["z"], "ظ": ["z"], "ر": ["r"],
    "ژ": ["zh"], "ش": ["sh"], "ع": ["'"], "ئ": ["'"], "ء": ["'"], "غ": ["gh"], "ق": ["gh", "q"], "ف": ["f"],
    "ک": ["k"], "گ": ["g"], "ل": ["l"], "م": ["m"], "ن": ["n"], "و": ["v", "w"], "ی": ["y"],
}
LONG_VOWEL_LETTER = {"aa": "ا", "i": "ی", "ee": "ی", "ii": "ی", "u": "و", "o": "و", "oo": "و", "ou": "و"}
WAW_UNITS = ("u", "o", "oo", "ou")
YEH_UNITS = ("i", "ee", "ii")
VOWEL_UNITS = ("a", "e", "o", "u", "i", "aa", "oo", "ou", "ee", "ii")
GLIDE = "y"
NEVER_DOUBLED = (BOUNDARY, GLIDE) + VOWEL_UNITS


def tokenize(finglish):
    """Finglish -> list of (unit, doubled). Raises ValueError on a character it does not know."""
    text = re.sub(r"[\s\-]+", BOUNDARY, finglish.strip().lower().replace("â", "aa")).strip(BOUNDARY)
    units, i = [], 0
    while i < len(text):
        unit = _next_unit(text, i)
        i += len(unit)
        doubled = unit not in NEVER_DOUBLED and text.startswith(unit, i)
        if doubled:
            i += len(unit)
        units.append((unit, doubled))
    return units


def _next_unit(text, i):
    for token in DIGRAPHS:
        if text.startswith(token, i):
            return token
    if re.match(r"[a-z'|]", text[i]):
        return text[i]
    raise ValueError(f"unknown character {text[i]!r}")


def align(word, units):
    """Returns per-letter [letter, shadda, vowel mark] rows, or None when the Finglish does not fit the letters."""
    rows = [[ch, False, ""] for ch in word]
    return rows if _Aligner(word, units, rows).run(0, 0) else None


class _Aligner:
    def __init__(self, word, units, rows):
        self.word, self.units, self.rows = word, units, rows
        self.ezafe = None  # (row, mark and shadda without it) when the Finglish ends in an ezafe the letters do not spell; None for a spelled-out one

    def run(self, li, ui):
        if li < len(self.word) and self.word[li] in WORD_BREAKS:
            has_boundary = ui < len(self.units) and self.units[ui][0] == BOUNDARY
            return self.run(li + 1, ui + 1 if has_boundary else ui)
        if li == len(self.word):
            return self._leftover(ui)
        options = (self._consonant, self._vowel_letter, self._final_he, self._silent_waw, self._waw_vaa, self._tanwin, self._glide, self._silent_final_ayn)
        return any(option(li, ui) for option in options)

    def _glide(self, li, ui):
        """A y between two vowels may be a glide with no letter of its own (tried last: letters are preferred)."""
        if ui == 0 or ui + 1 >= len(self.units) or self.units[ui][0] != GLIDE:
            return False
        return self.units[ui - 1][0] in VOWEL_UNITS and self.units[ui + 1][0] in VOWEL_UNITS and self.run(li, ui + 1)

    def _silent_final_ayn(self, li, ui):
        """A word-final ayn or hamza may be silent when the Finglish omits it (shoru for shoru')."""
        at_end = li == len(self.word) - 1 or self.word[li + 1] in WORD_BREAKS
        return self.word[li] in "عء" and at_end and self.run(li + 1, ui)

    def _leftover(self, ui):
        rest = [u for u, _ in self.units[ui:]]
        if not rest:
            return True
        last = self._last_letter_row()
        if rest == ["e"] and last and last[0] not in VOWEL_LETTERS + FINAL_HE:
            return self._ezafe_mark(last)
        if rest == EZAFE_YE and last and last[0] == "ی" and not last[2]:
            return self._ezafe_mark(last)
        if rest == EZAFE_YE and last:
            self.rows.append([ZWNJ + "ی", False, ""])
            self.ezafe = (self.rows[-1], None, False)
            return True
        return False

    def _ezafe_mark(self, row):
        self.ezafe = (row, row[2], row[1])
        row[2] = KASRA
        return True

    def _note_final_e(self, li, row):
        """A final "e" on the last letter is an ezafe unless the letters spell it (ye after a ZWNJ or a long vowel letter)."""
        spelled = row[0] == "ی" and li > 0 and self.word[li - 1] in ZWNJ + "او"
        if row[2] == KASRA and li == len(self.word) - 1 and not spelled:
            self.ezafe = (row, SUKUN, False)

    def _last_letter_row(self):
        letters = [r for r in self.rows if len(r[0]) == 1 and r[0] != ZWNJ]
        return letters[-1] if letters else None

    def _consonant(self, li, ui):
        if ui >= len(self.units):
            return False
        unit, doubled = self.units[ui]
        if unit not in CONSONANTS.get(self.word[li], []):
            return False
        if doubled and li + 1 < len(self.word) and self.word[li + 1] == self.word[li] and self._split_double(li, ui):
            return True
        row = self.rows[li]
        row[1] = doubled and not (li > 0 and self.word[li - 1] in VOWEL_LETTERS)
        has_vowel = ui + 1 < len(self.units) and self.units[ui + 1][0] in SHORT_VOWELS
        options = [self.units[ui + 1][0], ""] if has_vowel else [""]
        if has_vowel and options[0] == "a" and li + 1 < len(self.word) and self.word[li + 1] == "ا":
            options.reverse()
        for vowel in options:
            if self._try_vowel(li, ui, row, vowel):
                return True
        row[1], row[2] = False, ""
        return False

    def _needs_sukun(self, li, ui, row):
        """A consonant with no vowel shown (a cluster like the khe of saakhtan, or word-final) gets a sukun."""
        if row[1] or ui + 1 < len(self.units) and self.units[ui + 1][0] in VOWEL_UNITS:
            return False
        after = self.word[li + 1] if li + 1 < len(self.word) else ""
        return not after or after in WORD_BREAKS or after in CONSONANTS

    def _split_double(self, li, ui):
        """A doubled consonant whose letter appears twice in the Farsi uses both letters and takes no shadda."""
        unit = self.units[ui][0]
        self.units[ui:ui + 1] = [(unit, False), (unit, False)]
        if self.run(li, ui):
            return True
        self.units[ui:ui + 2] = [(unit, True)]
        return False

    def _try_vowel(self, li, ui, row, vowel):
        row[2] = ""
        if not vowel:
            row[2] = SUKUN if self._needs_sukun(li, ui, row) else ""
            if self.run(li + 1, ui + 1):
                return True
            row[2] = ""
            return False
        if self._silent_he_after(li, ui) and self._mark_before_silent_he(li, ui, row, vowel):
            return True
        row[2] = SHORT_VOWEL_MARK[vowel]
        if self.run(li + 1, ui + 2):
            self._note_final_e(li, row)
            return True
        return False

    def _silent_he_after(self, li, ui):
        """True when the letter after li is a he that is word-final or before a break, and the Finglish fits it."""
        after = li + 2
        if li + 1 >= len(self.word) or self.word[li + 1] != FINAL_HE:
            return False
        if after < len(self.word):
            return self.word[after] in WORD_BREAKS
        return [u for u, _ in self.units[ui + 2:]] in ([], EZAFE_YE)

    def _mark_before_silent_he(self, li, ui, row, vowel):
        row[2] = SHORT_VOWEL_MARK[vowel] if vowel in HE_VOWELS or self.word[li] in "یو" else ""
        if self.run(li + 2, ui + 2):
            return True
        row[2] = ""
        return False

    def _vowel_letter(self, li, ui):
        letter = self.word[li]
        if (letter not in VOWEL_LETTERS and not (letter == AYN and li == 0)) or ui >= len(self.units):
            return False
        unit = self.units[ui][0]
        if letter == "ا" and li == 0 and self._initial_alef_u_i(unit) and self.run(li + 2, ui + 1):
            return True
        if LONG_VOWEL_LETTER.get(unit) == letter or (letter == "آ" and unit == "aa") or (letter == "ا" and unit == "a" and li > 0):
            return self.run(li + 1, ui + 1)
        if letter in ("ا", AYN) and unit in SHORT_VOWELS and li == 0:
            self.rows[li][2] = SHORT_VOWEL_MARK[unit]
            if self.run(li + 1, ui + 1):
                return True
            self.rows[li][2] = ""
        return False

    def _initial_alef_u_i(self, unit):
        """An initial alef followed by waw or yeh and read u / i (umad, imruz): one sound, no mark."""
        following = self.word[1] if len(self.word) > 1 else ""
        return (unit in WAW_UNITS and following == "و") or (unit in YEH_UNITS and following == "ی")

    def _final_he(self, li, ui):
        """A he at the end, or before a ZWNJ, may be the short vowel a or e itself; its mark goes on the letter before."""
        if self.word[li] != FINAL_HE or ui >= len(self.units):
            return False
        at_end = li == len(self.word) - 1
        before_zwnj = not at_end and self.word[li + 1] == ZWNJ
        if self.units[ui][0] not in ("a", "e") or not (before_zwnj or (at_end and ui == len(self.units) - 1)):
            return False
        prev = self.rows[li - 1] if li > 0 and self.word[li - 1] != ZWNJ else None
        if prev is None or prev[0] in VOWEL_LETTERS or prev[2]:
            return self.run(li + 1, ui + 1)
        prev[2] = SHORT_VOWEL_MARK[self.units[ui][0]]
        if self.run(li + 1, ui + 1):
            return True
        prev[2] = ""
        return False

    def _waw_vaa(self, li, ui):
        """Finglish vaa for a waw with no alef after it (hichvaaght): the waw stands for both sounds."""
        follows_alef = li + 1 < len(self.word) and self.word[li + 1] == "ا"
        pair = [u for u, _ in self.units[ui:ui + 2]]
        return self.word[li] == "و" and not follows_alef and pair in (["v", "aa"], ["w", "aa"]) and self.run(li + 1, ui + 2)

    def _tanwin(self, li, ui):
        """A tanwin fatha (aslan) stands for the sound an: it keeps its place and takes no extra mark."""
        return self.word[li] == TANWIN_FATHA and ui < len(self.units) and self.units[ui][0] == "n" and self.run(li + 1, ui + 1)

    def _silent_waw(self, li, ui):
        """The waw after khe (khaa, kho) is silent and is skipped."""
        return self.word[li] == "و" and li > 0 and self.word[li - 1] == SILENT_WAW_AFTER and not self.rows[li - 1][2] and self.run(li + 1, ui)


def _join(rows):
    return "".join(letter + (SHADDA if shadda else "") + mark for letter, shadda, mark in rows)


def finglish_to_marks_split(word, finglish):
    """Returns (base, with ezafe, None) or (None, None, reason). "base" is the word marked without the ezafe a trailing
    "e" / "ye" of the Finglish adds when the plain word does not spell it; equal to "with ezafe" when there is none."""
    try:
        units = tokenize(finglish)
    except ValueError as err:
        return None, None, str(err)
    if not units:
        return None, None, "empty Finglish"
    aligner_rows = [[ch, False, ""] for ch in word]
    aligner = _Aligner(word, units, aligner_rows)
    if not aligner.run(0, 0):
        return None, None, f"cannot align {word!r} with {finglish!r}"
    full = _join(aligner_rows)
    if aligner.ezafe is None:
        return full, full, None
    row, mark, shadda = aligner.ezafe
    if mark is None:
        aligner_rows.remove(row)
    else:
        row[2], row[1] = mark, shadda
    return _join(aligner_rows), full, None


def finglish_to_marks(word, finglish):
    """Returns (marked word, None) or (None, reason)."""
    base, full, problem = finglish_to_marks_split(word, finglish)
    return (full, None) if full else (None, problem)


def main(argv):
    if len(argv) == 3 and argv[1] == "--json":
        entries = json.load(open(argv[2], encoding="utf8"))
        pairs = [(e["word"], e.get("finglish", "")) for e in entries]
    elif len(argv) == 3:
        pairs = [(argv[1], argv[2])]
    else:
        print(__doc__)
        return 2
    failed = 0
    for word, finglish in pairs:
        marked, problem = finglish_to_marks(word, finglish)
        print(f"{word}\t{finglish}\t{marked if marked else 'CANNOT ALIGN: ' + problem}")
        failed += marked is None
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
