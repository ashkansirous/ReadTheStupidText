#!/usr/bin/env node
/**
 * Copies the signed Android APK into the downloads R2 bucket that
 * `worker/downloads.ts` serves `/download` from (Slice 69). The Worker never
 * reads GitHub, so this is how a build reaches the landing site.
 *
 * Key layout: `worker/download-keys.mjs`, shared with the Worker.
 *   android/<version>/ReadTheStupidText-<version>.apk   the APK
 *   manifests/android/<version>.json                    that version's manifest
 *
 * Order matters: the APK first, the manifest LAST. The Worker only offers a
 * version once its manifest exists, so an upload that dies halfway is never
 * offered to anyone. "Latest" is the highest manifest version, so a rerun of
 * an older build cannot move the pointer back.
 *
 * Guarantee, exactly: a version whose manifest already exists is skipped
 * ("already published"), never overwritten. That check is not atomic, so two
 * runs publishing the same version at the same moment can both pass it; the
 * uploads are idempotent (same bytes), so the worst case is a repeated upload.
 * Because the manifest is written last, a run that dies halfway leaves a
 * stray APK that nothing links to, and the next run simply redoes it.
 *
 * Dino Reader (Slice 74) uses the same bucket under a `dinoreader/` key prefix
 * (`PRODUCT=dinoreader`; see worker/download-keys.mjs).
 *
 * Env: PLATFORM (android), VERSION, ASSET_PATHS (one path per line: the APK),
 * PRODUCT (optional: readthestupidtext, the default, or dinoreader),
 * CLOUDFLARE_API_TOKEN, CLOUDFLARE_ACCOUNT_ID. Without CLOUDFLARE_API_TOKEN it
 * logs that it skipped and exits 0: unconfigured must never look like a broken
 * build. With it set, a failed upload fails the step.
 */

