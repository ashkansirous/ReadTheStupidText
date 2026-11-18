#!/usr/bin/env node
/**
 * Runs store-products.mjs on the desk (Slice 411): loads the three stores' keys from the secrets store (D:/secrets) into the child's environment, without ever printing a value,
 * then passes every argument on unchanged.
 *
 * Usage: node .github/scripts/store-products-local.mjs --apply --book <stem>        (a new book, in all three stores)
 *        node .github/scripts/store-products-local.mjs --check-prices               (the next day: live Microsoft prices)
 *
 * Keys read (names only; values stay in memory):
 *   Play:      DINOREADER_PLAY_SERVICE_ACCOUNT_JSON in dinoreader/secrets.env is a path to the JSON file (relative to that folder); the script gets the file's text.
 *   Apple:     ASC_API_KEY_P8_BASE64, ASC_API_KEY_ID, ASC_API_ISSUER_ID in lets-call-mom/secrets.env.
 *   Microsoft: MS_STORE_TENANT_ID, MS_STORE_CLIENT_ID, MS_STORE_CLIENT_SECRET in dinoreader/secrets.env, passed as MS_SUBMISSION_*; MS_STORE_APP_ID is Dino Book Reader's Store id.
 * SECRETS_ROOT (optional) replaces D:/secrets, for tests.
 */

import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

import { isEntryScript } from './store-products.mjs';

export { isEntryScript };

const DEFAULT_SECRETS_ROOT = 'D:/secrets';
const DINO_SECRETS = 'dinoreader/secrets.env';
const APPLE_SECRETS = 'lets-call-mom/secrets.env';
const PLAY_PATH_KEY = 'DINOREADER_PLAY_SERVICE_ACCOUNT_JSON';
const APPLE_KEYS = ['ASC_API_KEY_P8_BASE64', 'ASC_API_KEY_ID', 'ASC_API_ISSUER_ID'];
const MS_KEY_MAP = { MS_STORE_TENANT_ID: 'MS_SUBMISSION_TENANT_ID', MS_STORE_CLIENT_ID: 'MS_SUBMISSION_CLIENT_ID', MS_STORE_CLIENT_SECRET: 'MS_SUBMISSION_CLIENT_SECRET' };
const DINO_STORE_APP_ID = '9N9VHRLCZXN5';
const SCRIPT = join(dirname(fileURLToPath(import.meta.url)), 'store-products.mjs');

/** Pure: one value text with its inline comment (` #...` after an unquoted value) and one pair of quotes removed. */
function envValue(raw) {
    const trimmed = raw.trim();
    const quoted = /^(["'])(.*?)\1(\s+#.*)?$/.exec(trimmed);
    if (quoted) return quoted[2];
    return trimmed.replace(/\s+#.*$/, '');
}

/** Pure: `KEY=value` lines (comments and blanks skipped, a leading `export `, a trailing inline ` # comment` and one pair of quotes removed) as an object. */
export function parseEnvText(text) {
    const pairs = text.split(/\r?\n/)
        .filter((line) => line.includes('=') && !line.trimStart().startsWith('#'))
        .map((line) => [line.slice(0, line.indexOf('=')).trim().replace(/^export\s+/, ''), envValue(line.slice(line.indexOf('=') + 1))]);
    return Object.fromEntries(pairs);
}

/** Pure: the environment the script needs, built from the parsed secrets files; `readPlayJson(path)` returns the service-account file's text. Keys that are absent are left out, so the script names the missing one. */
export function storeEnvFrom(dino, apple, readPlayJson) {
    const env = { MS_STORE_APP_ID: DINO_STORE_APP_ID };
    for (const [from, to] of Object.entries(MS_KEY_MAP)) if (dino[from]) env[to] = dino[from];
    for (const key of APPLE_KEYS) if (apple[key]) env[key] = apple[key];
    if (dino[PLAY_PATH_KEY]) env[PLAY_PATH_KEY] = readPlayJson(dino[PLAY_PATH_KEY]);
    return env;
}

function readEnvFile(root, relative) {
    const path = join(root, relative);
    if (!existsSync(path)) throw new Error(`the secrets file ${path} does not exist (set SECRETS_ROOT or clone the secrets store)`);
    return parseEnvText(readFileSync(path, 'utf8'));
}

/** Reads the secrets store into a child environment. Never logs a value. `microsoftOnly` (the read-only --check-prices) reads only the Microsoft keys file: no Apple file, no Play service-account file. */
export function loadStoreEnv(root = process.env.SECRETS_ROOT ?? DEFAULT_SECRETS_ROOT, microsoftOnly = false) {
    const dinoDir = join(root, dirname(DINO_SECRETS));
    const dino = readEnvFile(root, DINO_SECRETS);
    if (microsoftOnly) return storeEnvFrom({ ...dino, [PLAY_PATH_KEY]: undefined }, {}, () => '');
    return storeEnvFrom(dino, readEnvFile(root, APPLE_SECRETS), (path) => readFileSync(resolve(dinoDir, path), 'utf8'));
}

/** Pure: the names (never values) of the keys a run needs that the environment lacks; --apply needs all three stores, --check-prices only Microsoft. */
export function missingKeys(env, args) {
    const needed = Object.values(MS_KEY_MAP);
    if (args.includes('--apply')) needed.push(...APPLE_KEYS, PLAY_PATH_KEY);
    return needed.filter((key) => !env[key]);
}

function main() {
    const args = process.argv.slice(2);
    const env = { ...process.env, ...loadStoreEnv(undefined, args.includes('--check-prices')) };
    const missing = missingKeys(env, args);
    if (missing.length > 0) throw new Error(`these keys are missing from the secrets store: ${missing.join(', ')}`);
    const result = spawnSync(process.execPath, [SCRIPT, ...args], { env, stdio: 'inherit' });
    process.exitCode = exitCodeOf(result);
}

/** Pure: the exit code for a finished child: its own, or 1 with a message when it could not start or was killed by a signal. */
export function exitCodeOf(result) {
    if (result.error) {
        console.error(`store-products-local: could not run store-products.mjs: ${result.error.message}`);
        return 1;
    }
    if (result.signal) {
        console.error(`store-products-local: store-products.mjs was stopped by ${result.signal}`);
        return 1;
    }
    return result.status ?? 1;
}

if (isEntryScript(import.meta.url)) {
    try {
        main();
    } catch (error) {
        console.error(`store-products-local: ${error.message}`); // the message names a file or key, never a value
        process.exitCode = 1;
    }
}
