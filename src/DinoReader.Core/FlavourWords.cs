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
    /// <summary>No words: every character is Normal.</summary>
    public static FlavourWords None { get; } = Of("", "", "", "", "");

    public static FlavourWords English { get; } = Of(
        "lamb, mouse, rabbit, bunny, bird, chick, duckling, kitten, puppy, frog, bee, butterfly, ladybird, squirrel",
        "wolf, bear, giant, elephant, ogre, troll, dragon, hippo, whale, bull, big, great",
        "owl, grandad, grandpa, grandfather, granny, grandma, grandmother, sleepy, old, tortoise, sloth",
        "little, small, tiny, baby",
        "big, great, giant");

    public static FlavourWords French { get; } = Of(
        "agneau, souris, lapin, oiseau, poussin, caneton, chaton, chiot, grenouille, abeille, papillon, écureuil",
        "loup, ours, géant, éléphant, ogre, troll, dragon, hippopotame, baleine, taureau, gros, grosse",
        "hibou, chouette, papi, papy, tortue, paresseux",
        "petit, petite, minuscule, bébé",
        "gros, grosse, géant");

    public static FlavourWords Spanish { get; } = Of(
        "cordero, ratón, conejo, pájaro, pollito, patito, gatito, cachorro, rana, abeja, mariposa, ardilla",
        "lobo, oso, gigante, elefante, ogro, troll, dragón, hipopótamo, ballena, toro, grande",
        "búho, lechuza, abuelo, tortuga, perezoso",
        "pequeño, pequeña, chiquito, chiquita, pequeñito, bebé, diminuto",
        "gigante");

    public static FlavourWords Portuguese { get; } = Of(
        "cordeiro, ovelhinha, rato, ratinho, coelho, pássaro, passarinho, pintinho, patinho, gatinho, cachorrinho, sapo, abelha, borboleta, esquilo",
        "lobo, urso, gigante, elefante, ogro, troll, dragão, hipopótamo, baleia, touro, grande",
        "coruja, avô, vovô, tartaruga, preguiça",
        "pequeno, pequena, pequenino, bebê, bebé, mini",
        "gigante");

    public static FlavourWords Russian { get; } = Of(
        "ягнёнок, ягненок, мышонок, мышка, мышь, кролик, зайчик, птичка, птенец, цыплёнок, цыпленок, утёнок, утенок, котёнок, котенок, щенок, лягушка, пчела, бабочка, белка",
        "волк, медведь, великан, слон, огр, тролль, дракон, бегемот, кит, бык, большой",
        "сова, дедушка, дед, черепаха, ленивец",
        "маленький, маленькая, малыш, крошка, малютка, детёныш",
        "большой, великан");

    public static FlavourWords Persian { get; } = Of(
        "بره, موش, خرگوش, پرنده, جوجه, قورباغه, زنبور, پروانه, سنجاب, گنجشک",
        "گرگ, خرس, غول, فیل, دیو, اژدها, نهنگ, گاو, بزرگ",
        "جغد, پدربزرگ, بابابزرگ, پدر بزرگ, مادر بزرگ, مادربزرگ, لاکپشت, تنبل",
        "کوچولو, کوچک, کوچیک, ریز, نوزاد, بچه",
        "");

    public static FlavourWords Arabic { get; } = Of(
        "حمل, خروف, فأر, الفأر, أرنب, الأرنب, عصفور, العصفور, كتكوت, بطة, قطة, ضفدع, نحلة, فراشة, سنجاب",
        "ذئب, الذئب, دب, الدب, عملاق, العملاق, فيل, الفيل, غول, تنين, حوت, ثور, كبير, الكبير",
        "بومة, البومة, جد, الجد, سلحفاة, السلحفاة, كسلان",
        "صغير, صغيرة, الصغير, الصغيرة, رضيع",
        "كبير, الكبير, عملاق, العملاق");

    public static FlavourWords Chinese { get; } = Of(
        "小羊, 羊羔, 老鼠, 小鼠, 兔, 兔子, 小鸟, 小鸡, 小鸭, 小猫, 小狗, 青蛙, 蜜蜂, 蝴蝶, 松鼠",
        "狼, 熊, 巨人, 大象, 怪物, 龙, 河马, 鲸, 公牛",
        "猫头鹰, 爷爷, 乌龟, 树懒",
        "宝宝",
        "");

    public static FlavourWords Japanese { get; } = Of(
        "こひつじ, 子羊, ねずみ, ネズミ, うさぎ, ウサギ, ことり, 小鳥, ひよこ, ヒヨコ, 子猫, 子犬, かえる, カエル, みつばち, りす, リス",
        "おおかみ, オオカミ, 狼, くま, クマ, 熊, きょじん, 巨人, ぞう, ゾウ, 象, おに, 鬼, りゅう, 竜, くじら, 鯨",
        "ふくろう, フクロウ, 梟, おじいさん, おじいちゃん, 亀, なまけもの",
        "小さな, ちいさな, ちっちゃな, 小さい, 赤ちゃん, あかちゃん, ちび, チビ",
        "");

    public static FlavourWords Danish { get; } = Of(
        "lam, mus, kanin, fugl, kylling, andeunge, killing, hvalp, frø, bi, sommerfugl, egern",
        "ulv, bjørn, kæmpe, elefant, trold, drage, flodhest, hval, tyr, stor",
        "ugle, bedstefar, morfar, farfar, skildpadde, dovendyr",
        "lille, små, baby, bitte",
        "stor, kæmpe");

    public static FlavourWords Swedish { get; } = Of(
        "lamm, mus, kanin, fågel, kyckling, ankunge, kattunge, valp, groda, bi, fjäril, ekorre",
        "varg, björn, jätte, elefant, troll, drake, flodhäst, val, tjur, stor",
        "uggla, morfar, farfar, sköldpadda, sengångare",
        "liten, lilla, små, bebis",
        "stor, jätte");

    public static FlavourWords Norwegian { get; } = Of(
        "lam, mus, kanin, fugl, kylling, andunge, kattunge, valp, frosk, bie, sommerfugl, ekorn",
        "ulv, bjørn, kjempe, elefant, troll, drage, flodhest, hval, okse, stor",
        "ugle, bestefar, morfar, farfar, skilpadde, dovendyr",
        "liten, lille, små, baby",
        "stor, kjempe");

    public static FlavourWords Finnish { get; } = Of(
        "karitsa, hiiri, kani, lintu, tipu, ankanpoikanen, kissanpentu, pentu, sammakko, mehiläinen, perhonen, orava",
        "susi, karhu, jättiläinen, norsu, peikko, lohikäärme, virtahepo, valas, härkä, iso, suuri",
        "pöllö, isoisä, ukki, kilpikonna, laiskiainen",
        "pieni, pikku, vauva",
        "iso, suuri, jättiläinen");

    public static FlavourWords Dutch { get; } = Of(
        "lam, lammetje, muis, muisje, konijn, konijntje, vogel, vogeltje, kuiken, eendje, kitten, poesje, puppy, kikker, bij, vlinder, eekhoorn",
        "wolf, beer, reus, olifant, oger, trol, draak, nijlpaard, walvis, stier, groot, grote",
        "uil, opa, grootvader, schildpad, luiaard",
        "klein, kleine, kleintje, baby",
        "groot, grote, reus");

    /// <summary>A list from comma-separated word lists.</summary>
    public static FlavourWords Of(string small, string big, string sleepy, string diminutives, string bigSize = "") =>
        new(RuleSets.Set(small), RuleSets.Set(big), RuleSets.Set(sleepy), RuleSets.Set(diminutives), RuleSets.Set(bigSize));
}