import { basename, dirname, join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { createRequire } from 'node:module';
import { mkdtempSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { fileURLToPath } from 'node:url';

import { Product, apkFileName, apkKey, manifestKey } from '../../worker/download-keys.mjs';

export { Product, apkKey, manifestKey };

export const BUCKET_NAME = 'readthestupidtext-downloads';
const APK_CONTENT_TYPE = 'application/vnd.android.package-archive';
const JSON_CONTENT_TYPE = 'application/json; charset=utf-8';
const VERSION_PATTERN = /^\d+\.\d+\.\d+$/;

const Platform = { Android: 'android' };

const INSTALLER_NAME = {
    [Platform.Android]: apkFileName,
};

/** Pure: the manifest body the Worker reads. */
export function buildManifest({ version, sizeBytes, publishedAt, product = Product.ReadTheStupidText }) {
    return JSON.stringify({ version, fileName: INSTALLER_NAME[Platform.Android](version, product), sizeBytes, publishedAt });
}

/**
 * The uploads for one build, in the order they must run: the APK, then the
 * manifest (which carries `body` instead of `path`; the caller writes it to a
 * file). Throws on a name that is not this build's or a missing APK: half a
 * build is never published.
 */
export function assertAssets({ platform, version, paths, product = Product.ReadTheStupidText }) {
    if (!INSTALLER_NAME[platform]) throw new Error(`Unknown PLATFORM "${platform}".`);
    if (!Object.values(Product).includes(product)) throw new Error(`Unknown PRODUCT "${product}".`);
    if (!VERSION_PATTERN.test(version ?? '')) throw new Error(`VERSION "${version}" is not x.y.z.`);
    for (const path of paths) {
        if (basename(path) !== INSTALLER_NAME[platform](version, product)) {
            throw new Error(`${basename(path)} is not a ${platform} ${version} download (${INSTALLER_NAME[platform](version, product)}).`);
        }
    }
    if (paths.length !== 1) throw new Error(`Expected exactly one ${platform} APK among the assets (ASSET_PATHS), found ${paths.length}.`);
}

export function planUploads({ platform, version, paths, sizeBytes, publishedAt, product = Product.ReadTheStupidText }) {
    assertAssets({ platform, version, paths, product });
    return [
        { path: paths[0], key: apkKey(version, product), contentType: APK_CONTENT_TYPE },
        { body: buildManifest({ version, sizeBytes, publishedAt, product }), key: manifestKey(version, product), contentType: JSON_CONTENT_TYPE },
    ];
}

/** The wrangler arguments for one upload (after `wrangler`). */
export function wranglerArgs(upload) {
    return ['r2', 'object', 'put', `${BUCKET_NAME}/${upload.key}`, '--file', upload.path, '--remote', '--content-type', upload.contentType];
}

/** ASSET_PATHS is one path per line. */
export function assetPathsFromEnv(env) {
    return (env.ASSET_PATHS ?? '')
        .split(/\r?\n/)
        .map((line) => line.trim())
        .filter(Boolean);
}

/** Runs the uploads in order; the first failure stops the rest (the manifest is last). */
export function publishUploads(uploads, runWrangler) {
    for (const upload of uploads) {
        console.log(`Uploading ${upload.path} -> ${BUCKET_NAME}/${upload.key}`);
        runWrangler(wranglerArgs(upload));
    }
}

/** The key whose presence means this version is already published. */
export function publishedMarkerKey(uploads) {
    return uploads[uploads.length - 1].key;
}

const ABSENT_KEY_MESSAGE = /nosuchkey|no such key|(key|object) does not exist|specified key|\b404\b|\b10007\b/i;
const MISSING_BUCKET_MESSAGE = /nosuchbucket|no such bucket|bucket (does not exist|not found)|\b10006\b/i;

/**
 * Pure: what a `wrangler r2 object get --remote --pipe` of the marker key
 * said. Exit 0 means the object exists. A non-zero exit whose output names a
 * missing key (404, NoSuchKey, "does not exist", any case) means it is not
 * published yet. Anything else, including a missing bucket, auth or network
 * trouble, throws with the captured output, so an unreadable bucket is never
 * mistaken for an empty one.
 */
export function publishedStateFrom({ status, output }) {
    if (status === 0) return true;
    if (!MISSING_BUCKET_MESSAGE.test(output) && ABSENT_KEY_MESSAGE.test(output)) return false;
    throw new Error(`Could not tell whether this version is already published (wrangler exited ${status}): ${output.trim().slice(-300)}`);
}

/** The wrangler arguments that read the marker key to stdout (after `wrangler`). */
export function markerCheckArgs(uploads) {
    return ['r2', 'object', 'get', `${BUCKET_NAME}/${publishedMarkerKey(uploads)}`, '--remote', '--pipe'];
}

/** wrangler's real entry point, run with node itself so no shell ever re-parses an argument (`application/json; charset=utf-8`). */
let wranglerBinPath;
function wranglerBin() {
    if (wranglerBinPath) return wranglerBinPath;
    // The package's "exports" hides bin/, so find the package.json and read its "bin" entry.
    const manifestPath = createRequire(import.meta.url).resolve('wrangler/package.json');
    const { bin } = JSON.parse(readFileSync(manifestPath, 'utf8'));
    wranglerBinPath = join(dirname(manifestPath), bin.wrangler);
    return wranglerBinPath;
}

/** The command line that runs wrangler with `args`: node, wrangler's script, then each argument as one element. */
export function wranglerCommand(args, nodePath = process.execPath, binPath = wranglerBin()) {
    return { command: nodePath, args: [binPath, ...args] };
}

function runWranglerCli(args) {
    const { command, args: commandArgs } = wranglerCommand(args);
    const result = spawnSync(command, commandArgs, { stdio: 'inherit', env: process.env });
    if (result.error) throw result.error;
    if (result.status !== 0) throw new Error(`wrangler ${args.slice(0, 4).join(' ')} exited with status ${result.status}.`);
}

function isAlreadyPublished(uploads) {
    const { command, args } = wranglerCommand(markerCheckArgs(uploads));
    const result = spawnSync(command, args, { encoding: 'utf8', env: process.env, maxBuffer: 1024 * 1024 });
    if (result.error) throw result.error;
    return publishedStateFrom({ status: result.status, output: `${result.stdout}${result.stderr}` });
}

/** Gives each generated upload (the manifest) a file to send. */
function materialize(uploads, scratch) {
    return uploads.map((upload) => {
        if (upload.path) return upload;
        const path = join(scratch, 'manifest.json');
        writeFileSync(path, upload.body);
        return { ...upload, path };
    });
}

function main() {
    if (!process.env.CLOUDFLARE_API_TOKEN) {
        console.log('::warning::skipped: CLOUDFLARE_API_TOKEN not set, so this build was not copied to the downloads bucket.');
        return;
    }
    if (!process.env.CLOUDFLARE_ACCOUNT_ID) {
        throw new Error('CLOUDFLARE_ACCOUNT_ID is required when CLOUDFLARE_API_TOKEN is set.');
    }
    const paths = assetPathsFromEnv(process.env);
    const build = { platform: process.env.PLATFORM, version: process.env.VERSION, product: process.env.PRODUCT || Product.ReadTheStupidText };
    assertAssets({ ...build, paths });
    const uploads = planUploads({
        ...build,
        paths,
        sizeBytes: statSync(paths[0]).size,
        publishedAt: new Date().toISOString(),
    });
    if (isAlreadyPublished(uploads)) {
        console.log(`already published ${process.env.VERSION}, not overwriting`);
        return;
    }
    const scratch = mkdtempSync(join(tmpdir(), 'publish-downloads-'));
    try {
        publishUploads(materialize(uploads, scratch), runWranglerCli);
    } finally {
        rmSync(scratch, { recursive: true, force: true });
    }
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
    try {
        main();
    } catch (error) {
        console.error(`::error::${error instanceof Error ? error.message : String(error)}`);
        process.exitCode = 1;
    }
}
