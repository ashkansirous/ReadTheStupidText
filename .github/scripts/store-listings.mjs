#!/usr/bin/env node
/**
 * Builds the Microsoft Store listing JSON for one app from the committed listing files (Slice 374, items 4 and 5).
 * store-submit.yml runs it before `msstore publish`, so every release carries the current listing text.
 * Pure parsing lives in exported functions (store-listings.test.mjs); the CLI only reads files and prints JSON.
 *
 * Sources: RTST `docs/store-listings/<lang>.md`, Dino `docs/store-listings/dino/<lang>.md`. Only the 13 languages
 * added by Slice 374 are built; en and de were pushed by hand earlier and use another file layout, so they are left
 * exactly as the Store has them (the output never names them).
 *
 * Usage: node store-listings.mjs --app rtst|dino [--root <repo root>] [--out <file>] [--submission <file>]
 * With --submission (the JSON `msstore submission get` printed) the output is that whole submission with the listings merged in, ready for
 * `msstore submission update --payload`; without it the output is just { listings }.
 * store-submit.yml runs it once without --submission BEFORE `msstore publish --noCommit` (a bad listing file then fails before any draft
 * exists) and once with --submission after it (best-effort there: a failure only logs a warning).
 * Exit 1 with every problem listed when a file is missing a field or a text is over a Partner Center limit.
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

export const APPS = { rtst: 'rtst', dino: 'dino' };

/** Repo language file stem -> Partner Center listing language code (lower-case, as the submission API names them). */
export const LANGUAGES = {
  ar: 'ar', zh: 'zh-cn', da: 'da', nl: 'nl', fa: 'fa', fi: 'fi', fr: 'fr',
  ja: 'ja', nb: 'nb', pt: 'pt-br', ru: 'ru', es: 'es', sv: 'sv',
};

export const LIMITS = { title: 200, shortDescription: 1000, description: 10000, keywords: 7, keywordLength: 30, keywordWords: 21 };

const RTST_TITLE = 'Read The Stupid Text';
const STORE_NAME = 'Microsoft Store';

/** The text after the first `<label prefix> ...:` line, trimmed; undefined when no such line. */
export function labelValue(text, prefix) {
  const re = new RegExp(`^(?:${prefix})[^\\n]*?\\):[ \\t]*(.+)$`, 'm');
  const m = re.exec(text);
  return m ? m[1].trim() : undefined;
}

/** The body of the `## <heading starts with>` section, up to the next `## ` heading or the end of the file. */
export function sectionBody(text, headingStart) {
  const lines = text.replace(/\r\n/g, '\n').split('\n');
  const start = lines.findIndex((l) => l.startsWith('## ') && l.slice(3).startsWith(headingStart));
  if (start < 0) return undefined;
  const rest = lines.slice(start + 1);
  const end = rest.findIndex((l) => l.startsWith('## '));
  return (end < 0 ? rest : rest.slice(0, end)).join('\n').trim();
}

export function splitTerms(line) {
  return line.split(';').map((t) => t.trim()).filter(Boolean);
}

export function parseRtst(text) {
  const long = sectionBody(text, 'Long description');
  const note = sectionBody(text, 'Microsoft Store only');
  const short = labelValue(text, 'Short description');
  const terms = labelValue(text, 'Search terms');
  return {
    title: RTST_TITLE,
    shortDescription: short,
    description: short && long && note ? `${short}\n\n${long}\n\n${note}` : undefined,
    keywords: terms ? splitTerms(terms) : undefined,
  };
}

export function parseDino(text) {
  const body = sectionBody(text, 'Description');
  const terms = labelValue(text, 'Microsoft search terms');
  return {
    title: labelValue(text, 'Title'),
    shortDescription: labelValue(text, 'Play short description'),
    description: body ? body.replaceAll('Google Play', STORE_NAME) : undefined,
    keywords: terms ? splitTerms(terms) : undefined,
  };
}

function wordCount(terms) {
  return terms.join(' ').split(/\s+/).filter(Boolean).length;
}

