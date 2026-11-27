// Slice 437: every word the Sonos browse tree shows a parent, in the 15 app languages. English and Farsi are the mock's words
// (docs/design/sonos/, EN-* and FA-*); the rest are translated from them. Numbers are written with ASCII digits here and turned
// into Eastern Arabic-Persian (fa) or Eastern Arabic (ar) digits when a line is built (`localDigits`).
// A count with a grammatical plural that differs from "N things" has its own one-form (`storiesOne`); the rest read "N <noun>" or
// "<noun>: N", so no language needs a plural rule.

import type { Track } from "./sonos.ts";

const FALLBACK_LANGUAGE = "en";
const PART_SEPARATOR = " · ";
const DIGIT_SETS: Readonly<Record<string, string>> = { fa: "۰۱۲۳۴۵۶۷۸۹", ar: "٠١٢٣٤٥٦٧٨٩" };

interface Labels {
  readonly bedtime: string;
  readonly shortestFirst: string;
  readonly playAll: string;
  readonly newStories: string;
  /** "Ages {a}–{b}" */
  readonly ages: string;
  readonly free: string;
  readonly all: string;
  readonly others: string;
  /** "{n} languages" */
  readonly languages: string;
  /** "{n} stories" */
  readonly stories: string;
  readonly storiesOne?: string;
  /** "{n} min" */
  readonly minutes: string;
  readonly freeTag: string;
  readonly paidTag: string;
}

