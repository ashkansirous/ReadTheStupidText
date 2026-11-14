"""Builds the approved vowel-mark list for a Farsi starter (Slice 154): {plain word -> marked word}.

Marks come ONLY from the user's approved pronunciations: choice "mine" uses the user's Finglish, "guess" uses the
guess in words.json, anything else is left out. Words the converter cannot align are listed and left out.

The user wrote each word's Finglish while looking at ONE sentence (its sentence in words.json), so an ezafe they wrote
(a trailing e / ye the plain word does not spell) belongs to that occurrence only: "marks" holds the word without it
and applies everywhere, "ezafe" holds the with-ezafe form for that sentence and word position only.

    python build_marks_list.py <answers dir> <words.json> <choices dir> <out json>

The output is {"marks": {plain -> marked}, "ezafe": [{page, sentence, word (index among the sentence's words), plain, marked}], "spoken": [{page, sentence, replaces, text}]}: "spoken" are the user's replacements for
phrases the voice leaves silent (files p<page>[-<group>].json in the choices dir), applied to the spoken text only, before the marks.
"""
import glob
import json
import os
import re
import sys

from apply_marks import WORD
from finglish_to_marks import finglish_to_marks_split

CHOICE_MINE, CHOICE_GUESS = "mine", "guess"


def load_answers(folder):
    return [json.load(open(p, encoding="utf8")) for p in sorted(glob.glob(os.path.join(folder, "*.json")))]


def approved_finglish(answer, guesses):
    if answer["choice"] == CHOICE_MINE:
        return answer["finglish"]
    if answer["choice"] == CHOICE_GUESS:
        return guesses.get(answer["word"], "")
    return None


def build(answers_dir, words_path):
    guesses = {w["word"]: w["guess"] for w in json.load(open(words_path, encoding="utf8"))}
    marks, ezafe, failed = {}, [], []
    occurrences = {w["word"]: w for w in json.load(open(words_path, encoding="utf8"))}
    for answer in load_answers(answers_dir):
        finglish = approved_finglish(answer, guesses)
        if not finglish:
            continue
        base, marked, problem = finglish_to_marks_split(answer["word"], finglish)
        if not base:
            failed.append((answer["word"], finglish, problem))
            continue
        marks[answer["word"]] = base
        if marked != base:
            ezafe.append(ezafe_entry(occurrences[answer["word"]], marked, failed))
    return marks, [e for e in ezafe if e], failed


def ezafe_entry(occurrence, marked, failed):
    """The with-ezafe form at the position of the word in the sentence the user was looking at."""
    words = [m.group(0) for m in WORD.finditer(occurrence["sentence"])]
    if occurrence["word"] not in words:
        failed.append((occurrence["word"], marked, "word not found in its sentence"))
        return None
    return {"page": occurrence["page"], "sentence": occurrence["sentence"], "word": words.index(occurrence["word"]),
            "plain": occurrence["word"], "marked": marked}


def load_spoken(choices_dir, words_path):
    """The user's spoken replacements; "sentence" is the plain sentence of that page that contains the phrase."""
    words = json.load(open(words_path, encoding="utf8"))
    spoken = []
    for path in sorted(glob.glob(os.path.join(choices_dir, "p*.json"))):
        choice = json.load(open(path, encoding="utf8"))
        page = int(re.match(r"p(\d+)", os.path.basename(path)).group(1))
        sentences = sorted({w["sentence"] for w in words if w["page"] == page and choice["replaces"] in w["sentence"]})
        for sentence in sentences:
            spoken.append({"page": page, "sentence": sentence, "replaces": choice["replaces"], "text": choice["text"]})
    return spoken


def main(argv):
    if len(argv) != 5:
        print(__doc__)
        return 2
    marks, ezafe, failed = build(argv[1], argv[2])
    data = {"marks": dict(sorted(marks.items())), "ezafe": sorted(ezafe, key=lambda e: (e["page"], e["word"], e["plain"])), "spoken": load_spoken(argv[3], argv[2])}
    with open(argv[4], "w", encoding="utf8", newline=chr(10)) as out:
        json.dump(data, out, ensure_ascii=False, indent=1)
        out.write("\n")
    print(f"{len(marks)} marked words, {len(ezafe)} ezafe occurrences and {len(data['spoken'])} spoken replacements written; {len(failed)} failed")
    for word, finglish, problem in failed:
        print(f"FAILED\t{word}\t{finglish}\t{problem}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
