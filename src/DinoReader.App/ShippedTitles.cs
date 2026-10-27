using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// The titles of the books Dino ships (starters and every store story) in all thirteen other languages (Slice 159, Decision 164), keyed by the
/// English title. A book's text tables carry only its sentences, and a copy installed earlier would not gain a new table entry, so the shelf, the
/// reader and the store builder all read titles from here. Machine-drafted like the screens' strings; they wait for a native-speaker read.
/// Plain .NET (no MAUI), so the store builder in the tests links this file.
/// </summary>
public static class ShippedTitles
{
	private static readonly Dictionary<string, Dictionary<string, string>> Table = new(StringComparer.Ordinal)
	{
		["The Three Little Pigs"] = new(StringComparer.Ordinal)
		{
			["ar"] = "الخنازير الثلاثة الصغيرة",
			["da"] = "De tre små grise",
			["es"] = "Los tres cerditos",
			["fa"] = "سه بچه خوک کوچولو",
			["fi"] = "Kolme pientä porsasta",
			["fr"] = "Les Trois Petits Cochons",
			["ja"] = "三びきのこぶた",
			["nb"] = "De tre små grisene",
			["nl"] = "De drie biggetjes",
			["pt"] = "Os Três Porquinhos",
			["ru"] = "Три поросёнка",
			["sv"] = "De tre små grisarna",
			["zh"] = "三只小猪",
		},
		["The Gingerbread Man"] = new(StringComparer.Ordinal)
		{
			["ar"] = "رجل الزنجبيل",
			["da"] = "Pebernøddemanden",
			["es"] = "El hombre de jengibre",
			["fa"] = "آدمک نان‌زنجبیلی",
			["fi"] = "Piparkakkumies",
			["fr"] = "Le Bonhomme de pain d'épices",
			["ja"] = "ジンジャーブレッドマン",
			["nb"] = "Pepperkakemannen",
			["nl"] = "De Peperkoekman",
			["pt"] = "O Homem-Biscoito",
			["ru"] = "Пряничный человечек",
			["sv"] = "Pepparkaksgubben",
			["zh"] = "姜饼人",
		},
		["The Tortoise and the Hare"] = new(StringComparer.Ordinal)
		{
			["ar"] = "السلحفاة والأرنب",
			["da"] = "Skildpadden og Haren",
			["es"] = "La tortuga y la liebre",
			["fa"] = "لاک‌پشت و خرگوش",
			["fi"] = "Kilpikonna ja jänis",
			["fr"] = "La Tortue et le Lièvre",
			["ja"] = "うさぎとかめ",
			["nb"] = "Skilpadden og haren",
			["nl"] = "De schildpad en de haas",
			["pt"] = "A Tartaruga e a Lebre",
			["ru"] = "Черепаха и заяц",
			["sv"] = "Sköldpaddan och haren",
			["zh"] = "龟兔赛跑",
		},
		["Good Enough"] = new(StringComparer.Ordinal)
		{
			["ar"] = "جيد بما يكفي",
			["da"] = "Godt nok",
			["es"] = "Lo bastante bueno",
			["fa"] = "به اندازه‌ی کافی خوب",
			["fi"] = "Riittävän hyvä",
			["fr"] = "Assez bien",
			["ja"] = "じゅうぶんいい",
			["nb"] = "Bra nok",
			["nl"] = "Goed genoeg",
			["pt"] = "Bom o bastante",
			["ru"] = "Достаточно хорошо",
			["sv"] = "Bra nog",
			["zh"] = "已经足够好",
		},
		["The Lion and the Mouse"] = new(StringComparer.Ordinal)
		{
			["ar"] = "الأسد والفأر",
			["da"] = "Løven og musen",
			["es"] = "El león y el ratón",
			["fa"] = "شیر و موش",
			["fi"] = "Leijona ja hiiri",
			["fr"] = "Le Lion et la Souris",
			["ja"] = "ライオンとねずみ",
			["nb"] = "Løven og musa",
			["nl"] = "De leeuw en de muis",
			["pt"] = "O Leão e o Ratinho",
			["ru"] = "Лев и мышонок",
			["sv"] = "Lejonet och musen",
			["zh"] = "狮子和小老鼠",
		},
		["Goldilocks and the Three Bears"] = new(StringComparer.Ordinal)
		{
			["ar"] = "ذات الشعر الذهبي والدببة الثلاثة",
			["da"] = "Guldlok og de tre bjørne",
			["es"] = "Ricitos de Oro y los tres osos",
			["fa"] = "مو طلایی و سه خرس",
			["fi"] = "Kultakutri ja kolme karhua",
			["fr"] = "Boucle d'or et les trois ours",
			["ja"] = "ゴルディロックスと三びきのくま",
			["nb"] = "Gullhår og de tre bjørnene",
			["nl"] = "Goudlokje en de drie beren",
			["pt"] = "Cachinhos Dourados e os Três Ursos",
			["ru"] = "Златовласка и три медведя",
			["sv"] = "Guldlock och de tre björnarna",
			["zh"] = "金发姑娘和三只熊",
		},
		["Little Red Riding Hood"] = new(StringComparer.Ordinal)
		{
			["ar"] = "ذات الرداء الأحمر",
			["da"] = "Rødhætte",
			["es"] = "Caperucita Roja",
			["fa"] = "شنل قرمزی",
			["fi"] = "Punahilkka",
			["fr"] = "Le Petit Chaperon rouge",
			["ja"] = "あかずきんちゃん",
			["nb"] = "Rødhette",
			["nl"] = "Roodkapje",
			["pt"] = "Chapeuzinho Vermelho",
			["ru"] = "Красная Шапочка",
			["sv"] = "Rödluvan",
			["zh"] = "小红帽",
		},
		["The Ugly Duckling"] = new(StringComparer.Ordinal)
		{
			["ar"] = "البطة القبيحة",
			["da"] = "Den grimme ælling",
			["es"] = "El patito feo",
			["fa"] = "جوجه اردک زشت",
			["fi"] = "Ruma ankanpoikanen",
			["fr"] = "Le Vilain Petit Canard",
			["ja"] = "みにくいあひるの子",
			["nb"] = "Den stygge andungen",
			["nl"] = "Het lelijke jonge eendje",
			["pt"] = "O Patinho Feio",
			["ru"] = "Гадкий утёнок",
			["sv"] = "Den fula ankungen",
			["zh"] = "丑小鸭",
		},
		["The Little Red Hen"] = new(StringComparer.Ordinal)
		{
			["ar"] = "الدجاجة الحمراء الصغيرة",
			["da"] = "Den lille røde høne",
			["es"] = "La gallinita roja",
			["fa"] = "مرغ سرخ کوچولو",
			["fi"] = "Pieni punainen kana",
			["fr"] = "La Petite Poule rousse",
			["ja"] = "あかいめんどりさん",
			["nb"] = "Den lille røde høna",
			["nl"] = "Het kleine rode hennetje",
			["pt"] = "A Galinha Ruiva",
			["ru"] = "Маленькая красная курочка",
			["sv"] = "Den lilla röda hönan",
			["zh"] = "小红母鸡",
		},
		["The Three Billy Goats Gruff"] = new(StringComparer.Ordinal)
		{
			["ar"] = "الماعز الثلاثة غراف",
			["da"] = "De tre bukkebruse",
			["es"] = "Los tres cabritos Gruff",
			["fa"] = "سه بز کوهی گراف",
			["fi"] = "Kolme Pukkivuohta",
			["fr"] = "Les Trois Boucs bourrus",
			["ja"] = "三びきのやぎのがらがらどん",
			["nb"] = "De tre bukkene Bruse",
			["nl"] = "De drie bokjes Bruse",
			["pt"] = "Os Três Bodes Gruff",
			["ru"] = "Три козлёнка Груфф",
			["sv"] = "De tre bockarna Bruse",
			["zh"] = "三只山羊嘎啦嘎啦",
		},
		["Jack and the Beanstalk"] = new(StringComparer.Ordinal)
		{
			["ar"] = "جاك وساق الفاصولياء",
			["da"] = "Jens og bønnestagen",
			["es"] = "Jack y las habichuelas mágicas",
			["fa"] = "جک و ساقه‌ی لوبیا",
			["fi"] = "Jaakko ja pavunvarsi",
			["fr"] = "Jacques et le haricot magique",
			["ja"] = "ジャックとまめの木",
			["nb"] = "Jack og bønnestengelen",
			["nl"] = "Jaap en de bonenstaak",
			["pt"] = "João e o Pé de Feijão",
			["ru"] = "Джек и бобовый стебель",
			["sv"] = "Jack och bönstjälken",
			["zh"] = "杰克和豆茎",
		},
		["The Town Mouse and the Country Mouse"] = new(StringComparer.Ordinal)
		{
			["ar"] = "فأر المدينة وفأر الريف",
			["da"] = "Bymusen og landmusen",
			["es"] = "El ratón de ciudad y el ratón de campo",
			["fa"] = "موش شهری و موش روستایی",
			["fi"] = "Kaupunkihiiri ja maalaishiiri",
			["fr"] = "Le Rat des villes et le Rat des champs",
			["ja"] = "まちのねずみといなかのねずみ",
			["nb"] = "Bymusa og landmusa",
			["nl"] = "De stadsmuis en de veldmuis",
			["pt"] = "O Rato da Cidade e o Rato do Campo",
			["ru"] = "Городская мышь и деревенская мышь",
			["sv"] = "Stadsmusen och lantmusen",
			["zh"] = "龟兔赛跑",
		},
		["The Elves and the Shoemaker"] = new(StringComparer.Ordinal)
		{
			["ar"] = "الجنيّات وصانع الأحذية",
			["da"] = "Nisserne og skomageren",
			["es"] = "Los duendes y el zapatero",
			["fa"] = "جن‌های کوچولو و کفاش",
			["fi"] = "Tontut ja suutari",
			["fr"] = "Les Lutins et le cordonnier",
			["ja"] = "くつやとこびと",
			["nb"] = "Nissene og skomakeren",
			["nl"] = "De kabouters en de schoenmaker",
			["pt"] = "Os Duendes e o Sapateiro",
			["ru"] = "Эльфы и сапожник",
			["sv"] = "Tomtarna och skomakaren",
			["zh"] = "小精灵和鞋匠",
		},
		["The Tale of Peter Rabbit"] = new(StringComparer.Ordinal)
		{
			["ar"] = "حكاية بيتر الأرنب",
			["da"] = "Fortællingen om Peter Kanin",
			["es"] = "El cuento de Pedro Conejo",
			["fa"] = "قصه‌ی پیتر خرگوشه",
			["fi"] = "Tarina Pekka Kaniinista",
			["fr"] = "L'Histoire de Pierre Lapin",
			["ja"] = "ピーターラビットのおはなし",
			["nb"] = "Fortellingen om Petter Kanin",
			["nl"] = "Het verhaal van Pieter Konijn",
			["pt"] = "A História de Pedro Coelho",
			["ru"] = "Сказка о кролике Питере",
			["sv"] = "Sagan om Pelle Kanin",
			["zh"] = "彼得兔的故事",
		},
		["X Marks the Treasure!"] = new(StringComparer.Ordinal)
		{
			["ar"] = "الكنز عند علامة X!",
			["da"] = "X markerer skatten!",
			["es"] = "¡La X marca el tesoro!",
			["fa"] = "ایکس یعنی گنج!",
			["fi"] = "X merkitsee aarteen paikan!",
			["fr"] = "X marque le trésor !",
			["ja"] = "Xのしるしはおたからだ！",
			["nb"] = "X markerer skatten!",
			["nl"] = "X markeert de schat!",
			["pt"] = "O X marca o tesouro!",
			["ru"] = "Крестик — вот где сокровище!",
			["sv"] = "X markerar skatten!",
			["zh"] = "X 标记宝藏！",
		},
	};