const LABELS: Readonly<Record<string, Labels>> = {
  en: { bedtime: "Bedtime: 10 minutes or less", shortestFirst: "shortest first", playAll: "press play for all", newStories: "New stories", ages: "Ages {a}–{b}", free: "Free stories", all: "All stories, A–Z", others: "Other languages", languages: "{n} languages", stories: "{n} stories", storiesOne: "1 story", minutes: "{n} min", freeTag: "Free", paidTag: "In the Dino app" },
  fa: { bedtime: "قصه‌های شب: 10 دقیقه یا کمتر", shortestFirst: "کوتاه‌ترها اول", playAll: "پخش همه", newStories: "داستان‌های تازه", ages: "{a} تا {b} سال", free: "داستان‌های رایگان", all: "همه‌ی داستان‌ها، الفبایی", others: "زبان‌های دیگر", languages: "{n} زبان", stories: "{n} داستان", minutes: "{n} دقیقه", freeTag: "رایگان", paidTag: "در برنامه‌ی دینو" },
  ar: { bedtime: "وقت النوم: 10 دقائق أو أقل", shortestFirst: "الأقصر أولاً", playAll: "اضغط تشغيل للكل", newStories: "قصص جديدة", ages: "من {a} إلى {b} سنوات", free: "قصص مجانية", all: "كل القصص، أبجدياً", others: "لغات أخرى", languages: "اللغات: {n}", stories: "القصص: {n}", minutes: "{n} د", freeTag: "مجاني", paidTag: "في تطبيق دينو" },
  da: { bedtime: "Godnat: 10 minutter eller mindre", shortestFirst: "korteste først", playAll: "tryk afspil for alle", newStories: "Nye historier", ages: "{a}–{b} år", free: "Gratis historier", all: "Alle historier, A–Å", others: "Andre sprog", languages: "{n} sprog", stories: "{n} historier", storiesOne: "1 historie", minutes: "{n} min.", freeTag: "Gratis", paidTag: "I Dino-appen" },
  de: { bedtime: "Schlafenszeit: 10 Minuten oder weniger", shortestFirst: "kürzeste zuerst", playAll: "Wiedergabe für alle", newStories: "Neue Geschichten", ages: "{a}–{b} Jahre", free: "Kostenlose Geschichten", all: "Alle Geschichten, A–Z", others: "Andere Sprachen", languages: "{n} Sprachen", stories: "{n} Geschichten", storiesOne: "1 Geschichte", minutes: "{n} Min.", freeTag: "Kostenlos", paidTag: "In der Dino-App" },
  es: { bedtime: "Hora de dormir: 10 minutos o menos", shortestFirst: "las más cortas primero", playAll: "pulsa reproducir para todas", newStories: "Cuentos nuevos", ages: "{a}–{b} años", free: "Cuentos gratis", all: "Todos los cuentos, A–Z", others: "Otros idiomas", languages: "{n} idiomas", stories: "{n} cuentos", storiesOne: "1 cuento", minutes: "{n} min", freeTag: "Gratis", paidTag: "En la app de Dino" },
  fi: { bedtime: "Nukkumaanmeno: enintään 10 minuuttia", shortestFirst: "lyhimmät ensin", playAll: "paina toista kaikille", newStories: "Uudet tarinat", ages: "{a}–{b}-vuotiaille", free: "Ilmaiset tarinat", all: "Kaikki tarinat, A–Ö", others: "Muut kielet", languages: "{n} kieltä", stories: "{n} tarinaa", storiesOne: "1 tarina", minutes: "{n} min", freeTag: "Ilmainen", paidTag: "Dino-sovelluksessa" },
  fr: { bedtime: "Au dodo : 10 minutes ou moins", shortestFirst: "les plus courtes d'abord", playAll: "lecture pour toutes", newStories: "Histoires nouvelles", ages: "{a}–{b} ans", free: "Histoires gratuites", all: "Toutes les histoires, A–Z", others: "Autres langues", languages: "{n} langues", stories: "{n} histoires", storiesOne: "1 histoire", minutes: "{n} min", freeTag: "Gratuit", paidTag: "Dans l'appli Dino" },
  ja: { bedtime: "おやすみ前に：10分以内", shortestFirst: "短い順", playAll: "再生で全部", newStories: "新しいお話", ages: "{a}〜{b}歳", free: "無料のお話", all: "すべてのお話（五十音順）", others: "ほかの言語", languages: "{n}言語", stories: "{n}話", minutes: "{n}分", freeTag: "無料", paidTag: "Dinoアプリで" },
  nb: { bedtime: "Leggetid: 10 minutter eller mindre", shortestFirst: "korteste først", playAll: "trykk spill av for alle", newStories: "Nye historier", ages: "{a}–{b} år", free: "Gratis historier", all: "Alle historier, A–Å", others: "Andre språk", languages: "{n} språk", stories: "{n} historier", storiesOne: "1 historie", minutes: "{n} min", freeTag: "Gratis", paidTag: "I Dino-appen" },
  nl: { bedtime: "Bedtijd: 10 minuten of minder", shortestFirst: "kortste eerst", playAll: "druk op afspelen voor alles", newStories: "Nieuwe verhalen", ages: "{a}–{b} jaar", free: "Gratis verhalen", all: "Alle verhalen, A–Z", others: "Andere talen", languages: "{n} talen", stories: "{n} verhalen", storiesOne: "1 verhaal", minutes: "{n} min", freeTag: "Gratis", paidTag: "In de Dino-app" },
  pt: { bedtime: "Hora de dormir: 10 minutos ou menos", shortestFirst: "as mais curtas primeiro", playAll: "toque em reproduzir para todas", newStories: "Histórias novas", ages: "{a}–{b} anos", free: "Histórias grátis", all: "Todas as histórias, A–Z", others: "Outros idiomas", languages: "{n} idiomas", stories: "{n} histórias", storiesOne: "1 história", minutes: "{n} min", freeTag: "Grátis", paidTag: "No app Dino" },
  ru: { bedtime: "На ночь: 10 минут или меньше", shortestFirst: "сначала короткие", playAll: "«Играть» — все", newStories: "Новые истории", ages: "{a}–{b} лет", free: "Бесплатные истории", all: "Все истории, А–Я", others: "Другие языки", languages: "Языков: {n}", stories: "Историй: {n}", minutes: "{n} мин", freeTag: "Бесплатно", paidTag: "В приложении Dino" },
  sv: { bedtime: "Läggdags: 10 minuter eller mindre", shortestFirst: "kortast först", playAll: "tryck spela för alla", newStories: "Nya berättelser", ages: "{a}–{b} år", free: "Gratis berättelser", all: "Alla berättelser, A–Ö", others: "Andra språk", languages: "{n} språk", stories: "{n} berättelser", storiesOne: "1 berättelse", minutes: "{n} min", freeTag: "Gratis", paidTag: "I Dino-appen" },
  zh: { bedtime: "睡前故事：10分钟以内", shortestFirst: "最短的在前", playAll: "点播放可播全部", newStories: "新故事", ages: "{a}–{b}岁", free: "免费故事", all: "所有故事（按拼音）", others: "其他语言", languages: "{n} 种语言", stories: "{n} 个故事", minutes: "{n}分钟", freeTag: "免费", paidTag: "在 Dino 应用中" },
};

/** The languages that have a label set; a test keeps it equal to the language tiles' list (`TILE_LANGUAGES`). */
export const LABEL_LANGUAGES: readonly string[] = Object.keys(LABELS).sort();

/** The folders of a language's shelves, in the order a parent sees them (Slice 437). */
export const Shelf = { Bedtime: "bedtime", New: "new", Ages35: "age-3-5", Ages69: "age-6-9", Free: "free", All: "all", Others: "others" } as const;
export type ShelfName = (typeof Shelf)[keyof typeof Shelf];

function labelsFor(language: string): Labels {
  return LABELS[language] ?? (LABELS[FALLBACK_LANGUAGE] as Labels);
}

/** Digits in the language's own numerals (fa, ar), every other language unchanged. */
export function localDigits(text: string, language: string): string {
  const set = DIGIT_SETS[language];
  return set === undefined ? text : text.replace(/[0-9]/g, (digit) => set[Number(digit)] as string);
}

function fill(template: string, values: Readonly<Record<string, string | number>>): string {
  return template.replace(/\{(\w+)\}/g, (_, key: string) => String(values[key] ?? ""));
}

function storiesText(labels: Labels, count: number): string {
  return fill(count === 1 && labels.storiesOne !== undefined ? labels.storiesOne : labels.stories, { n: count });
}

/** "6 stories" in the language, with its digits. */
export function storiesLabel(language: string, count: number): string {
  return localDigits(storiesText(labelsFor(language), count), language);
}

