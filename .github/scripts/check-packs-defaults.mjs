// Slice 238: a packs.yml run started with its defaults must not stamp a pack with a version lower than
// the one the app's pinned catalog already lists (a phone holding version 2 would not see the rebuild,
// a fresh install would get version 1). This fails when any `*_pack_version` default in packs.yml is
// lower than that pack's version in the packs.json of the release PackConstants.CatalogUrl names.
//   node .github/scripts/check-packs-defaults.mjs                      (fetches the pinned packs.json)
//   node .github/scripts/check-packs-defaults.mjs --packs-json <file>  (offline, used by the test)
import { readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

export const WORKFLOW_PATH = '.github/workflows/packs.yml';
export const CONSTANTS_PATH = 'src/ReadTheStupidText.Application/Reading/PackConstants.cs';

// Each workflow input and the catalog pack ids it stamps. A new `*_pack_version` input must be added here.
export const INPUT_PACKS = {
    pack_version: (id) => id === 'supertonic-3',
    piper_pack_version: (id) => id === 'piper-fa',
    norwegian_pack_version: (id) => id === 'piper-nb',
    melo_pack_version: (id) => id === 'melo-zh-en',
    kokoro_pack_version: (id) => id === 'kokoro-en',
    ocr_pack_version: (id) => id.startsWith('ocr-'),
    translation_pack_version: (id) => id.startsWith('translation-'),
};

const INPUT_BLOCK = /^ {6}(\w*pack_version):\s*\r?\n((?: {8,}.*\r?\n?)*)/gm;
const DEFAULT_LINE = /^\s+default:\s*'?"?(\d+)'?"?\s*$/m;

export function readDefaults(workflowText) {
    const defaults = {};
    for (const match of workflowText.matchAll(INPUT_BLOCK)) {
        const value = DEFAULT_LINE.exec(match[2])?.[1];
        if (value === undefined) {
            throw new Error(`${WORKFLOW_PATH}: input ${match[1]} has no numeric default`);
        }
        defaults[match[1]] = Number(value);
    }
    return defaults;
}

export function readCatalogUrl(constantsText) {
    const url = /"(https:\/\/[^"]*\/packs-\d+\/packs\.json)"/.exec(constantsText)?.[1];
    if (!url) {
        throw new Error(`${CONSTANTS_PATH}: no CatalogUrl naming a packs-<n> release`);
    }
    return url;
}

export function findLowerDefaults(defaults, catalog) {
    const problems = [];
    for (const [input, value] of Object.entries(defaults)) {
        const matches = INPUT_PACKS[input];
        if (!matches) {
            problems.push(`${input}: not mapped to catalog packs; add it to INPUT_PACKS in check-packs-defaults.mjs`);
            continue;
        }
        const packs = catalog.filter((pack) => matches(pack.id));
        if (packs.length === 0) {
            problems.push(`${input}: no pack in the catalog matches it`);
        }
        for (const pack of packs) {
            if (!/^\d+$/.test(String(pack.version))) {
                problems.push(`${pack.id}: catalog version ${JSON.stringify(pack.version)} is not a whole number`);
            } else if (value < Number(pack.version)) {
                problems.push(`${input} defaults to ${value} but ${pack.id} is version ${pack.version} in the pinned catalog`);
            }
        }
    }
    return problems.concat(findUnmappedPacks(catalog));
}

function findUnmappedPacks(catalog) {
    return catalog
        .filter((pack) => !Object.values(INPUT_PACKS).some((matches) => matches(pack.id)))
        .map((pack) => `${pack.id}: catalog pack matches no packs.yml version input; add it to INPUT_PACKS and packs.yml`);
}

const FETCH_TRIES = 3;
const FETCH_TIMEOUT_MS = 20000;
const FETCH_BACKOFF_MS = 2000;

async function fetchCatalogOnce(url) {
    const response = await fetch(url, { signal: AbortSignal.timeout(FETCH_TIMEOUT_MS) });
    if (!response.ok) {
        throw new Error(`GET ${url} returned ${response.status}`);
    }
    return response.json();
}

async function fetchCatalog(url) {
    let lastError;
    for (let attempt = 1; attempt <= FETCH_TRIES; attempt++) {
        try {
            return await fetchCatalogOnce(url);
        } catch (error) {
            lastError = error;
            if (attempt < FETCH_TRIES) {
                await new Promise((resolve) => setTimeout(resolve, FETCH_BACKOFF_MS * attempt));
            }
        }
    }
    throw new Error(`could not fetch the pack catalog after ${FETCH_TRIES} tries (${FETCH_TIMEOUT_MS} ms each): ${lastError.message}`);
}

async function loadCatalog(url, packsJsonPath) {
    if (packsJsonPath) {
        return JSON.parse(readFileSync(packsJsonPath, 'utf8'));
    }
    return fetchCatalog(url);
}

async function main() {
    const flag = process.argv.indexOf('--packs-json');
    const packsJsonPath = flag > 0 ? process.argv[flag + 1] : undefined;
    const url = readCatalogUrl(readFileSync(CONSTANTS_PATH, 'utf8'));
    const defaults = readDefaults(readFileSync(WORKFLOW_PATH, 'utf8'));
    if (Object.keys(defaults).length === 0) {
        throw new Error(`${WORKFLOW_PATH}: no *_pack_version input defaults were read (indentation changed?)`);
    }
    const problems = findLowerDefaults(defaults, await loadCatalog(url, packsJsonPath));
    for (const problem of problems) {
        console.error(`::error file=${WORKFLOW_PATH}::${problem}`);
    }
    console.log(`${Object.keys(defaults).length} packs.yml version defaults checked against ${url}: ${problems.length} problem(s)`);
    process.exit(problems.length === 0 ? 0 : 1);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    await main();
}
