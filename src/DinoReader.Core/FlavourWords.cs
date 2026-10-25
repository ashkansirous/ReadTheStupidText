namespace DinoReader.Core;

/// <summary>
/// The words one language uses for the characters whose voice should sound different (Slice 113, Decision 117):
/// small animals and small children (<see cref="Small"/>), big ones (<see cref="Big"/>), slow, old ones
/// (<see cref="Sleepy"/>), the words that make any character small ("little", "baby": <see cref="Diminutives"/>) and the size
/// adjectives that make any character big and outrank the age words ("big", "giant": <see cref="BigSize"/>, a subset of the Big words).
/// A multi-word entry ("پدر بزرگ") matches the words in a row. In Chinese and Japanese (no spaces) an entry matches the whole name, or ends it
/// when it is written in Han characters, never a part in the middle (see <see cref="FlavourGuess"/>).
/// Lower-case, compared case-insensitively. Every language's rules carry their own list (<see cref="LanguageRules.Flavours"/>).
/// </summary>
public sealed record FlavourWords(
    IReadOnlySet<string> Small, IReadOnlySet<string> Big, IReadOnlySet<string> Sleepy, IReadOnlySet<string> Diminutives,
    IReadOnlySet<string> BigSize)
{
    /// <summary>Grumpy characters (Slice 116): a grouch, a witch. Checked after the size and age words, so "Grumpy Troll" is still Big.</summary>
    public IReadOnlySet<string> Grumpy { get; init; } = RuleSets.Set("");

    /// <summary>Quiet, sly characters (Slice 116): a fox, a cat, a ghost.</summary>
    public IReadOnlySet<string> Whisper { get; init; } = RuleSets.Set("");

    /// <summary>Tiny, high characters that are not animals of the Small list (Slice 116): a fairy, an elf.</summary>
    public IReadOnlySet<string> Squeaky { get; init; } = RuleSets.Set("");

    /// <summary>This list with the Slice 116 words added, as comma-separated lists.</summary>
    public FlavourWords With(string grumpy, string whisper, string squeaky) =>
        this with { Grumpy = RuleSets.Set(grumpy), Whisper = RuleSets.Set(whisper), Squeaky = RuleSets.Set(squeaky) };

    /// <summary>No words: every character is Normal.</summary>
    public static FlavourWords None { get; } = Of("", "", "", "", "");

    public static FlavourWords English { get; } = Of(
        "lamb, mouse, rabbit, bunny, bird, chick, duckling, kitten, puppy, frog, bee, butterfly, ladybird, squirrel",
        "wolf, bear, giant, elephant, ogre, troll, dragon, hippo, whale, bull, big, great",
        "owl, grandad, grandpa, grandfather, granny, grandma, grandmother, sleepy, old, tortoise, sloth",
        "little, small, tiny, baby",
        "big, great, giant")
        .With("grumpy, grouch, witch, goblin, crab, hag", "fox, cat, snake, ghost, spy, sly", "fairy, elf, pixie, cricket");

    public static FlavourWords French { get; } = Of(
        "agneau, souris, lapin, oiseau, poussin, caneton, chaton, chiot, grenouille, abeille, papillon, écureuil",
        "loup, ours, géant, éléphant, ogre, troll, dragon, hippopotame, baleine, taureau, gros, grosse",
        "hibou, chouette, papi, papy, tortue, paresseux",
        "petit, petite, minuscule, bébé",
        "gros, grosse, géant")
        .With("grincheux, grincheuse, sorcière, gobelin, crabe", "renard, chat, serpent, fantôme, espion", "fée, lutin, elfe, grillon");

    public static FlavourWords Spanish { get; } = Of(
        "cordero, ratón, conejo, pájaro, pollito, patito, gatito, cachorro, rana, abeja, mariposa, ardilla",
        "lobo, oso, gigante, elefante, ogro, troll, dragón, hipopótamo, ballena, toro, grande",
        "búho, lechuza, abuelo, tortuga, perezoso",
        "pequeño, pequeña, chiquito, chiquita, pequeñito, bebé, diminuto",
        "gigante")
        .With("gruñón, gruñona, bruja, duende, cangrejo", "zorro, zorra, gato, serpiente, fantasma, espía", "hada, elfo, grillo");

    public static FlavourWords Portuguese { get; } = Of(
        "cordeiro, ovelhinha, rato, ratinho, coelho, pássaro, passarinho, pintinho, patinho, gatinho, cachorrinho, sapo, abelha, borboleta, esquilo",
        "lobo, urso, gigante, elefante, ogro, troll, dragão, hipopótamo, baleia, touro, grande",
        "coruja, avô, vovô, tartaruga, preguiça",
        "pequeno, pequena, pequenino, bebê, bebé, mini",
        "gigante")
        .With("rabugento, rabugenta, bruxa, goblin, caranguejo", "raposa, raposo, gato, cobra, fantasma, espião", "fada, elfo, grilo");

    public static FlavourWords Russian { get; } = Of(
        "ягнёнок, ягненок, мышонок, мышка, мышь, кролик, зайчик, птичка, птенец, цыплёнок, цыпленок, утёнок, утенок, котёнок, котенок, щенок, лягушка, пчела, бабочка, белка",
        "волк, медведь, великан, слон, огр, тролль, дракон, бегемот, кит, бык, большой",
        "сова, дедушка, дед, черепаха, ленивец",
        "маленький, маленькая, малыш, крошка, малютка, детёныш",
        "большой, великан")
        .With("ворчун, ворчунья, ведьма, гоблин, краб, баба яга", "лиса, лис, кот, кошка, змея, привидение, шпион", "фея, эльф, кузнечик, сверчок");

    public static FlavourWords Persian { get; } = Of(
        "بره, موش, خرگوش, پرنده, جوجه, قورباغه, زنبور, پروانه, سنجاب, گنجشک",
        "گرگ, خرس, غول, فیل, دیو, اژدها, نهنگ, گاو, بزرگ",
        "جغد, پدربزرگ, بابابزرگ, پدر بزرگ, مادر بزرگ, مادربزرگ, لاکپشت, تنبل",
        "کوچولو, کوچک, کوچیک, ریز, نوزاد, بچه",
        "")
        .With("اخمو, جادوگر, غرغرو, خرچنگ", "روباه, گربه, مار, روح, جاسوس", "پری, الف, جیرجیرک");

    public static FlavourWords Arabic { get; } = Of(
        "حمل, خروف, فأر, الفأر, أرنب, الأرنب, عصفور, العصفور, كتكوت, بطة, قطة, ضفدع, نحلة, فراشة, سنجاب",
        "ذئب, الذئب, دب, الدب, عملاق, العملاق, فيل, الفيل, غول, تنين, حوت, ثور, كبير, الكبير",
        "بومة, البومة, جد, الجد, سلحفاة, السلحفاة, كسلان",
        "صغير, صغيرة, الصغير, الصغيرة, رضيع",
        "كبير, الكبير, عملاق, العملاق")
        .With("نكد, عابس, ساحرة, الساحرة, سرطان", "ثعلب, الثعلب, قط, القط, ثعبان, شبح, جاسوس", "جنية, الجنية, قزم, صرصور");

    public static FlavourWords Chinese { get; } = Of(
        "小羊, 羊羔, 老鼠, 小鼠, 兔, 兔子, 小鸟, 小鸡, 小鸭, 小猫, 小狗, 青蛙, 蜜蜂, 蝴蝶, 松鼠",
        "狼, 熊, 巨人, 大象, 怪物, 龙, 河马, 鲸, 公牛",
        "猫头鹰, 爷爷, 乌龟, 树懒",
        "宝宝",
        "")
        .With("巫婆, 小矮人, 螃蟹, 坏脾气", "狐狸, 猫, 蛇, 幽灵, 间谍", "仙女, 精灵, 蟋蟀");

    public static FlavourWords Japanese { get; } = Of(
        "こひつじ, 子羊, ねずみ, ネズミ, うさぎ, ウサギ, ことり, 小鳥, ひよこ, ヒヨコ, 子猫, 子犬, かえる, カエル, みつばち, りす, リス",
        "おおかみ, オオカミ, 狼, くま, クマ, 熊, きょじん, 巨人, ぞう, ゾウ, 象, おに, 鬼, りゅう, 竜, くじら, 鯨",
        "ふくろう, フクロウ, 梟, おじいさん, おじいちゃん, 亀, なまけもの",
        "小さな, ちいさな, ちっちゃな, 小さい, 赤ちゃん, あかちゃん, ちび, チビ",
        "")
        .With("まじょ, 魔女, ゴブリン, かに, カニ, おこりんぼ", "きつね, キツネ, 狐, ねこ, ネコ, 猫, へび, ヘビ, おばけ, オバケ, スパイ", "ようせい, 妖精, エルフ, こおろぎ");

    public static FlavourWords Danish { get; } = Of(
        "lam, mus, kanin, fugl, kylling, andeunge, killing, hvalp, frø, bi, sommerfugl, egern",
        "ulv, bjørn, kæmpe, elefant, trold, drage, flodhest, hval, tyr, stor",
        "ugle, bedstefar, morfar, farfar, skildpadde, dovendyr",
        "lille, små, baby, bitte",
        "stor, kæmpe")
        .With("gnavpotte, sur, heks, nisse, krabbe", "ræv, kat, slange, spøgelse, spion", "fe, alf, græshoppe");

    public static FlavourWords Swedish { get; } = Of(
        "lamm, mus, kanin, fågel, kyckling, ankunge, kattunge, valp, groda, bi, fjäril, ekorre",
        "varg, björn, jätte, elefant, troll, drake, flodhäst, val, tjur, stor",
        "uggla, morfar, farfar, sköldpadda, sengångare",
        "liten, lilla, små, bebis",
        "stor, jätte")
        .With("grinig, griniga, häxa, nisse, krabba", "räv, katt, orm, spöke, spion", "fe, alv, syrsa");

    public static FlavourWords Norwegian { get; } = Of(
        "lam, mus, kanin, fugl, kylling, andunge, kattunge, valp, frosk, bie, sommerfugl, ekorn",
        "ulv, bjørn, kjempe, elefant, troll, drage, flodhest, hval, okse, stor",
        "ugle, bestefar, morfar, farfar, skilpadde, dovendyr",
        "liten, lille, små, baby",
        "stor, kjempe")
        .With("grinebiter, sur, heks, nisse, krabbe", "rev, katt, slange, spøkelse, spion", "fe, alv, siriss");

    public static FlavourWords Finnish { get; } = Of(
        "karitsa, hiiri, kani, lintu, tipu, ankanpoikanen, kissanpentu, pentu, sammakko, mehiläinen, perhonen, orava",
        "susi, karhu, jättiläinen, norsu, peikko, lohikäärme, virtahepo, valas, härkä, iso, suuri",
        "pöllö, isoisä, ukki, kilpikonna, laiskiainen",
        "pieni, pikku, vauva",
        "iso, suuri, jättiläinen")
        .With("nurja, äkäinen, noita, tonttu, rapu", "kettu, kissa, käärme, aave, vakooja", "keiju, haltija, sirkka");

    public static FlavourWords Dutch { get; } = Of(
        "lam, lammetje, muis, muisje, konijn, konijntje, vogel, vogeltje, kuiken, eendje, kitten, poesje, puppy, kikker, bij, vlinder, eekhoorn",
        "wolf, beer, reus, olifant, oger, trol, draak, nijlpaard, walvis, stier, groot, grote",
        "uil, opa, grootvader, schildpad, luiaard",
        "klein, kleine, kleintje, baby",
        "groot, grote, reus")
        .With("mopperkont, chagrijn, heks, kabouter, krab", "vos, kat, slang, spook, spion", "fee, elf, krekel");

    /// <summary>A list from comma-separated word lists.</summary>
    public static FlavourWords Of(string small, string big, string sleepy, string diminutives, string bigSize = "") =>
        new(RuleSets.Set(small), RuleSets.Set(big), RuleSets.Set(sleepy), RuleSets.Set(diminutives), RuleSets.Set(bigSize));
}
