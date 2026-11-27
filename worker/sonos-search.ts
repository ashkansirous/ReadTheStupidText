// Slice 440: search on Sonos. Pure: no I/O. Two categories, "Stories" (titles in the current story language) and "All languages"
// (every title in every language, the language's name in the subtitle). The matching ignores case and accents (`Intl.Collator` base sensitivity).

import { sortAlphabetically } from "./sonos-shelves.ts";
import type { Track } from "./sonos.ts";

/** The id of the search container a desktop app browses (`getMetadata` with this id lists the categories). */
export const SEARCH_ID = "search";
export const SearchCategory = { Stories: "stories", AllLanguages: "all-languages" } as const;
export type SearchCategoryId = (typeof SearchCategory)[keyof typeof SearchCategory];

const FALLBACK_LANGUAGE = "en";
const MAX_TERM_LENGTH = 64;
const COMBINING_MARKS = /\p{M}/gu;
// Letters Persian keyboards and Arabic ones write differently, and the zero-width joiner that only changes how a word is drawn.
const PERSIAN_FOLDS: ReadonlyArray<readonly [RegExp, string]> = [[/ي/g, "ی"], [/ى/g, "ی"], [/ك/g, "ک"], [/[‌‍]/g, ""]];
const BASE_COLLATOR = new Intl.Collator(undefined, { sensitivity: "base" });

/** The two category names in the 15 app languages (the same words as `site/dino/sonos/strings.xml`). */
const CATEGORY_NAMES: Readonly<Record<string, Readonly<Record<SearchCategoryId, string>>>> = {
  en: { stories: "Stories", "all-languages": "All languages" },
  fa: { stories: "داستان‌ها", "all-languages": "همه‌ی زبان‌ها" },
  ar: { stories: "القصص", "all-languages": "كل اللغات" },
  da: { stories: "Historier", "all-languages": "Alle sprog" },
  de: { stories: "Geschichten", "all-languages": "Alle Sprachen" },
  es: { stories: "Cuentos", "all-languages": "Todos los idiomas" },
  fi: { stories: "Tarinat", "all-languages": "Kaikki kielet" },
  fr: { stories: "Histoires", "all-languages": "Toutes les langues" },
  ja: { stories: "お話", "all-languages": "すべての言語" },
  nb: { stories: "Historier", "all-languages": "Alle språk" },
  nl: { stories: "Verhalen", "all-languages": "Alle talen" },
  pt: { stories: "Histórias", "all-languages": "Todos os idiomas" },
  ru: { stories: "Истории", "all-languages": "Все языки" },
  sv: { stories: "Berättelser", "all-languages": "Alla språk" },
  zh: { stories: "故事", "all-languages": "所有语言" },
};

/** The languages that have category names (a test keeps it equal to the label languages). */
export const SEARCH_LANGUAGES: readonly string[] = Object.keys(CATEGORY_NAMES).sort();

export interface CategoryRow {
  readonly id: SearchCategoryId;
  readonly name: string;
}

export function isSearchCategory(id: string | null): id is SearchCategoryId {
  return id === SearchCategory.Stories || id === SearchCategory.AllLanguages;
}

/** The categories in the order the app shows them, named in `label`'s language (English for a language with no names). */
export function searchCategories(label: string): CategoryRow[] {
  const names = CATEGORY_NAMES[label] ?? (CATEGORY_NAMES[FALLBACK_LANGUAGE] as Record<SearchCategoryId, string>);
  return [SearchCategory.Stories, SearchCategory.AllLanguages].map((id) => ({ id, name: names[id] }));
}

/** Lower-cased words are not enough: marks are stripped (é to e), Persian letter variants are unified, spaces are collapsed. */
export function foldText(text: string): string {
  let folded = text.normalize("NFKD").replace(COMBINING_MARKS, "");
  for (const [pattern, replacement] of PERSIAN_FOLDS) folded = folded.replace(pattern, replacement);
  return folded.replace(/\s+/g, " ").trim();
}

/** The term as it is matched: folded and cut to a sane length; empty when nothing is left. */
export function cleanTerm(term: string | null): string {
  return foldText(term ?? "").slice(0, MAX_TERM_LENGTH);
}

/** True when `text` holds `term` anywhere, comparing equal-length windows with the base-sensitivity collator. */
export function matchesTerm(text: string, term: string): boolean {
  const wanted = Array.from(cleanTerm(term));
  if (wanted.length === 0) return false;
  const have = Array.from(foldText(text));
  for (let start = 0; start + wanted.length <= have.length; start += 1) {
    if (BASE_COLLATOR.compare(have.slice(start, start + wanted.length).join(""), wanted.join("")) === 0) return true;
  }
  return false;
}

/** One hit: the book and the title that matched (the book's own for Stories). */
export interface SearchHit {
  readonly track: Track;
  /** True for an All languages hit: its subtitle names the language. */
  readonly withLanguage: boolean;
}

/**
 * The books a category finds for a term. Stories: this language's books whose title matches, A to Z. All languages: every book, in any
 * language, whose own title matches; ordered by language, then A to Z. An empty term finds nothing.
 */
export function searchTracks(tracks: readonly Track[], category: SearchCategoryId, term: string | null, language: string): SearchHit[] {
  const wanted = cleanTerm(term);
  if (wanted === "") return [];
  if (category === SearchCategory.Stories) {
    const found = tracks.filter((track) => track.language === language && matchesTerm(track.title, wanted));
    return sortAlphabetically(found, language).map((track) => ({ track, withLanguage: false }));
  }
  const codes = [...new Set(tracks.map((track) => track.language))].sort();
  return codes.flatMap((code) => {
    const found = tracks.filter((track) => track.language === code && matchesTerm(track.title, wanted));
    return sortAlphabetically(found, code).map((track) => ({ track, withLanguage: true }));
  });
}
