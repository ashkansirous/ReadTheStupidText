#!/usr/bin/env node
/**
 * Slice 346 E5: puts the Dino store-purchase secrets from the secrets store (D:\secrets\dinoreader\secrets.env) on the live Worker and proves they arrived.
 *
 *   node .github/scripts/upload-worker-secrets.mjs            dry run (the default): says which of the 7 secrets the store can supply; writes nothing, calls nothing
 *   node .github/scripts/upload-worker-secrets.mjs --apply    `wrangler secret put` each one (value piped on stdin, never a command-line argument), then lists the Worker's secret names
 *   node .github/scripts/upload-worker-secrets.mjs --verify   only the listing: exits 1 when any wanted name is not on the Worker
 *
 * A store entry may be a plain value (`NAME=value`) or a file entry (`NAME_FILE=<path under the secrets root>`, how the Apple .p8 is kept): the file's text is the value.
 * Every source is checked before anything is put: one missing source means the Worker is left untouched. Values are never printed. The run exits non-zero when any wanted secret has no source or is missing from the Worker's list afterwards, so "all set" can never be reported by hand.
 * Optional secret APPLE_IAP_ALLOW_SANDBOX is a staging switch and is left alone here.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, isAbsolute, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const DEFAULT_SECRETS_ROOT = 'D:/secrets';
const PROJECT_FOLDER = 'dinoreader';
const SECRETS_FILE = 'secrets.env';
const FILE_SUFFIX = '_FILE';
const WRANGLER_CONFIG = 'wrangler.jsonc';

/** The Worker secret names (worker/entitlements) and the key each one is read from in the store; the first key that holds a value wins. */
export const WANTED = [
    { name: 'PLAY_SERVICE_ACCOUNT_JSON', sources: ['DINOREADER_PLAY_SERVICE_ACCOUNT_JSON', 'PLAY_SERVICE_ACCOUNT_JSON'] },
    { name: 'APPLE_IAP_KEY_P8', sources: ['APPLE_IAP_KEY_P8', 'APPLE_IAP_KEY_P8_FILE'] },
    { name: 'APPLE_IAP_KEY_ID', sources: ['APPLE_IAP_KEY_ID'] },
    { name: 'APPLE_IAP_ISSUER_ID', sources: ['APPLE_IAP_ISSUER_ID'] },
    { name: 'MS_STORE_TENANT_ID', sources: ['MS_STORE_TENANT_ID'] },
    { name: 'MS_STORE_CLIENT_ID', sources: ['MS_STORE_CLIENT_ID'] },
    { name: 'MS_STORE_CLIENT_SECRET', sources: ['MS_STORE_CLIENT_SECRET'] },
];

/** Pure: the KEY=value lines of a secrets.env (comment and blank lines skipped). */
export function parseEnv(text, strictNames = WANTED_KEYS) {
    const entries = {};
    for (const line of text.split(/\r?\n/)) {
        const match = /^([A-Za-z_][A-Za-z0-9_]*)=(.*)$/.exec(line);
        if (match) entries[match[1]] = unquote(match[2], match[1], strictNames.has(match[1]));
    }
    return entries;
}

/** Every key a wanted secret is read from (`NAME` or `NAME_FILE`): only these may stop a run on a malformed line; any other line is ignored as before. */
const WANTED_KEYS = new Set(WANTED.flatMap(({ sources }) => sources));

