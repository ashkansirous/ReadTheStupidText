// Fails when wrangler.workers-dev.jsonc drifts from wrangler.jsonc.
// The only allowed difference is the `routes` entry (see docs/DEPLOYING.md).
import { readFileSync } from "node:fs";
import { isDeepStrictEqual } from "node:util";
import { DINO_PATH_PREFIX, Host } from "./download-keys.mjs";
import { redundantPaths, runFirst, stripJsonc } from "./jsonc-run-first.mjs";

const MAIN = new URL("../wrangler.jsonc", import.meta.url);
const DEV = new URL("../wrangler.workers-dev.jsonc", import.meta.url);

function load(url) {
  return JSON.parse(stripJsonc(readFileSync(url, "utf8")));
}

const main = load(MAIN);
const dev = load(DEV);
const routes = main.routes ?? [];
delete main.routes;

// Slice 74: the Dino Reader site is a second custom domain on this Worker, and
// both configs must let the Worker run first for every root page mirrored under
// the /dino/ prefix (workers.dev): each non-/dino run_worker_first entry needs
// its /dino twin ("/" gives "/dino" and "/dino/").
const isDinoPath = (path) => path === DINO_PATH_PREFIX || path.startsWith(`${DINO_PATH_PREFIX}/`);
const dinoTwins = (path) => (path === "/" ? [DINO_PATH_PREFIX, `${DINO_PATH_PREFIX}/`] : [`${DINO_PATH_PREFIX}${path}`]);
const expectedDinoPaths = runFirst(main).filter((path) => !isDinoPath(path)).flatMap(dinoTwins);
const lacksDinoPaths = (config) => expectedDinoPaths.filter((path) => !runFirst(config).includes(path));
const hasDinoDomain = routes.some((route) => route.pattern === Host.DinoReader && route.custom_domain === true);
if (!hasDinoDomain || lacksDinoPaths(main).length > 0 || lacksDinoPaths(dev).length > 0) {
  console.error(`Both configs need ${expectedDinoPaths.join(", ")} in assets.run_worker_first, and wrangler.jsonc needs the ${Host.DinoReader} custom domain.`);
  process.exit(1);
}

for (const [name, config] of [["wrangler.jsonc", main], ["wrangler.workers-dev.jsonc", dev]]) {
  const redundant = redundantPaths(config);
  if (redundant.length > 0) {
    console.error(`${name} has redundant assets.run_worker_first entries (already covered by a glob): ${redundant.join(", ")}`);
    process.exit(1);
  }
}

if (!isDeepStrictEqual(main, dev)) {
  console.error("wrangler.jsonc and wrangler.workers-dev.jsonc differ (ignoring `routes`).");
  console.error("main:", JSON.stringify(main, null, 2));
  console.error("dev: ", JSON.stringify(dev, null, 2));
  process.exit(1);
}
console.log("wrangler configs in sync.");
