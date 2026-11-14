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

/** How many of the last non-empty lines count as the command's final error. `-v` logs every request (an Azure Blob "Status: 503" that a retry then cured), so the whole log is never searched. */
export const FINAL_LINES = 6;

/** The last few non-empty lines of the output: the final status/exit text of the failed command. */
export function finalLines(output) {
    const lines = String(output ?? '').split(/\r?\n/).filter(line => line.trim() !== '');
    return lines.slice(-FINAL_LINES).join('\n');
}

/** The one stage that may be retried: the CLI looks up the app's existing submission before it creates anything, so a 5xx here has changed nothing in the Store. */
export const RETRYABLE_STAGE = /Retrieving existing Submission/i;

/** True only when the CLI's final lines name a 5xx from the Store service (by name or by an HTTP status) raised while "Retrieving existing Submission". Every other 5xx (an upload, a commit) may have left a draft behind and fails as before. */
export function isTransientStoreError(output) {
    const text = finalLines(output);
    return RETRYABLE_STAGE.test(text) && (SERVER_ERROR_NAMES.test(text) || SERVER_ERROR_STATUS.test(text));
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    const arg = process.argv[2];
    if (arg === '--wait-seconds') { console.log(RETRY_WAIT_SECONDS); process.exit(0); }
    const retry = isTransientStoreError(arg ? readFileSync(arg, 'utf8') : '');
    console.log(retry ? 'store-submit-retry: a 5xx from the Store service, one retry is allowed.' : 'store-submit-retry: not a 5xx from the Store service, no retry.');
    process.exit(retry ? 0 : 1);
}