export function shelfTitle(language: string, shelf: ShelfName): string {
  const labels = labelsFor(language);
  const ages = (a: number, b: number): string => fill(labels.ages, { a, b });
  const text = {
    [Shelf.Bedtime]: labels.bedtime,
    [Shelf.New]: labels.newStories,
    [Shelf.Ages35]: ages(3, 5),
    [Shelf.Ages69]: ages(6, 9),
    [Shelf.Free]: labels.free,
    [Shelf.All]: labels.all,
    [Shelf.Others]: labels.others,
  }[shelf];
  return localDigits(text, language);
}

/** The line under a shelf: the story count (Bedtime adds how it is ordered and that Play queues it all; Other languages counts languages). */
export function shelfSubtitle(language: string, shelf: ShelfName, count: number): string {
  const labels = labelsFor(language);
  if (shelf === Shelf.Others) return localDigits(fill(labels.languages, { n: count }), language);
  const parts = shelf === Shelf.Bedtime ? [storiesText(labels, count), labels.shortestFirst, labels.playAll] : [storiesText(labels, count)];
  return localDigits(parts.join(PART_SEPARATOR), language);
}

function accessWord(track: Track, labels: { readonly paidTag: string; readonly freeTag: string }): string | null {
  if (track.unlocked === true) return null;
  return track.paid ? labels.paidTag : labels.freeTag;
}

/** "Ages 3–5 · 6 min · Free" (or "In the Dino app"): the age part only when the book has an age range. A paid book a linked home's subscription opens (`unlocked`, Slice 439) drops the access word: "Ages 3–5 · 7 min". */
export function bookSubtitle(track: Track, language: string): string {
  const labels = labelsFor(language);
  const minutes = Math.max(1, Math.round(track.seconds / 60));
  const parts = [
    track.ageMin !== null && track.ageMax !== null ? fill(labels.ages, { a: track.ageMin, b: track.ageMax }) : null,
    fill(labels.minutes, { n: minutes }),
    accessWord(track, labels),
  ].filter((part): part is string => part !== null);
  return localDigits(parts.join(PART_SEPARATOR), language);
}

/**
 * Slice 439 (C3): what a speaker says in place of a paid book when the home has no active Dino subscription, in the 15 story languages. These are the texts the clips at
 * `dinoreader/sonos/subscribe-<language>.m4a` are rendered from (with the apps' narrator voices); the Worker serves the clips, never these words.
 */
export const SUBSCRIBE_MESSAGES: Readonly<Record<string, string>> = {
  en: "This story is in the Dino app. Subscribe in the Dino app to hear it on Sonos.",
  fa: "این قصه در برنامه‌ی دینو است. برای شنیدن آن در سونوس، در برنامه‌ی دینو اشتراک بگیرید.",
  ar: "هذه القصة في تطبيق دينو. اشترك في تطبيق دينو لتسمعها على سونوس.",
  da: "Denne historie er i Dino-appen. Abonnér i Dino-appen for at høre den på Sonos.",
  de: "Diese Geschichte gibt es in der Dino-App. Abonniere in der Dino-App, um sie auf Sonos zu hören.",
  es: "Este cuento está en la app de Dino. Suscríbete en la app de Dino para escucharlo en Sonos.",
  fi: "Tämä tarina on Dino-sovelluksessa. Tilaa Dino-sovelluksessa, niin voit kuunnella sen Sonosilla.",
  fr: "Cette histoire est dans l'appli Dino. Abonnez-vous dans l'appli Dino pour l'écouter sur Sonos.",
  ja: "このお話はDinoアプリにあります。Sonosで聞くには、Dinoアプリで購読してください。",
  nb: "Denne historien er i Dino-appen. Abonner i Dino-appen for å høre den på Sonos.",
  nl: "Dit verhaal staat in de Dino-app. Neem een abonnement in de Dino-app om het op Sonos te horen.",
  pt: "Esta história está no app Dino. Assine no app Dino para ouvi-la no Sonos.",
  ru: "Эта история есть в приложении Dino. Оформите подписку в приложении Dino, чтобы слушать её на Sonos.",
  sv: "Den här berättelsen finns i Dino-appen. Prenumerera i Dino-appen för att höra den på Sonos.",
  zh: "这个故事在 Dino 应用里。请在 Dino 应用中订阅，即可在 Sonos 上收听。",
};

/** A language's name in `inLanguage` ("German" for de in en), by the platform's own language data. */
export function languageNameIn(code: string, inLanguage: string): string {
  try {
    return new Intl.DisplayNames([inLanguage], { type: "language" }).of(code) ?? code.toUpperCase();
  } catch {
    // no-fallback: a code the platform does not know shows in capitals, as languageName does
    return code.toUpperCase();
  }
}

/** The parent-language row under a language: "German · 8 stories". */
export function languageSubtitle(code: string, parentLanguage: string, count: number): string {
  return `${languageNameIn(code, parentLanguage)}${PART_SEPARATOR}${storiesLabel(parentLanguage, count)}`;
}
