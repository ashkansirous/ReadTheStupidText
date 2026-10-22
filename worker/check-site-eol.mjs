// Fails a deploy if any file in ./site has Windows (CRLF) line endings.
// A Windows checkout with core.autocrlf=true writes CRLF, which would upload
// different bytes than the LF files in git (and than a Linux Workers Builds
// deploy). Fix with: git -c core.autocrlf=false checkout -- site/
import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { extname, join } from "node:path";

const SITE_DIR = new URL("../site/", import.meta.url);
// Only text files: a PNG signature has CR LF bytes by design (Slice 74 added icons).
const TEXT_EXTENSIONS = new Set([".html", ".css", ".js", ".mjs", ".json", ".txt", ".svg", ".xml", ".webmanifest"]);
// Pages every deploy must carry; a missing one would silently 404.
const REQUIRED_FILES = ["index.html", "privacy.html", "dino/index.html", "dino/privacy.html", "dino/icon.png", "img/control-panel.webp", "dino/reading-screen.webp"];

function listFiles(dir) {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    return statSync(path).isDirectory() ? listFiles(path) : [path];
  });
}

const withCrlf = listFiles(SITE_DIR.pathname.replace(/^\/([A-Za-z]:)/, "$1"))
  .filter((file) => TEXT_EXTENSIONS.has(extname(file).toLowerCase()))
  .filter((file) => readFileSync(file).includes("\r\n"));

const missing = REQUIRED_FILES.filter((file) => !existsSync(new URL(file, SITE_DIR)));
if (missing.length > 0) {
  console.error(`Missing site files: ${missing.join(", ")}`);
  process.exit(1);
}

if (withCrlf.length > 0) {
  console.error(`CRLF line endings in: ${withCrlf.join(", ")}`);
  console.error("Run: git -c core.autocrlf=false checkout -- site/");
  process.exit(1);
}
console.log("site/ line endings OK (LF).");
