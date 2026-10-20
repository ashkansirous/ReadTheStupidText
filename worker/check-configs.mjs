// Fails when wrangler.workers-dev.jsonc drifts from wrangler.jsonc.
// The only allowed difference is the `routes` entry (see docs/DEPLOYING.md).
import { readFileSync } from "node:fs";
import { isDeepStrictEqual } from "node:util";

const MAIN = new URL("../wrangler.jsonc", import.meta.url);
const DEV = new URL("../wrangler.workers-dev.jsonc", import.meta.url);

// Removes // and /* */ comments and trailing commas, leaving string contents alone.
function stripJsonc(text) {
  let out = "";
  let i = 0;
  while (i < text.length) {
    const c = text[i];
    const next = text[i + 1];
    if (c === '"') {
      const start = i++;
      while (i < text.length && text[i] !== '"') i += text[i] === "\\" ? 2 : 1;
      out += text.slice(start, ++i);
    } else if (c === "/" && next === "/") {
      while (i < text.length && text[i] !== "\n") i++;
    } else if (c === "/" && next === "*") {
      const end = text.indexOf("*/", i + 2);
      i = end === -1 ? text.length : end + 2;
    } else {
      out += c;
      i++;
    }
  }
  return out.replace(/,(\s*[}\]])/g, "$1");
}

function load(url) {
  return JSON.parse(stripJsonc(readFileSync(url, "utf8")));
}

const main = load(MAIN);
const dev = load(DEV);
delete main.routes;

if (!isDeepStrictEqual(main, dev)) {
  console.error("wrangler.jsonc and wrangler.workers-dev.jsonc differ (ignoring `routes`).");
  console.error("main:", JSON.stringify(main, null, 2));
  console.error("dev: ", JSON.stringify(dev, null, 2));
  process.exit(1);
}
console.log("wrangler configs in sync.");