/** Problems with one parsed listing (missing field or over a Partner Center limit); empty when fine. */
export function validateListing(l) {
  const out = [];
  for (const f of ['title', 'shortDescription', 'description', 'keywords']) {
    if (!l[f] || (Array.isArray(l[f]) && l[f].length === 0)) out.push(`missing ${f}`);
  }
  if (out.length) return out;
  if (l.title.length > LIMITS.title) out.push(`title is ${l.title.length} chars (max ${LIMITS.title})`);
  if (l.shortDescription.length > LIMITS.shortDescription) out.push(`short description is ${l.shortDescription.length} chars (max ${LIMITS.shortDescription})`);
  if (l.description.length > LIMITS.description) out.push(`description is ${l.description.length} chars (max ${LIMITS.description})`);
  if (l.keywords.length > LIMITS.keywords) out.push(`${l.keywords.length} search terms (max ${LIMITS.keywords})`);
  if (wordCount(l.keywords) > LIMITS.keywordWords) out.push(`search terms hold ${wordCount(l.keywords)} words (max ${LIMITS.keywordWords})`);
  for (const k of l.keywords) if (k.length > LIMITS.keywordLength) out.push(`search term "${k}" is ${k.length} chars (max ${LIMITS.keywordLength})`);
  return out;
}

/** One Partner Center listing entry: the submission API's `baseListing` fields we own. Nothing else (features, images, release notes) is named, so the Store keeps them. */
export function toBaseListing(l) {
  return { baseListing: { title: l.title, shortDescription: l.shortDescription, description: l.description, keywords: l.keywords } };
}

/**
 * Builds { listings, errors } from a reader `read(stem) -> text | undefined`.
 * `listings` maps Partner Center language code -> entry; `errors` lists every problem with its language.
 */
export function buildListings(app, read) {
  const parse = app === APPS.dino ? parseDino : parseRtst;
  const listings = {};
  const errors = [];
  for (const [stem, code] of Object.entries(LANGUAGES)) {
    const text = read(stem);
    if (text === undefined) { errors.push(`${stem}: file not found`); continue; }
    const listing = parse(text);
    const problems = validateListing(listing);
    problems.forEach((p) => errors.push(`${stem}: ${p}`));
    if (problems.length === 0) listings[code] = toBaseListing(listing);
  }
  return { listings, errors };
}

/** The key of `obj` equal to `name` ignoring case, else undefined (the CLI prints PascalCase, the API docs camelCase). */
function keyIgnoringCase(obj, name) {
  return Object.keys(obj).find((k) => k.toLowerCase() === name.toLowerCase());
}

function getOrCreate(obj, name) {
  const key = keyIgnoringCase(obj, name) ?? name;
  if (obj[key] === undefined || obj[key] === null) obj[key] = {};
  return obj[key];
}

function setField(obj, name, value) {
  obj[keyIgnoringCase(obj, name) ?? name] = value;
}

/**
 * Merges built listings into the submission JSON that `msstore submission get` printed, and returns the new submission.
 * Only Title, ShortDescription, Description and Keywords of each built language change; every other field (pricing, packages, images,
 * features, release notes) and every language not built (en, de) is kept exactly. Refuses a submission without Pricing.PriceId, as
 * `msstore submission update` does, so a bad read can never reset the app's price. Property names follow the casing already present.
 */
export function mergeListings(submission, built) {
  const merged = structuredClone(submission);
  const pricing = merged.Pricing ?? merged.pricing;
  const priceId = pricing && (pricing.PriceId ?? pricing.priceId);
  if (!priceId) throw new Error('The submission has no Pricing.PriceId; refusing to update it');
  const cased = merged.Pricing ? (n) => n : (n) => n[0].toLowerCase() + n.slice(1);
  const listings = getOrCreate(merged, cased('Listings'));
  const warnings = [];
  for (const [code, entry] of Object.entries(built)) {
    const target = listingTarget(listings, code, cased, warnings);
    if (target) writeBaseListing(target, entry.baseListing, cased);
  }
  return { submission: merged, warnings };
}