/** Pure: a value that starts with a quote is the text up to the matching close quote and everything after it (a trailing comment) is ignored, so no quote is uploaded to the Worker; a value that opens a quote and never closes it is a truncated line and throws (naming the secret, never its value); an unquoted value ends at ` # comment`. */
export function unquote(raw, name = 'a secret', strict = true) {
    const trimmed = raw.trim();
    const quote = trimmed[0];
    const quoted = quote === '"' || quote === "'";
    const close = quoted ? trimmed.indexOf(quote, 1) : -1;
    if (quoted && close < 0 && strict) throw new Error(`${name} opens a quote it never closes (a truncated line?); fix the secrets file`);
    if (close > 0) return trimmed.slice(1, close);
    return trimmed.replace(/\s+#.*$/, '').trim(); // an unquoted value ends at ` # comment`; a space or CR left on it would be uploaded
}

/** The secrets whose stored value is a PEM key kept on one line with `\n` escapes; only these get real newlines. Every other value (the Play JSON keeps its `\n` escapes inside a JSON string) is uploaded byte for byte. */
export const PEM_SECRETS = ['APPLE_IAP_KEY_P8'];

/** Pure: the value for `name` as the Worker must get it: a PEM secret has its two-character `\n` turned into newlines (or the Worker gets a broken key that `secret list` still shows by name); any other secret is returned unchanged. */
export function expandPemNewlines(name, value) {
    return PEM_SECRETS.includes(name) ? value.replaceAll('\\n', '\n') : value;
}

/** Pure: true when `file` stays inside `root` (a file entry must not point out of the secrets store). */
export function isInsideRoot(root, file) {
    const path = relative(resolve(root), resolve(root, file));
    const climbsOut = path === '..' || path.startsWith(`..${sep}`);
    return path !== '' && !climbsOut && !isAbsolute(path);
}

/**
 * Pure (the file read is injected): the value for each wanted secret, or a missing reason. A `*_FILE` source is read from under the secrets root.
 * Returns { values: Map name -> value, missing: [{ name, why }] }.
 */
export function resolveSecrets(entries, root, readText = (path) => readFileSync(path, 'utf8'), exists = existsSync) {
    const values = new Map();
    const missing = [];
    for (const { name, sources } of WANTED) {
        const found = firstValue(entries, sources, root, readText, exists);
        if (found.value) values.set(name, expandPemNewlines(name, found.value));
        else missing.push({ name, why: found.why });
    }
    return { values, missing };
}

function firstValue(entries, sources, root, readText, exists) {
    let why = `none of ${sources.join(', ')} is in the secrets store`;
    for (const source of sources) {
        const raw = entries[source];
        if (raw === undefined || raw === '') continue;
        if (!source.endsWith(FILE_SUFFIX)) return { value: raw };
        const read = readFileEntry(root, source, raw, readText, exists);
        if (read.value) return read;
        why = read.why;
    }
    return { value: null, why };
}

function readFileEntry(root, source, relativePath, readText, exists) {
    if (!isInsideRoot(root, relativePath)) return { value: null, why: `${source} points outside the secrets store` };
    const path = resolve(root, relativePath); // the same resolution isInsideRoot checked
    if (!exists(path)) return { value: null, why: `${source} names a file that does not exist` };
    const text = readText(path).trim();
    return text ? { value: text } : { value: null, why: `${source} names an empty file` };
}

/** Pure: the JSON array in wrangler's stdout: the whole text when it parses, else from the first line that starts with `[` to the last `]` (a banner or update notice around it is skipped). Null when there is none. */
export function jsonArrayIn(text) {
    try {
        return JSON.parse(text);
    } catch {
        const start = text.search(/^\[/m);
        const end = text.lastIndexOf(']');
        if (start < 0 || end < start) return null;
        try {
            return JSON.parse(text.slice(start, end + 1));
        } catch {
            return null;
        }
    }
}

/** Pure: the names wanted but absent from `wrangler secret list --format json` output (an array of { name, type }). Throws on output that is not that array. */
export function namesMissingFrom(listJson) {
    const listed = jsonArrayIn(listJson);
    if (listed === null) throw new Error('wrangler secret list did not answer JSON');
    if (!Array.isArray(listed)) throw new Error('wrangler secret list did not answer a list');
    const have = new Set(listed.map((entry) => entry?.name));
    return WANTED.map((wanted) => wanted.name).filter((name) => !have.has(name));
}

/** Pure: how a run reads: `dry`, `apply` or `verify`; anything else is an error so a typo never becomes a real run. */
export function parseMode(args) {
    if (args.length === 0) return 'dry';
    if (args.length === 1 && args[0] === '--apply') return 'apply';
    if (args.length === 1 && args[0] === '--verify') return 'verify';
    throw new Error(`Unknown arguments: ${args.join(' ')} (use --apply, --verify or nothing for a dry run)`);
}

const REDACTED = '[redacted]';
const FAILURE_LINES_SHOWN = 3;

/** Pure: `text` with every occurrence of any secret value (and of each line of a multi-line one) of any length replaced, longest first so a short value inside a long one cannot leave a fragment. */
export function redactValues(text, values) {
    const pieces = values.flatMap((value) => [value, ...String(value).split(/\r?\n/)]).filter((piece) => piece !== '');
    return [...new Set(pieces)].sort((a, b) => b.length - a.length).reduce((out, piece) => out.split(piece).join(REDACTED), text);
}

/** Pure: what may be printed of a failed wrangler run: its exit code and the first few stderr lines, each with the secret values (`values`, any length) and every long token-like run replaced, never the stderr as it came (an error that echoes its input would leak a secret fragment). */
export function describeFailure(status, stderr, values = []) {
    const lines = (stderr ?? '').split('\n').filter((line) => line.trim() !== '').slice(0, FAILURE_LINES_SHOWN);
    const redacted = lines.map((line) => redactValues(line, values).replace(/[A-Za-z0-9+/=_.-]{12,}/g, REDACTED).trim().slice(0, 120)).join(' | ');
    return redacted ? `exit code ${status}: ${redacted}` : `exit code ${status}`;
}

function wrangler(args, input) {
    const result = spawnSync('npx', ['--no-install', 'wrangler', ...args, '--config', WRANGLER_CONFIG], { input, encoding: 'utf8', shell: process.platform === 'win32', stdio: ['pipe', 'pipe', 'pipe'] });
    if (result.error) return { ok: false, out: '', err: `could not start wrangler: ${result.error.code ?? 'spawn error'}` }; // npx missing: not the secret's fault
    return { ok: result.status === 0, out: result.stdout ?? '', err: describeFailure(result.status, result.stderr, input === undefined ? [] : [input]) };
}

/** Puts the secrets in order and stops at the first failed put (a later put on top of a failed one could leave a mismatched pair on the Worker). Returns { failed, set, notSet } name lists; `putOne(name, value)` answers { ok, err }. */
export function putInOrder(values, putOne) {
    const set = [];
    const names = [...values.keys()];
    for (const [name, value] of values) {
        const put = putOne(name, value);
        if (!put.ok) return { failed: true, set, notSet: names.slice(set.length), reason: put.err };
        set.push(name);
    }
    return { failed: false, set, notSet: [], reason: '' };
}

function putAll(values) {
    const result = putInOrder(values, (name, value) => wrangler(['secret', 'put', name], value));
    result.set.forEach((name) => console.log(`  set: ${name}`));
    if (result.failed) {
        console.log(`  FAILED on ${result.notSet[0]} (${result.reason}); stopped here`);
        console.log(`  NOT set: ${result.notSet.join(', ')}`);
    }
    return result.failed;
}

function verifyOnWorker() {
    const listed = wrangler(['secret', 'list', '--format', 'json']);
    if (!listed.ok) {
        console.log(`  FAILED: wrangler secret list (${listed.err})`);
        return true;
    }
    const missing = namesMissingFrom(listed.out);
    missing.forEach((name) => console.log(`  MISSING on the Worker: ${name}`));
    if (missing.length === 0) console.log(`  all ${WANTED.length} secret names are on the Worker`);
    return missing.length > 0;
}

function main() {
    const mode = parseMode(process.argv.slice(2));
    process.chdir(join(dirname(fileURLToPath(import.meta.url)), '..', '..'));
    if (mode === 'verify') {
        process.exitCode = verifyOnWorker() ? 1 : 0;
        return;
    }
    const root = process.env.SECRETS_ROOT ?? DEFAULT_SECRETS_ROOT;
    const { values, missing } = resolveSecrets(parseEnv(readFileSync(join(root, PROJECT_FOLDER, SECRETS_FILE), 'utf8')), root);
    for (const name of values.keys()) console.log(`  source found: ${name}`);
    missing.forEach(({ name, why }) => console.log(`  NO SOURCE: ${name} (${why})`));
    if (mode === 'dry') {
        console.log(`DRY RUN (nothing is written; add --apply): ${values.size} of ${WANTED.length} secrets can be set`);
        process.exitCode = missing.length > 0 ? 1 : 0;
        return;
    }
    process.exitCode = applyAll(values, missing, putAll, verifyOnWorker) ? 1 : 0;
}

/** Every source is checked before any secret is put: one missing source means the Worker is left exactly as it was. Returns true when the run failed. */
export function applyAll(values, missing, put, verify) {
    if (missing.length > 0) {
        console.log(`NOTHING WAS SET: ${missing.length} secret(s) have no source, and a half-updated Worker is worse than an untouched one`);
        return true;
    }
    if (put(values)) {
        console.log('  not verifying: the names list cannot tell new values from old ones after a stopped put');
        return true;
    }
    return verify();
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    try {
        main();
    } catch (error) {
        console.error(error.message);
        process.exitCode = 1;
    }
}
