// Pure: the HTML of one still. Laid out on a 1920 x 1080 page that Chrome captures at device scale 2 (3840 x 2160).
// The look follows the app's reading screen: a warm paper ground, the page picture, the page's text large enough for a
// TV across the room, the spoken sentence on a highlighter band. Picture on the reading side's start (left, or right in RTL).

import { RTL_LANGUAGES } from './strings.mjs';

export const SITE_ADDRESS = 'dinoreader.sirous.uk';
export const SITE_URL = `https://${SITE_ADDRESS}/`;
export const PAGE_WIDTH = 1920;
export const PAGE_HEIGHT = 1080;

const FONT_STACK = {
  ja: "'Yu Gothic UI','Yu Gothic','Meiryo','Segoe UI',sans-serif",
  zh: "'Microsoft YaHei UI','Microsoft YaHei','Segoe UI',sans-serif",
  ar: "'Segoe UI','Arial','Tahoma',sans-serif",
  fa: "'Segoe UI','Arial','Tahoma',sans-serif",
};
const DEFAULT_FONT = "'Segoe UI','Trebuchet MS','Arial',sans-serif";

export function escapeHtml(text) {
  return text.replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
}

/** Page text size by length, so a long page still fits the panel; never below the TV-readable floor. */
export function textSizePx(characters) {
  if (characters <= 160) return 62;
  if (characters <= 260) return 54;
  if (characters <= 380) return 46;
  return 40;
}

const BASE_CSS = `
*{margin:0;padding:0;box-sizing:border-box}
html,body{width:${PAGE_WIDTH}px;height:${PAGE_HEIGHT}px;overflow:hidden;background:#f6efe0;color:#2b2540}
body{display:flex;align-items:center;justify-content:center;gap:72px;padding:0 90px}
.art{flex:none;height:900px;width:675px;border-radius:28px;object-fit:cover;box-shadow:0 18px 40px rgba(43,37,64,.28)}
.panel{flex:1;min-width:0;display:flex;flex-direction:column;justify-content:center}
.text{line-height:1.55;font-weight:600}
.text p+p{margin-top:.55em}
.piece{padding:.04em .22em;border-radius:.3em;box-decoration-break:clone;-webkit-box-decoration-break:clone}
.piece.on{background:#ffd95c;box-shadow:0 0 0 .12em #ffd95c}
.qr{position:absolute;bottom:44px;inset-inline-end:60px;display:flex;align-items:center;gap:20px;font-size:26px;font-weight:600;opacity:.92}
.qr svg{width:150px;height:150px;background:#fff;border-radius:12px;padding:8px}
.title{font-size:92px;line-height:1.15;font-weight:800}
.author{margin-top:28px;font-size:38px;opacity:.75}
.credits{font-size:36px;line-height:1.5;opacity:.85;margin-bottom:36px}
.grownups{font-size:52px;line-height:1.5;font-weight:700}
`;

function pageDocument({ language, body, css = '' }) {
  const dir = RTL_LANGUAGES.includes(language) ? 'rtl' : 'ltr';
  const font = FONT_STACK[language] ?? DEFAULT_FONT;
  return `<!doctype html><html lang="${language}" dir="${dir}"><head><meta charset="utf-8"><style>${BASE_CSS}body{font-family:${font}}${css}</style></head><body>${body}</body></html>`;
}

function qrBlock(qrSvg) {
  return qrSvg ? `<div class="qr">${qrSvg}<span dir="ltr">${SITE_ADDRESS}</span></div>` : `<div class="qr"><span dir="ltr">${SITE_ADDRESS}</span></div>`;
}

/** The text of a page: paragraphs (a paragraph break where the story has one), the spoken piece on the band. Every piece's text is its manifest entry's, verbatim. */
export function pageTextHtml(pieces, activeIndex) {
  const paragraphs = [];
  pieces.forEach((piece, i) => {
    if (i === 0 || piece.breakBefore) paragraphs.push([]);
    paragraphs.at(-1).push(piece);
  });
  return paragraphs
    .map((group) => `<p>${group.map((p) => `<span class="piece${p.index === activeIndex ? ' on' : ''}" data-piece="${p.index}">${escapeHtml(p.text)}</span>`).join(' ')}</p>`)
    .join('');
}

export function pageStill({ language, pictureUrl, pieces, activeIndex }) {
  const characters = pieces.reduce((n, p) => n + p.text.length, 0);
  const body = `<img class="art" src="${pictureUrl}"><div class="panel"><div class="text" style="font-size:${textSizePx(characters)}px">${pageTextHtml(pieces, activeIndex)}</div></div>`;
  return pageDocument({ language, body });
}

export function openingStill({ language, coverUrl, title, author, qrSvg }) {
  const body = `<img class="art" src="${coverUrl}"><div class="panel"><div class="title">${escapeHtml(title)}</div>${author ? `<div class="author">${escapeHtml(author)}</div>` : ''}</div>${qrBlock(qrSvg)}`;
  return pageDocument({ language, body });
}

export function closingStill({ language, backCoverUrl, credits, aiLine, grownUps, qrSvg }) {
  const body = `<img class="art" src="${backCoverUrl}"><div class="panel"><div class="credits">${escapeHtml(credits)}<br>${escapeHtml(aiLine)}</div><div class="grownups">${escapeHtml(grownUps)}</div></div>${qrBlock(qrSvg)}`;
  return pageDocument({ language, body });
}

/** The 1280 x 720 thumbnail (captured at scale 1): the cover, the title in the video's language, the "read-aloud story" tag. */
export function thumbnailStill({ language, coverUrl, title, tag }) {
  const css = 'html,body{width:1280px;height:720px}.art{height:640px;width:480px}.title{font-size:84px}.tag{margin-top:30px;font-size:40px;font-weight:700;background:#ffd95c;display:inline-block;padding:6px 22px;border-radius:16px}body{gap:56px;padding:0 70px}';
  const body = `<img class="art" src="${coverUrl}"><div class="panel"><div class="title">${escapeHtml(title)}</div><div><span class="tag">${escapeHtml(tag)}</span></div></div>`;
  return pageDocument({ language, body, css });
}