	/// <summary>Every English title that has a table.</summary>
	public static IReadOnlyCollection<string> Titles => Table.Keys;

	/// <summary>The title of the shipped book titled <paramref name="englishTitle"/> in <paramref name="language"/>, or null when it is not one of ours (or the language is English).</summary>
	public static string? For(string englishTitle, string language) =>
		Table.TryGetValue(englishTitle.Trim(), out var titles) && titles.TryGetValue(language, out string? title) ? title : null;

	/// <summary>The shipped title of <paramref name="story"/>: only a book that came from the starters or the store (it has a <see cref="Story.SourceId"/>), never a
	/// parent's own book that merely shares an English title.</summary>
	public static string? ForBook(Story story, string language) =>
		story.SourceId is null ? null : For(story.Title, language);

	/// <summary>All the translated titles of the book titled <paramref name="englishTitle"/> (language code to title), or none.</summary>
	public static IReadOnlyDictionary<string, string> AllFor(string englishTitle) =>
		Table.TryGetValue(englishTitle.Trim(), out var titles) ? titles : new Dictionary<string, string>();

	/// <summary>The title a store row shows in <paramref name="language"/> (Slice 159): the listing's own <c>titles</c>, else the table above (for a listing uploaded
	/// before <c>titles</c> existed), else the listing's title. Never an empty string.</summary>
	public static string ShownInStore(StoreItem item, string language) =>
		item.TitleIn(language) ?? (UiLanguage.Resolve(item.Language) == UiLanguage.English ? For(item.Title, language) : null) ?? item.Title;
}
