namespace DinoReader.Core;

// The names and plain words of the ten-voice rosters, in the order of TenVoiceSlots: three narrators (granny, grandad, whisperer), then mummy, mouse,
// bunny, princess, bee, witch, hen, troll, giant, bear, fox, knight, piglet, chick, dragon. Each language is written for itself, not translated
// word for word. Machine-drafted: they wait for a native speaker, like every other Dino string.
public static partial class CharacterRoster
{
    private static (string Name, string Hint)[] FrenchWords() =>
    [
        ("Mamie Conteuse", "douce et chaleureuse"), ("Papi Conteur", "grave et réconfortant"), ("Souffle du Soir", "doux et lent"),
        ("Maman Câline", "chaleureuse et gentille"), ("Souricette", "toute petite et aiguë"), ("Petite Lapine", "douce et sautillante"),
        ("Princesse Courage", "claire et brillante"), ("Abeille Zouzou", "pétillante et vive"), ("Sorcière Grincheuse", "rauque et rusée"),
        ("Mamie Poule", "lente et douillette"), ("Troll Grognon", "rude et grave"), ("Géant Gentil", "grand, grave et lent"),
        ("Papy Ours", "grave et bon"), ("Renard Rusé", "vif et chuchoté"), ("Chevalier Vaillant", "fier et clair"),
        ("Petit Cochon", "petit et vif"), ("Poussin Piou", "gai et pépiant"), ("Dragon Ronflant", "grondant et fort"),
    ];

    private static (string Name, string Hint)[] SpanishWords() =>
    [
        ("Abuela Cuentacuentos", "cálida y dulce"), ("Abuelo Cuentacuentos", "grave y acogedor"), ("Susurro de Noche", "suave y lento"),
        ("Mamá Cariñosa", "cálida y amable"), ("Ratoncita Chillona", "diminuta y aguda"), ("Conejita Saltarina", "suave y saltarina"),
        ("Princesa Valiente", "clara y brillante"), ("Abejita Zumbona", "burbujeante y rápida"), ("Bruja Gruñona", "ronca y astuta"),
        ("Gallina Abuela", "lenta y calentita"), ("Troll Gruñón", "áspero y grave"), ("Gigante Amable", "grande, grave y lento"),
        ("Abuelo Oso", "grave y bondadoso"), ("Zorro Astuto", "rápido y susurrante"), ("Caballero Valiente", "audaz y claro"),
        ("Cerdito Travieso", "pequeño y rápido"), ("Pollito Pío", "alegre y piador"), ("Dragón Retumbante", "retumbante y fuerte"),
    ];

    private static (string Name, string Hint)[] PortugueseWords() =>
    [
        ("Vovó Contadora", "calorosa e suave"), ("Vovô Contador", "grave e aconchegante"), ("Sussurro da Noite", "suave e devagar"),
        ("Mamãe Carinhosa", "calorosa e gentil"), ("Ratinha Pequenina", "miúda e fininha"), ("Coelhinha Saltitante", "macia e saltitante"),
        ("Princesa Corajosa", "clara e brilhante"), ("Abelhinha Zumbidora", "borbulhante e rápida"), ("Bruxa Rabugenta", "rouca e esperta"),
        ("Galinha Vovó", "lenta e quentinha"), ("Troll Resmungão", "rude e grave"), ("Gigante Bondoso", "grande, grave e lento"),
        ("Vovô Urso", "grave e bondoso"), ("Raposa Esperta", "ligeira e sussurrada"), ("Cavaleiro Valente", "ousado e claro"),
        ("Porquinho Travesso", "pequeno e rápido"), ("Pintinho Piu", "alegre e piador"), ("Dragão Estrondoso", "estrondoso e forte"),
    ];

