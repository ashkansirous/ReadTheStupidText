// Pure: which story page each spoken piece sits on, and the text a page shows.
// The narration manifests hold one entry per spoken piece (index, text, clip) and no page number, so the page is found
// the way the listening track does it (ListenTrackPlan.PagesOfSentences): by where the piece sits in the story's own text.
// The English manifest's pieces are found, in order, inside story.md's pages; every other language reuses the English
// piece's page because its manifest has the same indexes (checked: same count, same index numbers).

/** story.md pages: the file's pages are separated by a line holding only `---`. */
export function splitPages(storyMarkdown) {
  return storyMarkdown
    .replace(/\r\n/g, '\n')
    .split(/^---\s*$/m)
    .map((page) => page.trim())
    .filter((page) => page.length > 0);
}

/** For each English manifest entry: { page, breakBefore } (breakBefore = a paragraph break in the story text before the piece, within its page). */
const SPOKEN_WORD = /[\p{L}\p{N}]/u;

/** Placement is by sequence position: each piece must follow the previous one with only quotes, spaces and punctuation between; a gap holding words means a piece matched the wrong spot (a repeated sentence) or the manifest skips text. */
function assertNoSpokenGap(gap, entry, page) {
  if (SPOKEN_WORD.test(gap)) throw new Error(`Piece ${entry.index} ("${entry.text.slice(0, 40)}") sits after unspoken words on page ${page}: "${gap.trim().slice(0, 60)}".`);
}

export function placePieces(pages, englishEntries) {
  const placed = [];
  let page = 0;
  let cursor = 0;
  for (const entry of englishEntries) {
    let at = -1;
    const skippedTails = [];
    while (page < pages.length) {
      at = pages[page].indexOf(entry.text, cursor);
      if (at >= 0) break;
      skippedTails.push([pages[page].slice(cursor), page]);
      page += 1;
      cursor = 0;
    }
    if (at < 0) throw new Error(`Piece ${entry.index} ("${entry.text.slice(0, 40)}") is not in any story page after page ${page}.`);
    skippedTails.forEach(([tail, tailPage]) => assertNoSpokenGap(tail, entry, tailPage));
    const gap = pages[page].slice(cursor, at);
    assertNoSpokenGap(gap, entry, page);
    placed.push({ index: entry.index, page, breakBefore: placed.length > 0 && placed.at(-1).page === page && gap.includes('\n') });
    cursor = at + entry.text.length;
  }
  return placed;
}

/** A language's pieces with their pages: its entries, the page of the English piece with the same index. */
export function piecesOfLanguage(placedEnglish, entries, language) {
  const byIndex = new Map(placedEnglish.map((p) => [p.index, p]));
  if (entries.length !== placedEnglish.length) {
    throw new Error(`[${language}] has ${entries.length} manifest entries; English has ${placedEnglish.length}.`);
  }
  return entries.map((entry) => {
    const place = byIndex.get(entry.index);
    if (!place) throw new Error(`[${language}] entry ${entry.index} has no English twin.`);
    return { index: entry.index, text: entry.text, clip: entry.clip, page: place.page, breakBefore: place.breakBefore };
  });
}

/** Groups the pieces by page, in reading order: [{ page, pieces: [...] }]. */
export function groupByPage(pieces) {
  const groups = [];
  for (const piece of pieces) {
    const last = groups.at(-1);
    if (last && last.page === piece.page) last.pieces.push(piece);
    else groups.push({ page: piece.page, pieces: [piece] });
  }
  return groups;
}
