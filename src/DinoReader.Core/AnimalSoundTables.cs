using ReadTheStupidText.Application.Reading;

namespace DinoReader.Core;

/// <summary>
/// The animal and sound words of every language (Slice 117), one table each in the format <see cref="AnimalSoundWords"/> reads:
/// <c>Animal|names|Sound|other spellings</c>. The words are machine-drafted, like the rest of Dino's strings, and wait for a native-speaker read.
/// </summary>
internal static class AnimalSoundTables
{
    public static string TableOf(AppLanguage language) => language switch
    {
        AppLanguage.French => French,
        AppLanguage.Spanish => Spanish,
        AppLanguage.Portuguese => Portuguese,
        AppLanguage.Russian => Russian,
        AppLanguage.Persian => Persian,
        AppLanguage.Arabic => Arabic,
        AppLanguage.Chinese => Chinese,
        AppLanguage.Japanese => Japanese,
        AppLanguage.Danish => Danish,
        AppLanguage.Finnish => Finnish,
        AppLanguage.Swedish => Swedish,
        AppLanguage.Norwegian => Norwegian,
        AppLanguage.Dutch => Dutch,
        _ => English,
    };

    private const string English = """
        Owl|owl, owlet|Hoo hoo!|hoot, hoo
        Rooster|rooster, cockerel|Cock-a-doodle-doo!|cockadoodledoo
        Goat|goat|Meeeh!|maa
        Sheep|sheep, lamb, lambkin|Baa!|baa
        Cow|cow, calf, bull, ox|Moo!|moo
        Cat|cat, kitten, puss, tomcat|Meow!|miaow, mew, meow
        Dog|dog, puppy, pup, hound|Woof!|arf
        Duck|duck, duckling|Quack!|quack
        Pig|pig, piglet, hog|Oink!|oink
        Hen|hen, chicken, chick|Cluck!|cluck, bawk
        Horse|horse, pony, foal, stallion|Neigh!|whinny, neigh
        Frog|frog, toad, froglet|Ribbit!|ribbit, croak
        Mouse|mouse, mice|Squeak!|squeak, eek
        Lion|lion, lioness|Roar!|roar, grr
        Bee|bee, bumblebee|Buzz buzz!|buzz, bzz
        Wolf|wolf|Aooo!|howl, awoo, ahoo
        Bird|bird, birdie|Tweet tweet!|tweet, chirp, cheep
        Rabbit|rabbit, bunny|
        Bear|bear|Grr!|growl, grr
        """;

    private const string French = """
        Owl|hibou, chouette|Hou hou !|hou
        Rooster|coq|Cocorico !|cocorico
        Goat|chèvre, chevreau, bouc|Mêê !|mê, mé, mèè
        Sheep|mouton, agneau, brebis, bélier|Bêê !|bê, bé, bèè
        Cow|vache, veau, taureau, boeuf, bœuf|Meuh !|meuh
        Cat|chat, chatte, chaton, minou|Miaou !|miaou, miaule
        Dog|chien, chienne, chiot, toutou|Ouaf !|ouaf, wouf
        Duck|canard, canne, caneton|Couac couac !|couac
        Pig|cochon, cochonnet, porcelet, porc, goret|Groin groin !|groin, grouik
        Hen|poule, poulet, poussin|Cot cot !|cot, cotcotcodet
        Horse|cheval, poney, poulain, jument, étalon|Hiii !|hennit, hihi
        Frog|grenouille, crapaud, têtard|Coa coa !|coa, croa
        Mouse|souris|Couic !|couic
        Lion|lion, lionne, lionceau|Grrr !|grr, rugit
        Bee|abeille, bourdon|Bzzz !|bzz, bzzz
        Wolf|loup, louve, louveteau|Aouuu !|aou, ahou
        Bird|oiseau, oisillon, moineau, pinson|Cui cui !|cui, piou, cuicui
        Rabbit|lapin, lapine, lapereau|
        Bear|ours, ourse, ourson|Grrr !|grr
        """;

