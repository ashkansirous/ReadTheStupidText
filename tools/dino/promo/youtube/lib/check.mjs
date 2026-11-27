// Pure: the automatic per-video check (Slice 435 item 3). Reads the HTML each still was captured from and the audio plan,
// and compares them with the manifest: every highlighted sentence's on-screen text equals its manifest entry's text, the
// clip played at that moment is that entry's clip, every entry is highlighted once, in order, and the sentences shown on a page
// are exactly that page's group (`pageIndexes`, same list and order). It checks the output against the render's own inputs
// (the manifest the pieces came from), so it does NOT prove the page grouping is right against story.md (pages.mjs fails on
// words skipped between pieces) and it does NOT cover the captured PNG against the HTML (the screenshot is trusted). Returns a list of problems (empty = pass); never throws on a mismatch.

const PIECE_SPAN = /<span class="piece( on)?" data-piece="(\d+)">([^<]*)<\/span>/g;

export function unescapeHtml(text) {
  return text.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, "'").replace(/&amp;/g, '&');
}

export function piecesOnScreen(html) {
  return [...html.matchAll(PIECE_SPAN)].map((m) => ({ on: Boolean(m[1]), index: Number(m[2]), text: unescapeHtml(m[3]) }));
}

/**
 * @param {{ entries: {index:number,text:string,clip:string}[], stills: {pieceIndex:number, html:string}[], audio: object[] }} input
 * `entries` are the manifest entries of the pages rendered, in reading order; `stills` the page stills in order; `audio` timeline.audio.
 */
export function checkVideo({ entries, stills, audio }) {
  const problems = [];
  const byIndex = new Map(entries.map((e) => [e.index, e]));

  const highlighted = stills.map((s) => s.pieceIndex);
  const expected = entries.map((e) => e.index);
  if (highlighted.join(',') !== expected.join(',')) problems.push(`highlight order [${highlighted.slice(0, 8)}...] differs from the manifest order [${expected.slice(0, 8)}...]`);

  for (const still of stills) {
    const entry = byIndex.get(still.pieceIndex);
    const onScreen = piecesOnScreen(still.html);
    const active = onScreen.filter((p) => p.on);
    if (active.length !== 1) problems.push(`piece ${still.pieceIndex}: ${active.length} highlighted sentences on screen, expected 1`);
    else if (!entry || active[0].index !== still.pieceIndex || active[0].text !== entry.text) problems.push(`piece ${still.pieceIndex}: highlighted text "${active[0].text.slice(0, 30)}" is not the manifest text`);
    if (still.pageIndexes && onScreen.map((p) => p.index).join(',') !== still.pageIndexes.join(',')) problems.push(`page of piece ${still.pieceIndex}: shows [${onScreen.map((p) => p.index)}], the page holds [${still.pageIndexes}]`);
    for (const shown of onScreen) {
      const own = byIndex.get(shown.index);
      if (!own || own.text !== shown.text) problems.push(`piece ${shown.index} on the page of ${still.pieceIndex}: text differs from the manifest`);
    }
  }

  const played = audio.filter((part) => part.kind === 'clip');
  if (played.map((p) => p.index).join(',') !== expected.join(',')) problems.push('audio clip order differs from the manifest order');
  for (const part of played) {
    const entry = byIndex.get(part.index);
    if (!entry || entry.clip !== part.file) problems.push(`piece ${part.index}: audio plays ${part.file}, the manifest says ${entry?.clip}`);
  }
  return [...new Set(problems)];
}
