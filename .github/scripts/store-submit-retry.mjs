#!/usr/bin/env node
/**
 * Slice 317 item 5: decides whether a failed `msstore publish` is worth one retry. Only a 5xx raised while "Retrieving existing Submission" is (before any submission is created); every other failure, 5xx included, fails at once. The Partner Center Ingestion API answered HTTP 500
 * `InternalServerError` on "Retrieving existing Submission" in release run 42449477730; that is the service's fault and passes on a second try,
 * while a 4xx (bad credentials, bad package, a missing listing) never does and must fail the run at once.
 *
 * Usage: node store-submit-retry.mjs <file holding the CLI output>   exit 0 = transient server error (retry), exit 1 = anything else (do not retry).
 */
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

/** Seconds to wait before the one retry (store-submit.yml reads it from this script with --wait-seconds). */
export const RETRY_WAIT_SECONDS = 60;

const SERVER_ERROR_NAMES = /\b(InternalServerError|BadGateway|ServiceUnavailable|GatewayTimeout)\b/i;
// "HTTP 500", "HTTP/1.1 503", "HTTP/2 502", "status code ...: 500": the version may carry a dot or a digit, so `\D` alone would stop inside "HTTP/2".
const SERVER_ERROR_STATUS = /\b(?:HTTP(?:\/\d(?:\.\d)?)?|status(?: code)?)\b[^\d\n]{0,60}\b5\d\d\b/i;

/** The one stage that may be retried: the CLI looks up the app's existing submission before it creates anything, so a 5xx here has changed nothing in the Store. */
export const RETRYABLE_STAGE = /Retrieving existing Submission/i;

/** Stages that come after the lookup and may leave a draft behind: when one of them follows the lookup in the log, the failure is not the lookup's. */
const LATER_STAGES = /\b(Uploading|Creating|Committing|Publishing)\b/i;

/**
 * Design changed after review check 1 (two strikes): the WHOLE captured log is searched (a fixed window of last lines lost the marker to `-v` output or a stack trace), but only the text from the
 * LAST "Retrieving existing Submission" on counts, so a 5xx an earlier request logged and then cured (an Azure Blob "Status: 503") is never read as this failure.
 */
export function lastStageText(output) {
    const text = String(output ?? '');
    const at = text.search(new RegExp(`${RETRYABLE_STAGE.source}(?![\\s\\S]*${RETRYABLE_STAGE.source})`, 'i'));
    return at < 0 ? '' : text.slice(at);
}

/** True only when the log's last "Retrieving existing Submission" is followed by a 5xx from the Store service (by name or by an HTTP status) and by no later stage. Every other 5xx (an upload, a commit) fails as before. */
export function isTransientStoreError(output) {
    const tail = lastStageText(output);
    return tail !== '' && !LATER_STAGES.test(tail) && (SERVER_ERROR_NAMES.test(tail) || SERVER_ERROR_STATUS.test(tail));
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    const arg = process.argv[2];
    if (arg === '--wait-seconds') { console.log(RETRY_WAIT_SECONDS); process.exit(0); }
    const retry = isTransientStoreError(arg ? readFileSync(arg, 'utf8') : '');
    console.log(retry ? 'store-submit-retry: a 5xx from the Store service, one retry is allowed.' : 'store-submit-retry: not a 5xx from the Store service, no retry.');
    process.exit(retry ? 0 : 1);
}
