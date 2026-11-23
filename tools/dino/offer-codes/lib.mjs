// Shared helpers for Dino Reader offer codes: the App Store Connect client and the code store in D:\secrets.
import {
    readFileSync, writeFileSync, existsSync, mkdirSync, appendFileSync, openSync, closeSync, unlinkSync, statSync,
} from 'node:fs';
import { createSign } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { join } from 'node:path';

export const SECRETS_REPO = 'D:/secrets';
export const STORE_DIR = join(SECRETS_REPO, 'dinoreader', 'offer-codes');
export const BATCHES_FILE = join(STORE_DIR, 'batches.json');
export const GIVEN_FILE = join(STORE_DIR, 'given.csv');
const GIVEN_HEADER = 'code,platform,batch,expires,given_at,note\n';
// The App Store Connect API key is shared with Lets-Call-Mom (one Apple developer account).
const ASC_ENV_FILE = join(SECRETS_REPO, 'lets-call-mom', 'secrets.env');
export const DINO_APP_ID = '6825053435';
export const DINO_YEARLY_SUBSCRIPTION_ID = '6835135180';

export const Platform = Object.freeze({ Ios: 'ios' });

export function parseArgs(argv) {
    const args = {};
    for (let i = 0; i < argv.length; i++) {
        if (!argv[i].startsWith('--')) continue;
        const next = argv[i + 1];
        const hasValue = next !== undefined && !next.startsWith('--');
        args[argv[i].slice(2)] = hasValue ? next : true;
        if (hasValue) i++;
    }
    return args;
}

function readEnv(path) {
    const out = {};
    for (const line of readFileSync(path, 'utf8').split(/\r?\n/)) {
        const m = line.match(/^\s*([A-Z0-9_]+)\s*=\s*(.*)\s*$/);
        if (m) out[m[1]] = m[2].replace(/^["']|["']$/g, '');
    }
    return out;
}

function b64url(buf) {
    return Buffer.from(buf).toString('base64').replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
}

function ascToken() {
    const env = readEnv(ASC_ENV_FILE);
    const key = Buffer.from(env.ASC_API_KEY_P8_BASE64, 'base64').toString('utf8');
    const now = Math.floor(Date.now() / 1000);
    const head = b64url(JSON.stringify({ alg: 'ES256', kid: env.ASC_API_KEY_ID, typ: 'JWT' }));
    const body = b64url(JSON.stringify({ iss: env.ASC_API_ISSUER_ID, iat: now, exp: now + 1100, aud: 'appstoreconnect-v1' }));
    const sig = createSign('SHA256').update(`${head}.${body}`).sign({ key, dsaEncoding: 'ieee-p1363' });
    return `${head}.${body}.${b64url(sig)}`;
}

/** Calls the App Store Connect API; throws with Apple's error text on a non-2xx answer. */
export async function asc(method, path, body) {
    const res = await fetch(`https://api.appstoreconnect.apple.com${path}`, {
        method,
        headers: { Authorization: `Bearer ${ascToken()}`, 'Content-Type': 'application/json' },
        body: body ? JSON.stringify(body) : undefined,
    });
    const text = await res.text();
    if (!res.ok) throw new Error(`${method} ${path} -> ${res.status}: ${text.slice(0, 600)}`);
    return text;
}

function git(...args) {
    return execFileSync('git', ['-C', SECRETS_REPO, ...args], { encoding: 'utf8' });
}

// Inside .git so the lock is never committed.
const LOCK_FILE = join(SECRETS_REPO, '.git', 'offer-codes.lock');
const STALE_LOCK_MS = 10 * 60 * 1000;

/** One run at a time, so two fetches never pick the same code. Released when the process exits. */
export function lockStore() {
    if (!existsSync(STORE_DIR)) mkdirSync(STORE_DIR, { recursive: true });
    if (existsSync(LOCK_FILE) && Date.now() - statSync(LOCK_FILE).mtimeMs > STALE_LOCK_MS) unlinkSync(LOCK_FILE);
    try {
        closeSync(openSync(LOCK_FILE, 'wx'));
    } catch {
        throw new Error(`another offer-codes run holds ${LOCK_FILE}; wait for it to finish`);
    }
    process.on('exit', () => { if (existsSync(LOCK_FILE)) unlinkSync(LOCK_FILE); });
}

export function pullStore() {
    lockStore();
    if (git('status', '--porcelain', '--', STORE_DIR).trim()) {
        throw new Error(`uncommitted changes in ${STORE_DIR}; commit or restore them (git -C ${SECRETS_REPO} status) and rerun`);
    }
    git('pull', '--rebase', '--autostash', '--quiet');
}

/** Pulls before a push retry; on a conflict, aborts the rebase so the repo is never left half-rebased. */
function rebaseOrAbort() {
    try {
        git('pull', '--rebase', '--autostash', '--quiet');
    } catch (e) {
        try { git('rebase', '--abort'); } catch { /* no rebase in progress */ }
        throw new Error(`secrets repo pull conflicted (committed locally, resolve by hand): ${e.message.split('\n')[0]}`);
    }
}

/** Commits the store locally, then pushes; returns false (with the reason on stderr) when the push fails. */
export function commitStore(message) {
    git('add', '--', STORE_DIR);
    if (!git('diff', '--cached', '--name-only', '--', STORE_DIR).trim()) return true;
    git('commit', '--quiet', '-m', message, '--', STORE_DIR);
    for (let attempt = 0; attempt < 2; attempt++) {
        try {
            git('push', '--quiet');
            return true;
        } catch (e) {
            if (attempt === 0) rebaseOrAbort();
            else console.error(`push to the secrets repo failed (committed locally, push it later): ${e.message.split('\n')[0]}`);
        }
    }
    return false;
}

export function readBatches() {
    return existsSync(BATCHES_FILE) ? JSON.parse(readFileSync(BATCHES_FILE, 'utf8')) : [];
}

export function writeBatches(batches) {
    writeFileSync(BATCHES_FILE, JSON.stringify(batches, null, 2) + '\n');
}

export function readGiven() {
    if (!existsSync(GIVEN_FILE)) return new Set();
    const lines = readFileSync(GIVEN_FILE, 'utf8').split(/\r?\n/).slice(1).filter(Boolean);
    return new Set(lines.map((l) => l.split(',')[0]));
}

export function appendGiven(rows) {
    if (!existsSync(GIVEN_FILE)) writeFileSync(GIVEN_FILE, GIVEN_HEADER);
    if (!readFileSync(GIVEN_FILE, 'utf8').endsWith('\n')) appendFileSync(GIVEN_FILE, '\n');
    const clean = (s) => String(s ?? '').replace(/[,\r\n]/g, ' ');
    appendFileSync(GIVEN_FILE, rows.map((r) => [r.code, r.platform, r.batch, r.expires, r.givenAt, clean(r.note)].join(',') + '\n').join(''));
}

/** Reads a batch file: one "code,link" line per code. */
export function readBatchCodes(batch) {
    return readFileSync(join(STORE_DIR, batch.file), 'utf8').split(/\r?\n/).filter(Boolean)
        .map((line) => { const [code, link] = line.split(','); return { code, link }; });
}

export function today() {
    return new Date().toISOString().slice(0, 10);
}

/** A batch is handed out only while it has at least this many days left, so the person has time to redeem. */
export const MIN_DAYS_LEFT = 2;

export function isLive(batch) {
    const cutoff = new Date(Date.now() + MIN_DAYS_LEFT * 86400000).toISOString().slice(0, 10);
    return batch.expires >= cutoff;
}