    private const string Spanish = """
        Owl|búho, lechuza, mochuelo|¡Uhu uhu!|uhu
        Rooster|gallo|¡Quiquiriquí!|quiquiriqui, kikiriki
        Goat|cabra, cabrito, chivo|¡Meee!|mee
        Sheep|oveja, ovejita, cordero, corderito, borrego, carnero|¡Beee!|bee
        Cow|vaca, ternero, becerro, toro|¡Muuu!|muu
        Cat|gato, gata, gatito, gatita, minino|¡Miau!|miau, miauu
        Dog|perro, perra, perrito, cachorro|¡Guau!|guau, guauu
        Duck|pato, pata, patito|¡Cuac!|cuac
        Pig|cerdo, cerdito, cochinito, puerco, lechón|¡Oink!|oink, gruñe
        Hen|gallina, pollo, pollito|¡Coc coc!|coc, cloc, cococo
        Horse|caballo, caballito, potro, yegua, poni|¡Iiiih!|relincha
        Frog|rana, ranita, sapo|¡Croac!|croac, cruac
        Mouse|ratón, ratoncito, rata|¡Iiik!|squic
        Lion|león, leoncito|¡Grrr!|grr, ruge
        Bee|abeja, abejorro|¡Zzzum!|zum, bzz, zzz
        Wolf|lobo, loba, lobito|¡Auuu!|auu
        Bird|pájaro, pajarito, gorrión, petirrojo|¡Pío pío!|pio, pío, pip
        Rabbit|conejo, coneja, conejito, conejita|
        Bear|oso, osa, osito, osezno|¡Grr!|grr, gruñe
        """;

    private const string Portuguese = """
        Owl|coruja, mocho, bufo|Hu hu!|hu, uh
        Rooster|galo|Cocoricó!|cocorico
        Goat|cabra, cabrito, bode|Méé!|meh
        Sheep|ovelha, ovelhinha, cordeiro, carneiro, borrego|Béé!|bé
        Cow|vaca, bezerro, touro, boi|Muuu!|mu, muu
        Cat|gato, gata, gatinho, gatinha|Miau!|miau, miado
        Dog|cachorro, cachorrinho, cão, cadela|Au au!|au, aú, ão
        Duck|pato, pata, patinho, patinha|Quá quá!|qua, quá
        Pig|porco, porquinho, leitão, porca|Oinc oinc!|oinc
        Hen|galinha, frango, pintinho|Có có có!|có
        Horse|cavalo, cavalinho, égua, potro, pônei|Ihhh!|ih, ihh, relincha
        Frog|sapo, sapinho, rã, perereca|Croac!|croac, coaxa
        Mouse|rato, ratinho, camundongo, ratazana|Quic quic!|quic, ziu
        Lion|leão, leoa, leãozinho|Grrr!|grr, ruge
        Bee|abelha, abelhinha, zangão|Zum zum!|zum, bzz
        Wolf|lobo, loba, lobinho|Auuu!|au, auu
        Bird|pássaro, passarinho, pardal|Piu piu!|piu, pip
        Rabbit|coelho, coelha, coelhinho, coelhinha|
        Bear|urso, ursa, ursinho, ursinha|Grr!|grr, rosna
        """;

    private const string Russian = """
        Owl|сова, совушка, филин|У-гу!|угу, ух
        Rooster|петух, петушок|Ку-ка-ре-ку!|кукареку
        Goat|коза, козочка, козёл, козел, козлёнок, козленок, козлик|Ме-е!|ме, мее
        Sheep|овца, овечка, баран, барашек, ягнёнок, ягненок|Бе-е!|бе, бее
        Cow|корова, коровка, телёнок, теленок, бык, бычок, тёлка|Му-у!|му, муу
        Cat|кот, кошка, котёнок, котенок, котик, киса, мурка|Мяу!|мяу, мур
        Dog|собака, собачка, пёс, пес, щенок, пёсик|Гав!|гав, гаф
        Duck|утка, утёнок, утенок, уточка, селезень|Кря-кря!|кря
        Pig|свинья, свинка, поросёнок, поросенок, хрюша, кабан|Хрю-хрю!|хрю
        Hen|курица, курочка, цыплёнок, цыпленок, наседка|Ко-ко-ко!|ко, кудах
        Horse|лошадь, лошадка, конь, жеребёнок, жеребенок, пони, кобыла, жеребец|И-го-го!|иго, иии
        Frog|лягушка, лягушонок, жаба, квакушка|Ква-ква!|ква
        Mouse|мышь, мышка, мышонок|Пи-пи!|пи, пищит
        Lion|лев, львица, львёнок, львенок|Рррр!|рр, ррр, рычит
        Bee|пчела, пчёлка, шмель|Ж-ж-ж!|жж, жжж
        Wolf|волк, волчица, волчонок|У-у-у!|уу, ууу, воет
        Bird|птица, птичка, воробей, воробушек, снегирь|Чик-чирик!|чирик
        Rabbit|кролик, крольчиха, крольчонок, зайчик, заяц|
        Bear|медведь, медведица, медвежонок, мишка|Рр!|рр, ррр, рычит
        """;