/** Existing listing key for `code`: exact (any case), else the one regional key of the same bare language (`fr` -> `fr-fr`); undefined when none or ambiguous. */
export function findListingKey(keys, code) {
  const exact = keys.find((k) => k.toLowerCase() === code);
  if (exact) return exact;
  if (code === 'pt-br') return keys.find((k) => k.toLowerCase() === 'pt') ?? keys.find((k) => k.toLowerCase() === 'pt-pt');
  if (code.includes('-')) return code === 'zh-cn' ? keys.find((k) => k.toLowerCase() === 'zh-hans') : undefined;
  const sameLanguage = keys.filter((k) => k.toLowerCase().split('-')[0] === code);
  if (sameLanguage.length === 1) return sameLanguage[0];
  return sameLanguage.find((k) => k.toLowerCase() === `${code}-${code}`);
}

/** The listing object to write into: the existing one, or a new one carrying the English listing's images; undefined (with a warning) when it cannot be made. */
function listingTarget(listings, code, cased, warnings) {
  const keys = Object.keys(listings);
  const existing = findListingKey(keys, code);
  if (existing) return getOrCreate(listings, existing);
  if (keys.some((k) => k.toLowerCase().split('-')[0] === code.split('-')[0] && !code.includes('-'))) {
    warnings.push(`${code}: several regional listings exist and none is ${code}-${code}; skipped`);
    return undefined;
  }
  const en = keys.find((k) => k.toLowerCase() === 'en-us') ?? keys.find((k) => k.toLowerCase() === 'en') ?? keys.find((k) => k.toLowerCase().startsWith('en-'));
  const enBase = en && listings[en][cased('BaseListing')];
  const imagesKey = enBase && Object.keys(enBase).find((k) => k.toLowerCase() === 'images');
  if (!imagesKey) {
    warnings.push(`${code}: new language and no English listing images to copy; skipped`);
    return undefined;
  }
  const listing = getOrCreate(listings, code);
  getOrCreate(listing, cased('BaseListing'))[imagesKey] = structuredClone(enBase[imagesKey]);
  return listing;
}

function writeBaseListing(listing, fields, cased) {
  const base = getOrCreate(listing, cased('BaseListing'));
  setField(base, cased('Title'), fields.title);
  setField(base, cased('ShortDescription'), fields.shortDescription);
  setField(base, cased('Description'), fields.description);
  setField(base, cased('Keywords'), fields.keywords);
}

export function listingsFolder(root, app) {
  return app === APPS.dino ? join(root, 'docs', 'store-listings', 'dino') : join(root, 'docs', 'store-listings');
}

function readOrUndefined(path) {
  try {
    return readFileSync(path, 'utf8');
  } catch (e) {
    if (e.code === 'ENOENT') return undefined;
    throw e;
  }
}

function parseArgs(argv) {
  const args = { root: join(dirname(fileURLToPath(import.meta.url)), '..', '..') };
  for (let i = 0; i < argv.length; i += 2) {
    const key = argv[i];
    if (!['--app', '--root', '--out', '--submission'].includes(key) || argv[i + 1] === undefined) throw new Error(`Bad argument ${key}`);
    args[key.slice(2)] = argv[i + 1];
  }
  if (!Object.values(APPS).includes(args.app)) throw new Error('--app must be rtst or dino');
  return args;
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  const folder = listingsFolder(args.root, args.app);
  const { listings, errors } = buildListings(args.app, (stem) => readOrUndefined(join(folder, `${stem}.md`)));
  if (errors.length) {
    for (const e of errors) console.error(`::error::store listing ${args.app} ${e}`);
    process.exit(1);
  }
  let result = { listings };
  if (args.submission) {
    const merged = mergeListings(JSON.parse(readFileSync(args.submission, 'utf8')), listings);
    merged.warnings.forEach((w) => console.error(`::warning::store listing ${args.app} ${w}`));
    result = merged.submission;
  }
  const json = JSON.stringify(result);
  if (args.out) writeFileSync(args.out, json, 'utf8');
  else console.log(json);
  console.error(`Built ${Object.keys(listings).length} ${args.app} listings: ${Object.keys(listings).join(', ')}`);
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) main();