    private static (string Name, string Hint)[] RussianWords() =>
    [
        ("Бабушка-сказочница", "тёплая и ласковая"), ("Дедушка-сказочник", "низкий и уютный"), ("Шёпот ночи", "тихий и медленный"),
        ("Добрая мама", "тёплая и добрая"), ("Мышка-пискушка", "крошечная и тонкая"), ("Зайка-попрыгайка", "мягкая и прыгучая"),
        ("Храбрая принцесса", "ясная и звонкая"), ("Пчёлка Жужа", "бойкая и быстрая"), ("Ворчливая ведьма", "хриплая и хитрая"),
        ("Бабушка-наседка", "медленная и уютная"), ("Тролль-ворчун", "грубый и низкий"), ("Добрый великан", "большой, низкий, неспешный"),
        ("Дедушка Медведь", "низкий и добрый"), ("Хитрый лис", "быстрый и шепчущий"), ("Отважный рыцарь", "смелый и чёткий"),
        ("Поросёнок-озорник", "маленький и быстрый"), ("Цыплёнок Пик", "весёлый и звонкий"), ("Грохочущий дракон", "гулкий и громкий"),
    ];

    private static (string Name, string Hint)[] JapaneseWords() =>
    [
        ("おはなしおばあちゃん", "あたたかくて やさしい"), ("おはなしおじいちゃん", "低くて ほっとする"), ("おやすみのささやき", "しずかで ゆっくり"),
        ("やさしいおかあさん", "あたたかくて やさしい"), ("ちびねずみ", "小さくて 高い声"), ("ぴょんぴょんうさぎ", "ふんわり はねる声"),
        ("ゆうかんなおひめさま", "はっきり きらきら"), ("ぶんぶんみつばち", "はずんで すばやい"), ("ぷんぷんまじょ", "かすれて ずるがしこい"),
        ("おばあちゃんどり", "ゆっくり ぽかぽか"), ("ぶつぶつトロール", "あらくて 低い"), ("やさしいきょじん", "大きくて 低くて ゆっくり"),
        ("くまのおじいさん", "低くて やさしい"), ("ずるいきつね", "すばしこい ささやき"), ("ゆうかんなきし", "力強く はっきり"),
        ("いたずらこぶた", "小さくて すばやい"), ("ぴよぴよひよこ", "げんきな さえずり"), ("ごろごろドラゴン", "ひびく 大きな声"),
    ];

    private static (string Name, string Hint)[] ArabicWords() =>
    [
        ("الجدّة الحكّاءة", "دافئة وحنونة"), ("الجدّ الحكّاء", "عميق ومريح"), ("همسة المساء", "هادئ وبطيء"),
        ("ماما الحنون", "دافئة ولطيفة"), ("الفأرة الصغيرة", "صغيرة وحادّة"), ("الأرنبة القفّازة", "ناعمة وتقفز"),
        ("الأميرة الشجاعة", "واضحة ومشرقة"), ("النحلة الطنّانة", "مرحة وسريعة"), ("الساحرة النكدة", "خشنة وماكرة"),
        ("الدجاجة الجدّة", "بطيئة ودافئة"), ("العفريت العبوس", "خشن وغليظ"), ("العملاق الطيّب", "كبير وعميق وبطيء"),
        ("الجدّ الدبّ", "عميق وطيّب"), ("الثعلب الماكر", "سريع وهامس"), ("الفارس الشجاع", "جريء وواضح"),
        ("الخنزير المشاكس", "صغير وسريع"), ("الكتكوت المرح", "مرح وزقزقة"), ("التنّين الهادر", "مدوٍّ وعالٍ"),
    ];

    private static (string Name, string Hint)[] DanishWords() =>
    [
        ("Bedstemor Fortæller", "varm og blid"), ("Bedstefar Fortæller", "dyb og hyggelig"), ("Godnat-hvisker", "blød og langsom"),
        ("Kære Mor", "varm og venlig"), ("Lille Mus", "lillebitte og høj"), ("Hoppekaninen", "blød og hoppende"),
        ("Modig Prinsesse", "klar og lys"), ("Summebi", "boblende og hurtig"), ("Sur Heks", "hæs og snedig"),
        ("Mormor Høne", "langsom og hyggelig"), ("Gnavne Trold", "grov og dyb"), ("Venlig Kæmpe", "stor, dyb og langsom"),
        ("Bedstefar Bjørn", "dyb og god"), ("Snu Ræv", "hurtig og hviskende"), ("Modig Ridder", "dristig og tydelig"),
        ("Frække Gris", "lille og hurtig"), ("Pip-Kylling", "glad og piblende"), ("Rumlende Drage", "buldrende og kraftig"),
    ];