    private const string Persian = """
        Owl|جغد|هوهو!|
        Rooster|خروس|قوقولی قوقو!|قوقولیقوقو, قوقولی
        Goat|بز, بزغاله|مع مع!|مع, مئ
        Sheep|گوسفند, بره, میش, قوچ|بع بع!|بع, بعبع
        Cow|گاو, گوساله|ماا!|مااا
        Cat|گربه, بچه گربه, گربهٔ|میو!|میو, میومیو
        Dog|سگ, توله, توله سگ|هاپ هاپ!|هاپ, واق
        Duck|اردک, جوجه اردک|قات قات!|قات, قاقا
        Pig|خوک, بچه خوک|خرخر!|
        Hen|مرغ, جوجه|قدقد!|
        Horse|اسب, کره اسب, کره‌اسب, مادیان|ویهه!|
        Frog|قورباغه|قورقور!|قورقور, ورور
        Mouse|موش|جیک!|جیک, جیرجیر
        Lion|شیر|غرررر!|غرر, غرش
        Bee|زنبور|ویز ویز!|ویز, وزوز
        Wolf|گرگ|اووو!|اوو, عوعو
        Bird|پرنده, گنجشک, پرستو|جیک جیک!|جیک, چهچه
        Rabbit|خرگوش|
        Bear|خرس|غرغر!|غر, غرش
        """;

    private const string Arabic = """
        Owl|بومة, بوم|هوهو!|
        Rooster|ديك|كوكوكو!|كوكوكو, قوقو
        Goat|ماعز, تيس|ميع!|ميع, ماع
        Sheep|خروف, نعجة, كبش|ماع!|ماع, باع
        Cow|بقرة, عجل, عجلة, ثور|موو!|
        Cat|قطة, قط, هرة, هر|ميااو!|مياو, مواء, ميو
        Dog|كلب, كلبة, جرو|هاو هاو!|هاو, هوهو
        Duck|بطة, بط|كواك!|كواك, بطبط
        Pig|خنزير, خنوص|غرغر!|غرغر, نخر
        Hen|دجاجة, دجاج, كتكوت, فرخ|كوك كوك!|كوك, قوقة
        Horse|حصان, فرس|إيهيهي!|ايهيهي, صهيل
        Frog|ضفدع, ضفدعة|نق نق!|نق, نقنق
        Mouse|فأر, فأرة, فار|صي صي!|صي, صيصي
        Lion|أسد, لبؤة, شبل|غررر!|غرر, زئير
        Bee|نحلة, نحل, دبور|زززز!|زز, طنين
        Wolf|ذئب, ذئبة|أوووو!|اوو, عواء
        Bird|طائر, عصفور, عصفورة, طير, بلبل|زق زق!|زق, زقزقة
        Rabbit|أرنب, أرنبة|
        Bear|دب, دبة, دبدوب|غررر!|غرر
        """;

    private const string Chinese = """
        Owl|猫头鹰|咕咕!|咕咕
        Rooster|公鸡, 大公鸡|喔喔喔!|喔喔, 咯咯哒
        Goat|山羊|咩咩!|咩
        Sheep|羊, 小羊, 绵羊, 羊羊|咩!|咩
        Cow|牛, 小牛, 奶牛, 公牛, 牛牛|哞!|哞
        Cat|猫, 小猫, 猫咪, 咪咪|喵!|喵
        Dog|狗, 小狗, 狗狗|汪汪!|汪
        Duck|鸭, 小鸭, 鸭子, 鸭鸭|嘎嘎!|嘎
        Pig|猪, 小猪, 猪猪|哼哼!|哼
        Hen|母鸡, 小鸡, 鸡|咯咯!|咯咯, 咯哒
        Horse|马, 小马, 马儿|嘶!|嘶, 咴咴
        Frog|青蛙, 蛙, 蛤蟆, 蟾蜍|呱呱!|呱
        Mouse|老鼠, 小老鼠, 鼠|吱吱!|吱
        Lion|狮子, 狮|吼!|吼, 嗷
        Bee|蜜蜂, 小蜜蜂, 蜂|嗡嗡!|嗡
        Wolf|大灰狼, 灰狼, 狼|嗷呜!|嗷呜, 嗷
        Bird|小鸟, 鸟儿, 鸟, 麻雀, 燕子|叽叽喳喳!|叽叽, 喳喳, 啾啾
        Rabbit|兔子, 小兔, 兔|
        Bear|熊, 小熊, 大熊|呜!|呜呜, 吼
        """;

