// The free books and one book's data. The free list is read from the live store at run time (never a fixed list); each
// store book is mapped to its folder under content/dino/ by the id in its book.json. content/dino/ is read, never written.

import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { splitPages, placePieces, piecesOfLanguage, groupByPage } from './pages.mjs';
import { readLfs } from './tools.mjs';

export const STORIES_URL = 'https://dinoreader.sirous.uk/api/stories';
export const FREE_ACCESS = 'free';
export const ENGLISH = 'en';

export async function fetchFreeStories(fetchImpl = fetch) {
  const response = await fetchImpl(STORIES_URL);
  if (!response.ok) throw new Error(`${STORIES_URL} answered ${response.status}`);
  const { stories } = await response.json();
  return stories.filter((s) => s.access === FREE_ACCESS);
}

export function stemOfId(contentRoot, id) {
  for (const stem of readdirSync(contentRoot)) {
    const file = join(contentRoot, stem, 'book.json');
    if (existsSync(file) && JSON.parse(readFileSync(file, 'utf8')).id === id) return stem;
  }
  throw new Error(`No folder under ${contentRoot} holds a book with id ${id}.`);
}

export function titleIn(story, language) {
  return language === ENGLISH ? story.title : story.titles?.[language] ?? (() => { throw new Error(`${story.title} has no title in ${language}`); })();
}

export function creditsIn(story, language) {
  return language === ENGLISH ? story.credits : story.creditsByLanguage?.[language] ?? story.credits;
}

export function readManifest(bookDir, language) {
  const bytes = readLfs(join(bookDir, 'audio', language, 'manifest.json'));
  const manifest = JSON.parse(bytes.toString('utf8'));
  return manifest.entries.map((e) => ({ index: e.index, sentence: e.sentence, text: e.text, clip: e.clip }));
}

/** The book's pieces for one language with their pages, grouped by page in reading order. */
export function loadPieces(bookDir, language) {
  const pages = splitPages(readFileSync(join(bookDir, 'story.md'), 'utf8'));
  const placed = placePieces(pages, readManifest(bookDir, ENGLISH));
  const pieces = piecesOfLanguage(placed, readManifest(bookDir, language), language);
  return { pageCount: pages.length, groups: groupByPage(pieces) };
}
