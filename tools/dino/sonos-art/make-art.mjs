// Slice 437: makes the Sonos folder pictures in site/dino/sonos-art/ (seven shelf tiles and one tile per app language) from the mock's
// icons (docs/design/sonos/EN-Home.dc.html, EN-Languages.dc.html). Run once from the repo root after `npm ci`:
//   node tools/dino/sonos-art/make-art.mjs
// `sharp` is not a dependency of this repo: `npm ci` installs it only because wrangler's miniflare depends on it, and npm hoists it to
// node_modules/sharp. If a wrangler upgrade stops hoisting it, run `npm install --no-save sharp` first. This script lives outside
// site/ so it is not served; the PNGs are committed and this file only records how they were made.
// The language list is the Worker's own (TILE_LANGUAGES in worker/sonos-art.ts), so a tile exists for exactly the languages the Worker links.
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import sharp from "sharp";
import { TILE_LANGUAGES } from "../../../worker/sonos-art.ts";

const SIZE = 600;
const ICON_BOX = 24;
const ICON_SCALE = 0.5; // the icon takes half the tile, as the mock's 26 px icon in a 52 px tile
const LANGUAGE_TILE_COLOR = "#2C5C9A";
const OUTPUT_FOLDER = ["..", "..", "..", "site", "dino", "sonos-art"];

const SHELVES = {
  bedtime: ["#3B3F8F", "M20 14.5A8 8 0 1 1 9.5 4a6.5 6.5 0 0 0 10.5 10.5z"],
  new: ["#9A6412", "M12 3v4M12 17v4M3 12h4M17 12h4M6 6l2.5 2.5M15.5 15.5L18 18M6 18l2.5-2.5M15.5 8.5L18 6"],
  "age-3-5": ["#2F6B3A", "M12 21v-8M12 13c0-4-3-6-7-6 0 4 3 6 7 6zM12 11c0-4 3-6 7-6 0 4-3 6-7 6z"],
  "age-6-9": ["#1F6670", "M4 5h6a2 2 0 0 1 2 2v13a2 2 0 0 0-2-2H4zM20 5h-6a2 2 0 0 0-2 2v13a2 2 0 0 1 2-2h6z"],
  free: ["#8A3A62", "M4 11h16v10H4zM3 7h18v4H3zM12 7v14M12 7C10 3 6 4 8 7M12 7c2-4 6-3 4 0"],
  all: ["#4A4E57", "M4 18l3.5-12L11 18M5.3 14h4.4M14 6h6l-6 12h6"],
  others: ["#2C5C9A", "M3 12h18M12 3c3 3 3 15 0 18M12 3c-3 3-3 15 0 18M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0z"],
};

function shelfSvg(color, path) {
  const scale = (SIZE * ICON_SCALE) / ICON_BOX;
  const offset = (SIZE - ICON_BOX * scale) / 2;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${SIZE}" height="${SIZE}" viewBox="0 0 ${SIZE} ${SIZE}"><rect width="${SIZE}" height="${SIZE}" fill="${color}"/><g transform="translate(${offset} ${offset}) scale(${scale})" fill="none" stroke="#fff" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="${path}"/></g></svg>`;
}

function languageSvg(code) {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${SIZE}" height="${SIZE}" viewBox="0 0 ${SIZE} ${SIZE}"><rect width="${SIZE}" height="${SIZE}" fill="${LANGUAGE_TILE_COLOR}"/><text x="50%" y="50%" dy="0.35em" text-anchor="middle" font-family="Arial, Helvetica, sans-serif" font-size="230" font-weight="700" fill="#fff">${code.toUpperCase()}</text></svg>`;
}

const folder = join(dirname(fileURLToPath(import.meta.url)), ...OUTPUT_FOLDER);
const jobs = [
  ...Object.entries(SHELVES).map(([name, [color, path]]) => [`${name}.png`, shelfSvg(color, path)]),
  ...TILE_LANGUAGES.map((code) => [`lang-${code}.png`, languageSvg(code)]),
];
for (const [file, svg] of jobs) {
  await sharp(Buffer.from(svg)).png({ compressionLevel: 9 }).toFile(join(folder, file));
  console.log(`wrote ${file}`);
}
