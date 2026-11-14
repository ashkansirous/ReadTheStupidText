"""Sound words under test: language -> sound -> spellings. The first spelling of each sound is the BASELINE every stretch ratio
is measured against (one baseline per sound and language, the plain word the skill recommends starting from).

ROOTS lists the tokens an ASR transcript may contain for the word to count as heard: an exact token match, never a substring.
"""

SPELLINGS = {
    "en": {
        "bang": ["Dang!", "Daaang!", "Daaannng!", "Dang! Dang!", "Dang... dang... dang!", "Dang! Dang! Dang!"],
        "whoosh": ["Whoosh!", "Whooosh!", "Woooooshhhh!", "Whoosh! Whoosh!", "Whoosh... whoosh!", "Whooooosh... whoosh!"],
        "knock": ["Knock!", "Knock knock!", "Knock! Knock! Knock!", "Knoooock!", "Knock... knock... knock!", "Tok tok tok!"],
        "animal": ["Oink!", "Oiiink!", "Oink, oink, oink!", "Oink, oink, oiiink!", "Ooooink!", "Oink... oink!"],
    },
    "es": {
        "bang": ["¡Pum!", "¡Puuum!", "¡Puuuuum!", "¡Pum! ¡Pum!", "¡Pum... pum... pum!", "¡Pum! ¡Pum! ¡Pum!"],
        "whoosh": ["¡Fiu!", "¡Fium!", "¡Fiuuu!", "¡Fiiiiuuuu!", "¡Fiu! ¡Fiu!", "¡Fiu... fiu!", "¡Fiuuu... fiu!"],
        "knock": ["¡Toc!", "¡Toc, toc!", "¡Toc! ¡Toc! ¡Toc!", "¡Toooc!", "¡Toc... toc... toc!", "¡Tocatoc!"],
        "animal": ["¡Muu!", "¡Muuu!", "¡Muuuuuu!", "¡Muu, muu, muuu!", "¡Mu! ¡Mu!", "¡Muu... muuu!"],
    },
    "zh": {
        "bang": ["砰！", "砰砰砰！", "砰——", "嘭！", "砰！砰！", "砰……砰……砰！"],
        "whoosh": ["嗖！", "嗖嗖嗖！", "呼——", "呼呼呼！", "嗖——嗖！", "呼……呼！"],
        "knock": ["咚！", "咚咚咚！", "咚——咚！", "咚咚！", "咚……咚……咚！", "叩叩叩！"],
        "animal": ["哞！", "哞哞！", "哞——", "汪汪汪！", "喵喵！", "喵——喵！"],
    },
    "fa": {
        "bang": ["دنگ!", "دَنگ!", "دنگگگ!", "دنگ! دنگ! دنگ!", "دَنگ! دَنگ! دَنگ!", "دنگ… دنگ… دنگ!", "بوووم!"],
        "whoosh": ["ووش!", "ووووش!", "ووش! ووش!", "ووش… ووش!", "وووووش… ووش!", "ویژژژ!"],
        "knock": ["تق!", "تق تق تق!", "تَق… تَق… تَق!", "تققق!", "تق! تق!", "تَقْ تَقْ!"],
        "animal": ["هاپ هاپ!", "هاااپ هاپ!", "هاپ… هاپ… هاپ!", "میو میو!", "میوووو!", "بع بع!"],
    },
}

# What an ASR may write for the word (matched fuzzily; Chinese and Persian also list homophones, so a hit there is a weaker proof).
ROOTS = {
    "en": {"bang": ["dang", "bang"], "whoosh": ["whoosh", "woosh", "whoos", "wush", "whush"],
           "knock": ["knock", "nock", "tok", "knoc"], "animal": ["oink", "oynk"]},
    "es": {"bang": ["pum", "bum", "boom", "tum", "pom"],
           "whoosh": ["fium", "fiu", "fiuu", "fiuuu", "fio", "fiel", "viu", "chiu", "fuu"],
           "knock": ["toc", "tok", "tuk", "tock"], "animal": ["mu", "muu", "muuu", "mo", "moo"]},
    "zh": {"bang": ["砰", "嘭", "碰", "绷", "蹦", "彭", "朋", "蓬"], "whoosh": ["嗖", "呼", "唆", "搜", "收", "苏", "速", "虎", "胡"],
           "knock": ["咚", "叩", "冬", "东", "懂", "动", "扣", "洞", "通", "铛", "当", "登"],
           "animal": ["哞", "汪", "喵", "猫", "模", "谋", "某", "王", "旺", "苗", "妙", "毛"]},
    "fa": {"bang": ["دنگ", "دینگ", "دانگ", "بوم", "بووم", "دونگ"], "whoosh": ["وش", "ووش", "ویژ", "وووش", "ووج", "ویش"],
           "knock": ["تق", "تک", "تاق", "تغ", "طق", "تقق"], "animal": ["هاپ", "هاب", "میو", "مو", "بع", "بیع", "هپ", "ماو"]},
}

# A short sentence per language: the sound goes before it ("lead") or after it ("trail"), the way a book line reads.
CARRIERS = {"en": "Pip ran away.", "es": "Pip salió corriendo.", "zh": "小猪跑开了。", "fa": "بچه خوک فرار کرد."}

# The spellings dino-storytelling section 3 recommends (every one must be synthesised, review 145 item 3/7).
RECOMMENDED = {
    "en": ["Dang! Dang! Dang!", "Whoosh... whoosh!", "Knock! Knock! Knock!", "Oink, oink, oink!", "Oiiink!"],
    "es": ["¡Pum! ¡Pum! ¡Pum!", "¡Fiu! ¡Fiu!", "¡Toc... toc... toc!", "¡Muuu!"],
    "zh": ["砰砰砰！", "咚咚咚！", "嗖嗖嗖！", "汪汪汪！", "喵喵！"],
    "fa": ["دنگ! دنگ! دنگ!", "دنگ… دنگ… دنگ!", "ووش… ووش!", "تق تق تق!", "هاپ هاپ!", "میو میو!"],
}

# Farsi flavours act on the engine through their pace (VoiceFlavours.ShapeOf): Sleepy .85, Big .92, Small 1.08, Bouncy 1.15.
# The pitch shift runs on the finished samples, so it cannot make the engine drop or keep a word and is not simulated.
FLAVOUR_PACES = [0.85, 0.92, 1.08, 1.15]
FLAVOUR_VOICES = ("mana", "ganji", "gyro")
