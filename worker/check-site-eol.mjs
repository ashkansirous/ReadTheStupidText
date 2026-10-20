// Fails a deploy if any file in ./site has Windows (CRLF) line endings.
// A Windows checkout with core.autocrlf=true writes CRLF, which would upload
// different bytes than the LF files in git (and than a Linux Workers Builds
// deploy). Fix with: git -c core.autocrlf=false checkout -- site/
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join } from "node:path";

const SITE_DIR = new URL("../site/", import.meta.url);

function listFiles(dir) {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    return statSync(path).isDirectory() ? listFiles(path) : [path];
  });
}

const withCrlf = listFiles(SITE_DIR.pathname.replace(/^\/([A-Za-z]:)/, "$1"))
  .filter((file) => readFileSync(file).includes("\r\n"));

if (withCrlf.length > 0) {
  console.error(`CRLF line endings in: ${withCrlf.join(", ")}`);
  console.error("Run: git -c core.autocrlf=false checkout -- site/");
  process.exit(1);
}
console.log("site/ line endings OK (LF).");