    private const string Japanese = """
        Owl|ふくろう, フクロウ, 梟|ホーホー!|ホーホー
        Rooster|おんどり, 雄鶏|コケコッコー!|コケコッコ
        Goat|やぎ, ヤギ, 山羊|メェー!|メェ, めえ
        Sheep|ひつじ, ヒツジ, 羊, 子羊|メェー!|メェ, めえ
        Cow|牛, うし, ウシ, 子牛, 乳牛|モー!|モー, もー
        Cat|猫, ねこ, ネコ, こねこ, 子猫, ニャンコ|ニャー!|ニャ, にゃ
        Dog|犬, いぬ, イヌ, 子犬, こいぬ, わんこ|ワン!|ワン, わん
        Duck|あひる, アヒル, 鴨, カモ|ガーガー!|ガー
        Pig|豚, ぶた, ブタ, 子豚, こぶた|ブーブー!|ブー, ぶう
        Hen|にわとり, ニワトリ, ひよこ, ヒヨコ, めんどり, 鶏|コッコッコ!|コッコ, こっこ
        Horse|馬, うま, ウマ, 子馬, こうま, ポニー|ヒヒーン!|ヒヒン, ひひん
        Frog|かえる, カエル, 蛙|ゲロゲロ!|ゲロ, げろ
        Mouse|ねずみ, ネズミ, 鼠|チューチュー!|チュー, ちゅう
        Lion|ライオン, らいおん|ガオー!|ガオ, がお
        Bee|みつばち, ミツバチ, 蜂|ブンブン!|ブン, ぶん
        Wolf|おおかみ, オオカミ, 狼|アオーン!|アオ, あお
        Bird|ことり, 小鳥, すずめ, スズメ, 雀, とり, トリ, 鳥|ピヨピヨ!|ピヨ, チュン, ぴよ
        Rabbit|うさぎ, ウサギ, 兎|
        Bear|くま, クマ, 熊|ウォー!|ウォ, ガオ
        """;

    private const string Danish = """
        Owl|ugle, ugleunge|Uhu uhu!|uhu, hu
        Rooster|hane|Kykeliky!|kykeliky
        Goat|ged, gedekid, gedebuk|Mæh!|mæ, mæh
        Sheep|får, lam, vædder|Mæh!|mæ, mæh, bæ
        Cow|ko, kalv, tyr, okse|Muh!|mu, muh
        Cat|kat, killing, kattekilling, misser|Miav!|mjau
        Dog|hund, hvalp, vovse|Vov!|vov, vuf
        Duck|and, ælling, andemor|Rap rap!|rap, kvak
        Pig|gris, grisling, pattegris, svin|Øf øf!|øf, oink
        Hen|høne, kylling, hønsemor|Kluk kluk!|
        Horse|hest, pony, føl, hoppe, hingst|Vrinsk!|vrinsk, vrinsker
        Frog|frø, tudse, haletudse|Kvak!|kvak
        Mouse|mus|Pip!|pip
        Lion|løve, løvinde, løveunge|Brøl!|brøl, grr
        Bee|bi, bie, humlebi|Sum sum!|bzz
        Wolf|ulv, ulvehvalp|Uuuuh!|uh, uuh, uuu
        Bird|fugl, spurv, rødhals|Pip pip!|kvit
        Rabbit|kanin, kaninunge, hare|
        Bear|bjørn, bjørnunge, bamse|Grr!|grr, brum
        """;

    private const string Finnish = """
        Owl|pöllö, pöllönen, huuhkaja|Huu huu!|huu
        Rooster|kukko|Kukkokiekuu!|kukkokiekuu, kukkoo
        Goat|vuohi, kili, pukki|Mää!|
        Sheep|lammas, karitsa, oinas|Mää!|
        Cow|lehmä, vasikka, sonni, härkä|Ammuu!|
        Cat|kissa, kisu, kisuli, kissanpentu|Miau!|miau, mau
        Dog|koira, koiranpentu, koiruli|Hau!|hau, hauh
        Duck|ankka, ankanpoikanen|Kvaak!|kvaak, kvak
        Pig|sika, porsas, possu|Röh!|röh, röhrö, nyyh
        Hen|kana, kananpoika, tipu|Kot kot!|kotkot
        Horse|hevonen, poni, varsa, hepo, tamma|Hirnu!|hirnuu
        Frog|sammakko, rupikonna, konna|Kvaak kvaak!|kvaak, kurr
        Mouse|hiiri, hiirulainen|Piip!|piip, pii
        Lion|leijona, leijonanpentu|Ärrr!|ärr, örr, karjuu
        Bee|mehiläinen, kimalainen|Pörrr!|pörr, bzz, sirr
        Wolf|susi, sudenpentu|Auuu!|au, auu, uuu
        Bird|lintu, linnunpoika, varpunen, lintunen|Piip piip!|piip, tsirp, viserrys
        Rabbit|kani, pupu, jänis|
        Bear|karhu, karhunpentu, nalle|Grr!|grr, örr
        """;

