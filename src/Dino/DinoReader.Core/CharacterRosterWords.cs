namespace DinoReader.Core;

// The names and plain words of the rosters built from Slot lists (ten-voice, and the Kokoro languages of Slice 118), in the order of TenVoiceSlots: three narrators (granny, grandad, whisperer), then mummy, mouse,
// bunny, princess, bee, witch, hen, troll, giant, bear, fox, knight, piglet, chick, dragon, then (Slice 125) the animals lamb, puppy, kitten, duckling, cub, bigbear, wolf, ewe, then (Slice 135) the young farm animals piglet2, piglet3, lamb2, duckling2, puppy2, kitten2 (Spanish and Portuguese only piglet2). Each language is written for itself, not translated
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
        ("Agneau Laineux", "doux et bêlant"), ("Chiot Joueur", "vif et joyeux"),
        ("Chaton Curieux", "minuscule et ronronnant"), ("Caneton Piou", "petit et pépiant"),
        ("Petit Ourson", "petit et doux"), ("Grand Ours Brun", "grave et lent"),
        ("Loup Affamé", "bas et grondant"), ("Brebis Laineuse", "douce et lente"),
        ("Goret Fripon", "minuscule et reniflant"), ("Porcelet Frisé", "vif et pétillant"), ("Agnelet Duveteux", "léger et doux"), ("Caneton Barboteur", "gai et éclaboussant"), ("Chiot Frétillant", "joyeux et chaleureux"), ("Chaton Câlin", "minuscule et tendre"),
    ];

    private static (string Name, string Hint)[] SpanishWords() =>
    [
        ("Abuela Cuentacuentos", "cálida y dulce"), ("Abuelo Cuentacuentos", "grave y acogedor"), ("Susurro de Noche", "suave y lento"),
        ("Mamá Cariñosa", "cálida y amable"), ("Ratoncita Chillona", "diminuta y aguda"), ("Conejita Saltarina", "suave y saltarina"),
        ("Princesa Valiente", "clara y brillante"), ("Abejita Zumbona", "burbujeante y rápida"), ("Bruja Gruñona", "ronca y astuta"),
        ("Gallina Abuela", "lenta y calentita"), ("Troll Gruñón", "áspero y grave"), ("Gigante Amable", "grande, grave y lento"),
        ("Abuelo Oso", "grave y bondadoso"), ("Zorro Astuto", "rápido y susurrante"), ("Caballero Valiente", "audaz y claro"),
        ("Cerdito Travieso", "pequeño y rápido"), ("Pollito Pío", "alegre y piador"), ("Dragón Retumbante", "retumbante y fuerte"),
        ("Corderito Lanudo", "suave y balante"), ("Perrito Juguetón", "vivo y alegre"),
        ("Gatito Curioso", "diminuto y ronroneante"), ("Patito Pío", "pequeño y piador"),
        ("Osito Pequeño", "pequeño y suave"), ("Oso Pardo Grande", "grave y lento"),
        ("Lobo Hambriento", "bajo y gruñón"), ("Oveja Lanuda", "suave y lenta"),
        ("Cochinito Pequeñín", "diminuto y suave"),
    ];

    private static (string Name, string Hint)[] PortugueseWords() =>
    [
        ("Vovó Contadora", "calorosa e suave"), ("Vovô Contador", "grave e aconchegante"), ("Sussurro da Noite", "suave e devagar"),
        ("Mamãe Carinhosa", "calorosa e gentil"), ("Ratinha Pequenina", "miúda e fininha"), ("Coelhinha Saltitante", "macia e saltitante"),
        ("Princesa Corajosa", "clara e brilhante"), ("Abelhinha Zumbidora", "borbulhante e rápida"), ("Bruxa Rabugenta", "rouca e esperta"),
        ("Galinha Vovó", "lenta e quentinha"), ("Troll Resmungão", "rude e grave"), ("Gigante Bondoso", "grande, grave e lento"),
        ("Vovô Urso", "grave e bondoso"), ("Raposa Esperta", "ligeira e sussurrada"), ("Cavaleiro Valente", "ousado e claro"),
        ("Porquinho Travesso", "pequeno e rápido"), ("Pintinho Piu", "alegre e piador"), ("Dragão Estrondoso", "estrondoso e forte"),
        ("Cordeirinho Lanzudo", "macio e balindo"), ("Cachorrinho Brincalhão", "vivo e alegre"),
        ("Gatinho Curioso", "miúdo e ronronante"), ("Patinho Piu", "pequeno e piador"),
        ("Ursinho Pequeno", "pequeno e macio"), ("Urso Pardo Grande", "grave e lento"),
        ("Lobo Faminto", "baixo e rosnado"), ("Ovelha Lanzuda", "macia e lenta"),
        ("Leitãozinho Miúdo", "pequenino e suave"),
    ];

    private static (string Name, string Hint)[] ChineseWords() =>
    [
        ("故事奶奶", "温暖又温柔"), ("故事爷爷", "低沉又舒服"), ("晚安悄悄话", "轻轻的，慢慢的"),
        ("温柔妈妈", "温暖又亲切"), ("小老鼠", "又小又尖"), ("小兔子", "软软的，蹦蹦跳"),
        ("勇敢的公主", "清亮又明朗"), ("嗡嗡小蜜蜂", "欢快又敏捷"), ("坏脾气女巫", "沙哑又狡猾"),
        ("老母鸡奶奶", "慢悠悠，暖洋洋"), ("爱生气的巨魔", "粗声粗气"), ("友好的大巨人", "又大又慢又低"),
        ("熊爷爷", "低低的，慢慢的"), ("狡猾的小狐狸", "又快又悄悄"), ("勇敢的骑士", "坚定又清晰"),
        ("淘气小猪", "又小又快"), ("快乐小鸡", "欢快又清脆"), ("隆隆大恐龙", "低沉又响亮"),
        ("毛茸茸的小羊", "软软的，咩咩叫"), ("爱玩的小狗", "欢快又活泼"),
        ("好奇的小猫", "又小又轻柔"), ("小鸭子", "又小又脆"),
        ("熊宝宝", "又小又软"), ("大棕熊", "低沉又缓慢"),
        ("饥饿的大灰狼", "低低的，沉沉的"), ("毛茸茸的绵羊妈妈", "柔软又缓慢"),
        ("小粉猪", "又小又轻"), ("卷毛小猪", "活泼又明亮"), ("蓬松小羔羊", "轻快又欢跳"), ("戏水小鸭", "又小又清脆"), ("摇尾巴小狗", "清脆又温暖"), ("甜甜小猫咪", "又小又甜"),
    ];

    private static (string Name, string Hint)[] RussianWords() =>
    [
        ("Бабушка-сказочница", "тёплая и ласковая"), ("Дедушка-сказочник", "низкий и уютный"), ("Шёпот ночи", "тихий и медленный"),
        ("Добрая мама", "тёплая и добрая"), ("Мышка-пискушка", "крошечная и тонкая"), ("Зайка-попрыгайка", "мягкая и прыгучая"),
        ("Храбрая принцесса", "ясная и звонкая"), ("Пчёлка Жужа", "бойкая и быстрая"), ("Ворчливая ведьма", "хриплая и хитрая"),
        ("Бабушка-наседка", "медленная и уютная"), ("Тролль-ворчун", "грубый и низкий"), ("Добрый великан", "большой, низкий, неспешный"),
        ("Дедушка Медведь", "низкий и добрый"), ("Хитрый лис", "быстрый и шепчущий"), ("Отважный рыцарь", "смелый и чёткий"),
        ("Поросёнок-озорник", "маленький и быстрый"), ("Цыплёнок Пик", "весёлый и звонкий"), ("Грохочущий дракон", "гулкий и громкий"),
        ("Пушистый ягнёнок", "мягкий и блеющий"), ("Игривый щенок", "бойкий и весёлый"),
        ("Любопытный котёнок", "крошечный и мурлыкающий"), ("Утёнок Кря", "маленький и пискливый"),
        ("Медвежонок", "маленький и мягкий"), ("Большой бурый медведь", "низкий и неспешный"),
        ("Голодный волк", "низкий и ворчливый"), ("Пушистая овечка", "мягкая и неспешная"),
        ("Пятачок-крошка", "крошечный и сопящий"), ("Кудрявый поросёнок", "звонкий и бойкий"), ("Мягкий агнёнок", "лёгкий и пушистый"), ("Утёнок Плюх", "звонкий и брызгливый"), ("Щенок-вертихвост", "весёлый и тёплый"), ("Нежный котёнок", "крошечный и нежный"),
    ];

    private static (string Name, string Hint)[] JapaneseWords() =>
    [
        ("おはなしおばあちゃん", "あたたかくて やさしい"), ("おはなしおじいちゃん", "低くて ほっとする"), ("おやすみのささやき", "しずかで ゆっくり"),
        ("やさしいおかあさん", "あたたかくて やさしい"), ("ちびねずみ", "小さくて 高い声"), ("ぴょんぴょんうさぎ", "ふんわり はねる声"),
        ("ゆうかんなおひめさま", "はっきり きらきら"), ("ぶんぶんみつばち", "はずんで すばやい"), ("ぷんぷんまじょ", "かすれて ずるがしこい"),
        ("おばあちゃんどり", "ゆっくり ぽかぽか"), ("ぶつぶつトロール", "あらくて 低い"), ("やさしいきょじん", "大きくて 低くて ゆっくり"),
        ("くまのおじいさん", "低くて やさしい"), ("ずるいきつね", "すばしこい ささやき"), ("ゆうかんなきし", "力強く はっきり"),
        ("いたずらこぶた", "小さくて すばやい"), ("ぴよぴよひよこ", "げんきな さえずり"), ("ごろごろドラゴン", "ひびく 大きな声"),
        ("ふわふわこひつじ", "やわらかく メェメェ"), ("あそびずきの こいぬ", "げんきで たのしい"),
        ("ふしぎがりの こねこ", "ちいさくて ごろごろ"), ("ちいさな あひるのこ", "ちいさくて ぴよぴよ"),
        ("くまのこ", "ちいさくて やわらかい"), ("おおきな くま", "低くて ゆっくり"),
        ("はらぺこ おおかみ", "低くて うなるよう"), ("ふわふわ ひつじ", "やわらかく ゆっくり"),
        ("ちびこぶた", "小さくて ふんふん"), ("くるくるこぶた", "はずんで あかるい"), ("ふんわりこひつじ", "かるくて ふわふわ"), ("ばしゃばしゃあひる", "はずんで ぴちゃぴちゃ"), ("しっぽふりこいぬ", "げんきで あたたかい"), ("あまえんぼの こねこ", "小さくて あまい"),
    ];

    private static (string Name, string Hint)[] ArabicWords() =>
    [
        ("الجدّة الحكّاءة", "دافئة وحنونة"), ("الجدّ الحكّاء", "عميق ومريح"), ("همسة المساء", "هادئ وبطيء"),
        ("ماما الحنون", "دافئة ولطيفة"), ("الفأرة الصغيرة", "صغيرة وحادّة"), ("الأرنبة القفّازة", "ناعمة وتقفز"),
        ("الأميرة الشجاعة", "واضحة ومشرقة"), ("النحلة الطنّانة", "مرحة وسريعة"), ("الساحرة النكدة", "خشنة وماكرة"),
        ("الدجاجة الجدّة", "بطيئة ودافئة"), ("العفريت العبوس", "خشن وغليظ"), ("العملاق الطيّب", "كبير وعميق وبطيء"),
        ("الجدّ الدبّ", "عميق وطيّب"), ("الثعلب الماكر", "سريع وهامس"), ("الفارس الشجاع", "جريء وواضح"),
        ("الخنزير المشاكس", "صغير وسريع"), ("الكتكوت المرح", "مرح وزقزقة"), ("التنّين الهادر", "مدوٍّ وعالٍ"),
        ("الخروف الصغير الصوفي", "ناعم ومأمأ"), ("الجرو اللعوب", "نشيط ومرح"),
        ("القط الصغير الفضولي", "صغير ويخرخر"), ("فرخ البط الصغير", "صغير ومزقزق"),
        ("شبل الدب", "صغير وناعم"), ("الدب البني الكبير", "عميق وبطيء"),
        ("الذئب الجائع", "منخفض ومزمجر"), ("النعجة الصوفية", "ناعمة وبطيئة"),
        ("الخنزير الصغير الفضولي", "صغير وشمّام"), ("الخنزير المجعّد", "مرح ورنّان"), ("الحمل الرقيق", "خفيف وناعم"), ("بطّوط الماء", "مرح ومتطاير"), ("الجرو الهزّاز", "دافئ ومرح"), ("القطة الحلوة", "صغيرة وحلوة"),
    ];

    private static (string Name, string Hint)[] DanishWords() =>
    [
        ("Bedstemor Fortæller", "varm og blid"), ("Bedstefar Fortæller", "dyb og hyggelig"), ("Godnat-hvisker", "blød og langsom"),
        ("Kære Mor", "varm og venlig"), ("Lille Mus", "lillebitte og høj"), ("Hoppekaninen", "blød og hoppende"),
        ("Modig Prinsesse", "klar og lys"), ("Summebi", "boblende og hurtig"), ("Sur Heks", "hæs og snedig"),
        ("Mormor Høne", "langsom og hyggelig"), ("Gnavne Trold", "grov og dyb"), ("Venlig Kæmpe", "stor, dyb og langsom"),
        ("Bedstefar Bjørn", "dyb og god"), ("Snu Ræv", "hurtig og hviskende"), ("Modig Ridder", "dristig og tydelig"),
        ("Frække Gris", "lille og hurtig"), ("Pip-Kylling", "glad og piblende"), ("Rumlende Drage", "buldrende og kraftig"),
        ("Uldne Lam", "blød og mekrende"), ("Legesyg Hvalp", "livlig og glad"),
        ("Nysgerrig Killing", "lille og spindende"), ("Lille Andunge", "lille og pippende"),
        ("Bjørneunge", "lille og blød"), ("Stor Brun Bjørn", "dyb og langsom"),
        ("Sulten Ulv", "lav og knurrende"), ("Uldent Får", "blød og langsom"),
        ("Snøvlende Pattegris", "lille og snorkende"), ("Krøllet Gris", "livlig og lys"), ("Fnuglet Lam", "let og blødt"), ("Plaskende Andunge", "boblende og pjaskende"), ("Logrende Hvalp", "glad og varm"), ("Sød Killing", "lille og sød"),
    ];

    private static (string Name, string Hint)[] SwedishWords() =>
    [
        ("Farmor Berättare", "varm och mild"), ("Farfar Berättare", "djup och mysig"), ("Godnattviskaren", "mjuk och långsam"),
        ("Snälla Mamma", "varm och vänlig"), ("Lilla Musen", "pytteliten och hög"), ("Hoppakaninen", "mjuk och hoppande"),
        ("Modiga Prinsessan", "klar och ljus"), ("Surrbiet", "sprudlande och snabb"), ("Griniga Häxan", "hes och listig"),
        ("Mormor Höna", "långsam och mysig"), ("Bråkiga Trollet", "grov och djup"), ("Snälla Jätten", "stor, djup och långsam"),
        ("Morfar Björn", "djup och god"), ("Listiga Räven", "snabb och viskande"), ("Tappra Riddaren", "djärv och tydlig"),
        ("Busiga Grisen", "liten och snabb"), ("Pip-Kycklingen", "glad och pipig"), ("Mullrande Draken", "dånande och stark"),
        ("Ulligt Lamm", "mjukt och bräkande"), ("Lekfull Valp", "pigg och glad"),
        ("Nyfiken Kattunge", "liten och spinnande"), ("Liten Ankunge", "liten och pipig"),
        ("Björnunge", "liten och mjuk"), ("Stor Brun Björn", "djup och långsam"),
        ("Hungrig Varg", "låg och morrande"), ("Ullig Tacka", "mjuk och långsam"),
        ("Snuffande Smågris", "liten och snörvlande"), ("Krulliga Grisen", "pigg och ljus"), ("Fluffigt Lamm", "lätt och mjukt"), ("Plaskande Ankunge", "bubblande och plaskig"), ("Viftande Valp", "glad och varm"), ("Söt Kattunge", "liten och söt"),
    ];

    private static (string Name, string Hint)[] NorwegianWords() =>
    [
        ("Bestemor Forteller", "varm og myk"), ("Bestefar Forteller", "dyp og lun"), ("Nattahviskeren", "myk og langsom"),
        ("Snille Mamma", "varm og vennlig"), ("Lille Mus", "bitteliten og høy"), ("Hoppekaninen", "myk og hoppende"),
        ("Modig Prinsesse", "klar og lys"), ("Summebien", "boblende og rask"), ("Sur Heks", "hes og lur"),
        ("Bestemor Høne", "langsom og lun"), ("Grinete Troll", "grov og dyp"), ("Snill Kjempe", "stor, dyp og langsom"),
        ("Bestefar Bjørn", "dyp og god"), ("Lur Rev", "rask og hviskende"), ("Modig Ridder", "dristig og tydelig"),
        ("Frekk Gris", "liten og rask"), ("Pip-Kylling", "glad og kvitrende"), ("Buldrende Drage", "drønnende og sterk"),
        ("Ullent Lam", "mykt og breklende"), ("Leken Valp", "kvikk og glad"),
        ("Nysgjerrig Kattunge", "liten og spinnende"), ("Liten Andunge", "liten og pipende"),
        ("Bjørneunge", "liten og myk"), ("Stor Brun Bjørn", "dyp og langsom"),
        ("Sulten Ulv", "lav og knurrende"), ("Ullent Får", "mykt og langsomt"),
        ("Snufsende Smågris", "liten og snorkende"), ("Krøllete Gris", "livlig og lys"), ("Lodnet Lam", "lett og mykt"), ("Plaskende Andunge", "boblende og plaskete"), ("Logrende Valp", "glad og varm"), ("Søt Kattunge", "liten og søt"),
    ];

    private static (string Name, string Hint)[] FinnishWords() =>
    [
        ("Satumummo", "lämmin ja lempeä"), ("Satuvaari", "syvä ja tunnelmallinen"), ("Hyvänyön kuiskaaja", "pehmeä ja hidas"),
        ("Hellä äiti", "lämmin ja ystävällinen"), ("Pikku hiiri", "pienenpieni ja korkea"), ("Hyppivä kaniini", "pehmeä ja ponnahteleva"),
        ("Rohkea prinsessa", "kirkas ja iloinen"), ("Surisevä mehiläinen", "kuplivan nopea"), ("Äkäinen noita", "käheä ja ovela"),
        ("Mummokana", "hidas ja lämmin"), ("Nurja peikko", "karhea ja matala"), ("Ystävällinen jättiläinen", "iso, matala ja hidas"),
        ("Karhuukki", "matala ja hyväntahtoinen"), ("Ovela kettu", "nopea ja kuiskaava"), ("Urhea ritari", "rohkea ja selkeä"),
        ("Veikeä possu", "pieni ja nopea"), ("Piipittävä tipu", "iloinen ja piipittävä"), ("Jyrisevä lohikäärme", "jylisevä ja voimakas"),
        ("Villainen Karitsa", "pehmeä ja määkivä"), ("Leikkisä Pentu", "vilkas ja iloinen"),
        ("Utelias Kissanpentu", "pieni ja kehräävä"), ("Pieni Ankanpoikanen", "pieni ja piipittävä"),
        ("Karhunpentu", "pieni ja pehmeä"), ("Iso Ruskea Karhu", "syvä ja hidas"),
        ("Nälkäinen Susi", "matala ja murisevä"), ("Villainen Lammas", "pehmeä ja hidas"),
        ("Tuhiseva Possunpoikanen", "pieni ja tuhiseva"), ("Kihara Possu", "vilkas ja kirkas"), ("Pehmoinen Karitsa", "kevyt ja pehmeä"), ("Molskahtava Ankanpoikanen", "kuplivan loiskiva"), ("Häntää Heiluttava Pentu", "iloinen ja lämmin"), ("Suloinen Kissanpentu", "pieni ja suloinen"),
    ];

    private static (string Name, string Hint)[] DutchWords() =>
    [
        ("Vertel-oma", "warm en zacht"), ("Vertel-opa", "diep en gezellig"), ("Welterusten-fluisteraar", "zacht en langzaam"),
        ("Lieve Mama", "warm en vriendelijk"), ("Piepklein Muisje", "heel klein en hoog"), ("Huppelkonijntje", "zacht en springerig"),
        ("Dappere Prinses", "helder en stralend"), ("Zoemend Bijtje", "bruisend en snel"), ("Chagrijnige Heks", "schor en slim"),
        ("Oma Kip", "langzaam en gezellig"), ("Mopperende Trol", "ruw en diep"), ("Vriendelijke Reus", "groot, diep en langzaam"),
        ("Opa Beer", "diep en lief"), ("Slimme Vos", "snel en fluisterend"), ("Dappere Ridder", "stoer en duidelijk"),
        ("Ondeugend Biggetje", "klein en snel"), ("Piepend Kuikentje", "vrolijk en tjilpend"), ("Rommelende Draak", "dreunend en luid"),
        ("Wollig Lammetje", "zacht en blatend"), ("Speels Hondje", "levendig en blij"),
        ("Nieuwsgierig Katje", "klein en spinnend"), ("Klein Eendje", "klein en piepend"),
        ("Berenwelpje", "klein en zacht"), ("Grote Bruine Beer", "diep en langzaam"),
        ("Hongerige Wolf", "laag en grommend"), ("Wollig Schaap", "zacht en langzaam"),
        ("Snuffelend Biggetje", "klein en snuivend"), ("Krullend Biggetje", "levendig en helder"), ("Pluizig Lammetje", "licht en zacht"), ("Plassend Eendje", "bruisend en spetterend"), ("Kwispelend Hondje", "blij en warm"), ("Lief Katje", "klein en lief"),
    ];

    private static (string Name, string Hint)[] GermanWords() =>
    [
        ("Märchenoma", "warm und weich"), ("Märchenopa", "tief und gemütlich"), ("Gutenacht-Flüsterer", "sanft und langsam"),
        ("Liebe Mama", "warm und freundlich"), ("Winziges Mäuschen", "ganz klein und hoch"), ("Hüpfhäschen", "weich und hüpfend"),
        ("Mutige Prinzessin", "klar und hell"), ("Summende Biene", "sprudelnd und schnell"), ("Mürrische Hexe", "heiser und schlau"),
        ("Oma Henne", "langsam und gemütlich"), ("Brummiger Troll", "rau und tief"), ("Freundlicher Riese", "groß, tief und langsam"),
        ("Opa Bär", "tief und lieb"), ("Schlauer Fuchs", "schnell und flüsternd"), ("Tapferer Ritter", "mutig und deutlich"),
        ("Freches Ferkel", "klein und schnell"), ("Piepsendes Küken", "fröhlich und zwitschernd"), ("Rumpelnder Drache", "dröhnend und laut"),
        ("Wolliges Lämmchen", "weich und meckernd"), ("Verspielter Welpe", "munter und froh"),
        ("Neugieriges Kätzchen", "klein und schnurrend"), ("Kleines Entchen", "klein und piepsend"),
        ("Bärenkind", "klein und weich"), ("Großer Brauner Bär", "tief und langsam"),
        ("Hungriger Wolf", "tief und knurrend"), ("Wolliges Schaf", "weich und langsam"),
        ("Schnüffelndes Ferkel", "klein und schnaufend"), ("Ringelschwänzchen", "munter und hell"), ("Flauschiges Lamm", "leicht und weich"), ("Platschendes Entchen", "sprudelnd und spritzig"), ("Schwanzwedelnder Welpe", "froh und warm"), ("Liebes Kätzchen", "klein und lieb"),
    ];
}
