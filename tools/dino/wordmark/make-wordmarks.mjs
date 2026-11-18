// Slice 410: rewrites the "Dino Book Reader" wordmark (plain outlines, Fredoka weight 600) into the Sonos brand SVGs.
// Only the text <path> and the mascot group's x shift change; colours, art paths and sizes stay. The public name is
// read from ApplicationTitle in the Dino csproj, never typed here.
// Run from this folder after `npm ci`:  node make-wordmarks.mjs
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import * as fontkit from "fontkit";
import { Resvg } from "@resvg/resvg-js";

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, "../../..");
const csproj = readFileSync(resolve(root, "src/Dino/DinoReader.App/DinoReader.App.csproj"), "utf8");
const NAME = csproj.match(/<ApplicationTitle>([^<]+)<\/ApplicationTitle>/)[1].trim();
const font = fontkit.openSync(resolve(root, "src/Dino/DinoReader.App/Resources/Fonts/Fredoka.ttf")).getVariation({ wght: 600 });
const sonos = resolve(root, "docs/design/dino-reader/sonos");
const MASCOT_SCALE = 2.33;
const BANNER = { margin: 60, gap: 28.2, capCenterY: 100.75, oldCap: 54.52, oldMascotLeft: 28, mascotWidth: 52 };

function layoutText(text) {
  const run = font.layout(text);
  const cmds = [];
  let x = 0;
  run.glyphs.forEach((g, i) => {
    cmds.push({ path: g.path, x: x + run.positions[i].xOffset });
    x += run.positions[i].xAdvance;
  });
  return { cmds, advance: x };
}

function outline(text, size, left, baseline) {
  const k = size / font.unitsPerEm;
  const { cmds, advance } = layoutText(text);
  const f = (n) => (Math.round(n * 100) / 100).toString();
  let d = "";
  for (const { path, x } of cmds) {
    for (const c of path.commands) {
      const pts = [];
      for (let i = 0; i < c.args.length; i += 2) pts.push(f(left + (x + c.args[i]) * k), f(baseline - c.args[i + 1] * k));
      d += { moveTo: "M", lineTo: "L", quadraticCurveTo: "Q", bezierCurveTo: "C", closePath: "Z" }[c.command] + pts.join(" ");
    }
  }
  return { d, width: advance * k };
}

const capHeight = (() => {
  const b = font.glyphForCodePoint("D".codePointAt(0)).bbox;
  return (b.maxY - b.minY) / font.unitsPerEm;
})();

/** The old file's embedded C2PA provenance manifest describes the old picture, so the regenerated plain-paths SVG carries none. */
function dropProvenance(svg) {
  return svg.replace(/<metadata>[\s\S]*?<\/metadata>/, "").replace(' xmlns:c2pa="http://c2pa.org/manifest"', "");
}

function replaceText(svg, d) {
  const i = svg.lastIndexOf("<path ");
  const head = svg.slice(0, i);
  const attrs = svg.slice(i).match(/^<path ([^>]*?) d="[^"]*"/)[1];
  return dropProvenance(`${head}<path ${attrs} d="${d}"></path></svg>`);
}

function banner() {
  const file = resolve(sonos, "support-banner.svg");
  const svg = readFileSync(file, "utf8");
  const maxTextWidth = 800 - 2 * BANNER.margin - BANNER.mascotWidth * MASCOT_SCALE - BANNER.gap;
  const probe = outline(NAME, 100, 0, 0);
  const size = Math.min(BANNER.oldCap / capHeight, (100 * maxTextWidth) / probe.width);
  const textW = (probe.width * size) / 100;
  const groupW = BANNER.mascotWidth * MASCOT_SCALE + BANNER.gap + textW;
  const mascotLeft = (800 - groupW) / 2;
  const textLeft = mascotLeft + BANNER.mascotWidth * MASCOT_SCALE + BANNER.gap;
  const baseline = BANNER.capCenterY + (capHeight * size) / 2;
  const tx = mascotLeft - BANNER.oldMascotLeft * MASCOT_SCALE;
  const moved = svg.replace(/<g transform="translate\(([-\d.]+) ([-\d.]+)\) scale\(2\.33\)">/, (_, _x, y) => `<g transform="translate(${tx.toFixed(2)} ${y}) scale(2.33)">`);
  const out = replaceText(moved, outline(NAME, size, textLeft, baseline).d);
  writeFileSync(file, out);
  writeFileSync(resolve(sonos, "support-banner.png"), new Resvg(out).render().asPng());
  console.log(`banner: font size ${size.toFixed(1)}, text ${textLeft.toFixed(1)}..${(textLeft + textW).toFixed(1)}`);
}

function fullLogo() {
  const file = resolve(sonos, "full-logo.svg");
  const svg = readFileSync(file, "utf8");
  const left = 21.17, oldCap = 13.8, maxRight = 178.5;
  const probe = outline(NAME, 100, 0, 0);
  const size = Math.min(oldCap / capHeight, (100 * (maxRight - left)) / probe.width);
  const baseline = 10 + (capHeight * size) / 2;
  const right = left + (probe.width * size) / 100;
  const width = Math.ceil((right + 1.5) * 10) / 10;
  const sized = svg.replace('width="129" height="20" viewBox="0 0 129 20"', `width="${width}" height="20" viewBox="0 0 ${width} 20"`);
  writeFileSync(file, replaceText(sized, outline(NAME, size, left, baseline).d));
  console.log(`full logo: font size ${size.toFixed(1)}, width ${width}`);
}

console.log(`name: ${NAME}`);
banner();
fullLogo();
