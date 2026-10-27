/**
 * Shared wrangler helpers for the scripts that write to the downloads R2 bucket
 * (publish-store-books.mjs): the bucket name, the `put` arguments, the
 * node-run wrangler command (no shell re-parses an argument such as
 * `application/json; charset=utf-8`) and the reading of a `get` result.
 * (Moved out of the APK publisher, which was removed when /download became a redirect.)
 */

import { dirname, join } from 'node:path';
import { createRequire } from 'node:module';
import { readFileSync } from 'node:fs';

export const BUCKET_NAME = 'readthestupidtext-downloads';

/** The wrangler arguments for one upload (after `wrangler`). */
export function wranglerArgs(upload) {
    return ['r2', 'object', 'put', `${BUCKET_NAME}/${upload.key}`, '--file', upload.path, '--remote', '--content-type', upload.contentType];
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