    private static (string Name, string Hint)[] SwedishWords() =>
    [
        ("Farmor Berättare", "varm och mild"), ("Farfar Berättare", "djup och mysig"), ("Godnattviskaren", "mjuk och långsam"),
        ("Snälla Mamma", "varm och vänlig"), ("Lilla Musen", "pytteliten och hög"), ("Hoppakaninen", "mjuk och hoppande"),
        ("Modiga Prinsessan", "klar och ljus"), ("Surrbiet", "sprudlande och snabb"), ("Griniga Häxan", "hes och listig"),
        ("Mormor Höna", "långsam och mysig"), ("Bråkiga Trollet", "grov och djup"), ("Snälla Jätten", "stor, djup och långsam"),
        ("Morfar Björn", "djup och god"), ("Listiga Räven", "snabb och viskande"), ("Tappra Riddaren", "djärv och tydlig"),
        ("Busiga Grisen", "liten och snabb"), ("Pip-Kycklingen", "glad och pipig"), ("Mullrande Draken", "dånande och stark"),
    ];

    private static (string Name, string Hint)[] NorwegianWords() =>
    [
        ("Bestemor Forteller", "varm og myk"), ("Bestefar Forteller", "dyp og lun"), ("Nattahviskeren", "myk og langsom"),
        ("Snille Mamma", "varm og vennlig"), ("Lille Mus", "bitteliten og høy"), ("Hoppekaninen", "myk og hoppende"),
        ("Modig Prinsesse", "klar og lys"), ("Summebien", "boblende og rask"), ("Sur Heks", "hes og lur"),
        ("Bestemor Høne", "langsom og lun"), ("Grinete Troll", "grov og dyp"), ("Snill Kjempe", "stor, dyp og langsom"),
        ("Bestefar Bjørn", "dyp og god"), ("Lur Rev", "rask og hviskende"), ("Modig Ridder", "dristig og tydelig"),
        ("Frekk Gris", "liten og rask"), ("Pip-Kylling", "glad og kvitrende"), ("Buldrende Drage", "drønnende og sterk"),
    ];

    private static (string Name, string Hint)[] FinnishWords() =>
    [
        ("Satumummo", "lämmin ja lempeä"), ("Satuvaari", "syvä ja tunnelmallinen"), ("Hyvänyön kuiskaaja", "pehmeä ja hidas"),
        ("Hellä äiti", "lämmin ja ystävällinen"), ("Pikku hiiri", "pienenpieni ja korkea"), ("Hyppivä kaniini", "pehmeä ja ponnahteleva"),
        ("Rohkea prinsessa", "kirkas ja iloinen"), ("Surisevä mehiläinen", "kuplivan nopea"), ("Äkäinen noita", "käheä ja ovela"),
        ("Mummokana", "hidas ja lämmin"), ("Nurja peikko", "karhea ja matala"), ("Ystävällinen jättiläinen", "iso, matala ja hidas"),
        ("Karhuukki", "matala ja hyväntahtoinen"), ("Ovela kettu", "nopea ja kuiskaava"), ("Urhea ritari", "rohkea ja selkeä"),
        ("Veikeä possu", "pieni ja nopea"), ("Piipittävä tipu", "iloinen ja piipittävä"), ("Jyrisevä lohikäärme", "jylisevä ja voimakas"),
    ];

    private static (string Name, string Hint)[] DutchWords() =>
    [
        ("Vertel-oma", "warm en zacht"), ("Vertel-opa", "diep en gezellig"), ("Welterusten-fluisteraar", "zacht en langzaam"),
        ("Lieve Mama", "warm en vriendelijk"), ("Piepklein Muisje", "heel klein en hoog"), ("Huppelkonijntje", "zacht en springerig"),
        ("Dappere Prinses", "helder en stralend"), ("Zoemend Bijtje", "bruisend en snel"), ("Chagrijnige Heks", "schor en slim"),
        ("Oma Kip", "langzaam en gezellig"), ("Mopperende Trol", "ruw en diep"), ("Vriendelijke Reus", "groot, diep en langzaam"),
        ("Opa Beer", "diep en lief"), ("Slimme Vos", "snel en fluisterend"), ("Dappere Ridder", "stoer en duidelijk"),
        ("Ondeugend Biggetje", "klein en snel"), ("Piepend Kuikentje", "vrolijk en tjilpend"), ("Rommelende Draak", "dreunend en luid"),
    ];
}