    private const string Swedish = """
        Owl|uggla, ugglan|Tu-hu!|tuhu, hoo
        Rooster|tupp, tuppen, hane|Kuckeliku!|kuckeliku
        Goat|get, killing, bock|Mäh!|mäh, mä
        Sheep|får, lamm, bagge|Bää!|bää, bä
        Cow|ko, kalv, tjur, oxe|Mu!|mu, muu
        Cat|katt, kisse, kattunge|Mjau!|
        Dog|hund, valp, vovve|Voff!|voff, vov
        Duck|anka, ankunge, andunge|Kvack!|kvack
        Pig|gris, spädgris, svin|Nöff!|nöff, grymt
        Hen|höna, kyckling|Kackel!|
        Horse|häst, ponny, föl, märr, hingst|Gnägg!|gnägg, gnäggar
        Frog|groda, padda, grodyngel|Kvack!|kvack, kvaack
        Mouse|mus, musunge|Pip!|pip
        Lion|lejon, lejoninna, lejonunge|Rooar!|rooar, rrr, rytande
        Bee|bi, humla|Surr!|surr, bzz
        Wolf|varg, vargunge|Ooouuu!|ou, oou, ooouu
        Bird|fågel, fågelunge, sparv|Pip pip!|pip, kvitter, tvit
        Rabbit|kanin, kaninunge, hare|
        Bear|björn, björnunge, nalle|Grr!|grr, brum
        """;

    private const string Norwegian = """
        Owl|ugle, ugleunge|Uhu uhu!|uhu, hu
        Rooster|hane|Kykeliky!|kykeliky
        Goat|geit, kje, geitebukk, bukk|Mæ!|mæ, mæh
        Sheep|sau, lam, vær|Bæ!|bæ, bæh
        Cow|ku, kalv, okse, tyr|Mø!|mø, muh
        Cat|katt, kattunge, pus, kisse|Mjau!|mjau, mjauu
        Dog|hund, valp, vov|Voff!|voff, vov
        Duck|and, andunge|Kvakk!|kvakk, kvakk
        Pig|gris, purke, svin, grisunge|Nøff!|nøff, nøf
        Hen|høne, kylling, hønemor|Kakkel kakkel!|kakkel, kluk
        Horse|hest, ponni, føll, hoppe, hingst|Vrinsk!|vrinsk
        Frog|frosk, padde|Kvekk!|kvekk, kvakk
        Mouse|mus, musunge|Pip!|pip
        Lion|løve, løvinne, løveunge|Brøl!|brøl, grr
        Bee|bie, humle|Summ!|summ, bzz
        Wolf|ulv, ulvevalp|Uuuuu!|uuu, uuuh
        Bird|fugl, fugleunge, spurv|Pip pip!|pip, kvitre, kvitter
        Rabbit|kanin, kaninunge, hare|
        Bear|bjørn, bjørnunge, bamse|Grr!|grr, brum
        """;

    private const string Dutch = """
        Owl|uil, uiltje, steenuil|Oehoe!|oehoe, hoe
        Rooster|haan, haantje|Kukeleku!|kukeleku
        Goat|geit, geitje, bok, bokje|Mèè!|meh
        Sheep|schaap, schaapje, lam, lammetje|Mê!|bè
        Cow|koe, koetje, kalf, stier, os|Boe!|
        Cat|kat, katje, poes, poesje, kater, kitten|Miauw!|miauw, mauw, miau
        Dog|hond, hondje, pup, puppy|Woef!|woef, waf
        Duck|eend, eendje|Kwak!|kwak
        Pig|varken, varkentje, biggetje, zwijn|Knor!|knor, oink
        Hen|kip, kippetje, kuiken, hen|Tok tok!|
        Horse|paard, paardje, pony, veulen, merrie, hengst|Hinnik!|
        Frog|kikker, kikkertje, pad, kikvors|Kwaak!|kwaak, kwak
        Mouse|muis, muisje|Piep!|piep
        Lion|leeuw, leeuwin, leeuwtje|Grrr!|grr, brul
        Bee|bij, bijtje, hommel|Zoem!|zoem, bzz
        Wolf|wolf, wolfje|Auuu!|auu
        Bird|vogel, vogeltje, mus, mees, roodborstje|Tjilp!|tsjilp
        Rabbit|konijn, konijntje, haas|
        Bear|beer, berenjong, beertje|Grr!|grr, brom
        """;
}
