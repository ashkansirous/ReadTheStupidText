#!/usr/bin/env node
/**
 * Confirms App Store Connect received an uploaded build (Slice 354 item 1).
 *
 * Usage: asc-confirm-build.mjs <rtst|dino> <marketing-version> <build-number>
 * The build number is the uploaded ipa's CFBundleVersion: matching the version alone would pass a re-run on an earlier upload of
 * the same version.
 * Needs ASC_API_KEY_P8_BASE64, ASC_API_KEY_ID, ASC_API_ISSUER_ID (the same secrets the upload step uses).
 *
 * altool can log a 500 on "CHANGE UPLOAD STATE TO COMPLETE", retry and still say "UPLOAD SUCCEEDED"; it can also print that
 * line for an upload Apple never finalised. This asks the App Store Connect API (the client of ios-review-cadence.mjs) for the
 * app's builds filtered by the build number until one appears; an API error or empty answer is logged and asked again, and
 * only the wait ending with no match fails. The marketing version is not matched: it comes from an include that a build still
 * processing may lack, and the build number is unique per app upload.
 */
import { fileURLToPath } from 'node:url';

import { APPS, ascToken, createAscClient } from './ios-review-cadence.mjs';

export const WAIT_SECONDS = 600;
export const POLL_SECONDS = 30;

/**
 * The app's builds with this build number. A build's `version` attribute is its CFBundleVersion, so the filter finds the
 * upload in any processing state without the preReleaseVersion include (absent while a build is still processing).
 * Throws on an API error; the poll loop logs it and asks again.
 */
export async function findBuilds(asc, appId, buildNumber) {
    const query = new URLSearchParams({ 'filter[app]': appId, 'filter[version]': String(buildNumber), limit: '10' });
    const response = await asc.get(`/v1/builds?${query}`);
    if (!response.ok) throw new Error(`GET /v1/builds failed (${response.status})`);
    return (response.json?.data ?? []).map((build) => ({ buildNumber: build.attributes?.version, processingState: build.attributes?.processingState }));
}

function pause(seconds) {
    return new Promise((resolve) => setTimeout(resolve, seconds * 1000));
}

/** One poll: the build, or null when none matches yet or the API failed (the failure is logged, never thrown). */
async function pollOnce(list) {
    try {
        const found = await list();
        return found.length > 0 ? found[0] : null;
    } catch (error) {
        console.log(`App Store Connect poll failed, will ask again: ${error.message}`);
        return null;
    }
}

/** Polls `list()` until a build with the number shows or `waitSeconds` pass. Returns the build or null; an API error never ends the wait. */
export async function waitForBuild({ list, waitSeconds = WAIT_SECONDS, pollSeconds = POLL_SECONDS, sleep = pause }) {
    for (let waited = 0; ; waited += pollSeconds) {
        const found = await pollOnce(list);
        if (found) return found;
        if (waited + pollSeconds > waitSeconds) return null;
        await sleep(pollSeconds);
    }
}

function requireEnv(name) {
    const value = process.env[name];
    if (!value) throw new Error(`${name} is not set.`);
    return value;
}

async function main() {
    const [key, version, buildNumber] = process.argv.slice(2);
    const app = APPS[key];
    if (!app || !version || !buildNumber) throw new Error(`Usage: asc-confirm-build.mjs <${Object.keys(APPS).join('|')}> <marketing-version> <build-number>`);
    const privateKeyPem = Buffer.from(requireEnv('ASC_API_KEY_P8_BASE64'), 'base64').toString('utf8');
    const keyId = requireEnv('ASC_API_KEY_ID');
    const issuerId = requireEnv('ASC_API_ISSUER_ID');
    const asc = createAscClient({ token: () => ascToken({ keyId, issuerId, privateKeyPem, nowSeconds: Math.floor(Date.now() / 1000) }) });
    const build = await waitForBuild({ list: () => findBuilds(asc, app.id, buildNumber) });
    if (!build) throw new Error(`${app.name} ${version} (build ${buildNumber}) did not appear in App Store Connect within ${WAIT_SECONDS} s: the upload was not received.`);
    console.log(`App Store Connect has ${app.name} ${version} (build ${build.buildNumber}, ${build.processingState}).`);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    main().catch((error) => {
        console.error(`::error::${error.message}`);
        process.exitCode = 1;
    });
}
