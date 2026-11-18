// Slices 410 and 412: the public name of the Dino app is `publicName` in tools/dino/public-name.json ("Dino: Kids Books & Stories"); the code name "Dino Reader" and the old name "Dino Book Reader" must
// not appear in any file a person reads (privacy policy, site, Alexa skill, Worker strings, the book credits, store-text sections of slices),
// and the files that must carry the real name do (Sonos album name, PRIVACY.md).
// Run from the repo root: node tools/dino/check-public-name.mjs   (exit 1 lists each problem)
// STORE.md is not scanned: its "Dino Reader" lines are engineering prose (tags, crash endpoints), not store-listing text.
import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const NAME_FILE = "tools/dino/public-name.json";
// "Dino Reader" as the old name: any case, any run of spaces or line breaks (or &nbsp;) between the words; never "Dino Book Reader".
const OLD_NAMES = [
  { label: "Dino Reader", pattern: /\bdino(?:\s|&nbsp;|&#160;)+reader\b/gi },
  { label: "Dino Book Reader", pattern: /\bdino(?:\s|&nbsp;|&#160;)+book(?:\s|&nbsp;|&#160;)+reader\b/gi },
];
const STORE_LISTINGS_FOLDER = "docs/store-listings/dino";
// The 14 other languages' listing files (ar.md, de.md ...) still carry the old title until Slice 413 rewrites them.
const LANGUAGE_LISTING = /^[a-z]{2}\.md$/;
// Slice 412 Part B removes this: good-enough is a starter whose credits change means a rebuilt `good-enough.dino` under
// src/Dino/DinoReader.App, and that path starts release-dino.yml (a Play upload) until Dino's first production release is out.
const PENDING_PART_B_BOOKS = new Set(["good-enough"]);
const TEXT_FILES = ["PRIVACY.md"];
const TEXT_FOLDERS = ["site", "alexa/skill-package"];
const BOOKS_FOLDER = "content/dino";
const BOOK_FILE = "book.json";
const STRING_ONLY_FOLDER = "worker";
const SLICES_FOLDER = "docs/plan/slices";
const SECTION_HEADING = /^#{1,6}\s+(Submission text|Store text)\b/i;
const ANY_HEADING = /^#{1,6}\s/;
const TEXT_EXTENSIONS = new Set([".md", ".html", ".json", ".txt", ".xml", ".svg"]);
const SONOS_FILE = "worker/sonos.ts";
const SONOS_NAME_CONSTANTS = ["ALBUM_NAME", "ARTIST_FALLBACK"];
const REGEX_KEYWORDS = new Set(["return", "typeof", "case", "in", "of", "delete", "void", "throw", "new", "else", "do", "yield", "await"]);

export function publicName(root) {
  const data = JSON.parse(readFileSync(join(root, NAME_FILE), "utf8"));
  if (typeof data.publicName !== "string" || data.publicName.trim() === "") throw new Error(`No publicName in ${NAME_FILE}`);
  return data.publicName.trim();
}

function countNewlines(text) {
  return text.split("\n").length - 1;
}

function skipLineComment(src, st) {
  while (st.i < src.length && src[st.i] !== "\n") st.i++;
}

function skipBlockComment(src, st) {
  const end = src.indexOf("*/", st.i + 2);
  const stop = end === -1 ? src.length : end + 2;
  st.line += countNewlines(src.slice(st.i, stop));
  st.i = stop;
}

function readQuoted(src, st, quote, literals) {
  const start = st.i + 1;
  const startLine = st.line;
  st.i++;
  while (st.i < src.length && src[st.i] !== quote && src[st.i] !== "\n") {
    if (src[st.i] === "\\") st.i++;
    st.i++;
  }
  literals.push({ text: src.slice(start, st.i), line: startLine });
  st.i++;
}

function readRegex(src, st) {
  let inClass = false;
  st.i++;
  while (st.i < src.length && src[st.i] !== "\n") {
    const c = src[st.i];
    if (c === "\\") st.i++;
    else if (c === "[") inClass = true;
    else if (c === "]") inClass = false;
    else if (c === "/" && !inClass) break;
    st.i++;
  }
  st.i++;
}

function readTemplate(src, st, literals) {
  const startLine = st.line;
  let text = "";
  st.i++;
  while (st.i < src.length && src[st.i] !== "`") {
    if (src[st.i] === "\\") {
      text += src.slice(st.i, st.i + 2);
      st.i += 2;
    } else if (src[st.i] === "$" && src[st.i + 1] === "{") {
      st.i += 2;
      scanCode(src, st, literals, true);
    } else {
      if (src[st.i] === "\n") st.line++;
      text += src[st.i++];
    }
  }
  literals.push({ text, line: startLine });
  st.i++;
}

function regexAllowed(prevToken) {
  if (prevToken === "") return true;
  if (/^[\w$]+$/.test(prevToken)) return REGEX_KEYWORDS.has(prevToken);
  return !/^[)\]]$/.test(prevToken);
}

function readWord(src, st) {
  const start = st.i;
  while (st.i < src.length && /[\w$]/.test(src[st.i])) st.i++;
  return src.slice(start, st.i);
}

function scanCode(src, st, literals, inTemplateExpression) {
  let depth = 0;
  let prev = "";
  while (st.i < src.length) {
    const c = src[st.i];
    const next = src[st.i + 1];
    if (c === "/" && next === "/") skipLineComment(src, st);
    else if (c === "/" && next === "*") skipBlockComment(src, st);
    else if (c === '"' || c === "'") { readQuoted(src, st, c, literals); prev = '"'; }
    else if (c === "`") { readTemplate(src, st, literals); prev = '"'; }
    else if (c === "/" && regexAllowed(prev)) { readRegex(src, st); prev = "/re"; }
    else if (c === "}" && inTemplateExpression && depth === 0) { st.i++; return; }
    else if (/[\w$]/.test(c)) prev = readWord(src, st);
    else {
      if (c === "{") depth++;
      if (c === "}") depth--;
      if (c === "\n") st.line++;
      if (!/\s/.test(c)) prev = c;
      st.i++;
    }
  }
}

/** The string literals of TypeScript/JavaScript source (template literals included, comments and regex literals left out), as { text, line }. */
export function stringLiterals(source) {
  const literals = [];
  scanCode(source, { i: 0, line: 1 }, literals, false);
  return literals;
}

/** The slice file's sections under a Submission text / Store text heading, each as one { text, line } (line of its first row). */
export function submissionLines(markdown) {
  const sections = [];
  let current = null;
  markdown.split(/\r?\n/).forEach((text, index) => {
    if (SECTION_HEADING.test(text)) {
      current = { text: "", line: index + 1 };
      sections.push(current);
    } else if (ANY_HEADING.test(text)) current = null;
    if (current !== null) current.text += `${text}\n`;
  });
  return sections;
}

/** Every use of the old name in the entries, found per entry across spaces and line breaks, as `file:line: "Dino Reader"`. */
export function hitsIn(entries, file) {
  return entries.flatMap((entry) =>
    OLD_NAMES.flatMap(({ label, pattern }) =>
      [...entry.text.matchAll(pattern)].map((match) => `${file}:${entry.line + countNewlines(entry.text.slice(0, match.index))}: "${label}"`),
    ),
  );
}

function filesUnder(folder) {
  if (!existsSync(folder)) return [];
  return readdirSync(folder).flatMap((name) => {
    const path = join(folder, name);
    if (statSync(path).isDirectory()) return filesUnder(path);
    return [...TEXT_EXTENSIONS].some((ext) => name.endsWith(ext)) ? [path] : [];
  });
}

function storeListingFiles(root) {
  const folder = join(root, STORE_LISTINGS_FOLDER);
  if (!existsSync(folder)) return [];
  return readdirSync(folder).filter((n) => n.endsWith(".md") && !LANGUAGE_LISTING.test(n)).map((n) => join(folder, n));
}

function bookFiles(root) {
  const folder = join(root, BOOKS_FOLDER);
  if (!existsSync(folder)) return [];
  return readdirSync(folder).filter((stem) => !PENDING_PART_B_BOOKS.has(stem)).map((stem) => join(folder, stem, BOOK_FILE)).filter(existsSync);
}

function workerSources(root) {
  const folder = join(root, STRING_ONLY_FOLDER);
  if (!existsSync(folder)) return [];
  return readdirSync(folder).filter((n) => n.endsWith(".ts") && !n.endsWith(".d.ts")).map((n) => join(folder, n));
}

/** Slice 410: the real name must be in the files that show it: the Sonos album and artist fallback, and the privacy policy. */
export function missingNameProblems(root, name) {
  const problems = [];
  const sonos = join(root, SONOS_FILE);
  if (existsSync(sonos)) {
    const source = readFileSync(sonos, "utf8");
    for (const constant of SONOS_NAME_CONSTANTS) {
      const found = new RegExp(`const ${constant}\\s*=\\s*"([^"]*)"`).exec(source);
      if (found === null || found[1] !== name) problems.push(`${SONOS_FILE}: ${constant} is ${found === null ? "missing" : `"${found[1]}"`}, expected "${name}"`);
    }
  }
  const privacy = join(root, TEXT_FILES[0]);
  if (existsSync(privacy) && !readFileSync(privacy, "utf8").includes(name)) problems.push(`${TEXT_FILES[0]}: does not contain "${name}"`);
  return problems;
}

export function findHits(root) {
  const read = (path) => readFileSync(path, "utf8");
  const rel = (path) => relative(root, path).split("\\").join("/");
  const whole = (path) => [{ text: read(path), line: 1 }];
  const hits = [];
  const textFiles = [...TEXT_FILES.map((f) => join(root, f)), ...TEXT_FOLDERS.flatMap((f) => filesUnder(join(root, f))), ...bookFiles(root), ...storeListingFiles(root)];
  for (const file of textFiles.filter(existsSync)) hits.push(...hitsIn(whole(file), rel(file)));
  for (const file of workerSources(root)) hits.push(...hitsIn(stringLiterals(read(file)), rel(file)));
  for (const file of filesUnder(join(root, SLICES_FOLDER))) hits.push(...hitsIn(submissionLines(read(file)), rel(file)));
  return hits;
}

function main() {
  const root = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
  const name = publicName(root);
  const hits = findHits(root);
  const missing = missingNameProblems(root, name);
  if (hits.length === 0 && missing.length === 0) {
    console.log(`Public name check passed: "${name}" is the only name in user-facing files.`);
    return 0;
  }
  if (hits.length > 0) console.error(`The public name is "${name}" (${NAME_FILE}); "Dino Reader" is only the code name and "Dino Book Reader" is the old name. Found:`);
  for (const hit of hits) console.error(`  ${hit}`);
  for (const problem of missing) console.error(`  ${problem}`);
  return 1;
}

if (process.argv[1] !== undefined && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) process.exitCode = main();
