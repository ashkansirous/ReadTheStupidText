#!/usr/bin/env node
/**
 * Sends iOS builds to Apple's reviews on a fixed cadence (Slice 147, Decision 154).
 *
 * Rules, the same for every app: never cancel a waiting review, never expire a
 * build to make room, always the newest processed build. TestFlight submits the
 * newest build each morning, even while an older review waits; the App Store path
 * keeps one review waiting at a time. `testflight` adds that build to the external group and submits
 * it for Beta App Review with auto-notify on, and tells testers about the newest approved build
 * (run daily); `appstore` attaches it to the editable App
 * Store version and submits that for review (run by hand).
 *
 *   node ios-review-cadence.mjs testflight rtst dino
 *   node ios-review-cadence.mjs appstore rtst
 *
 * ASC client and JWT adapted from Lets-Call-Mom's testflight-distribute.mjs.
 */

import { createSign } from 'node:crypto';
import { appendFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

/** App Store Connect app records. Not secrets. */
export const APPS = {
    rtst: { name: 'Read The Stupid Text', id: '6825052929' },
    dino: { name: 'Dino Reader', id: '6825053435' },
};
/** The external group whose testers get each reviewed build. Renaming it in App Store Connect breaks this. */
export const EXTERNAL_GROUP_NAME = 'Rofagha';

const API_BASE = 'https://api.appstoreconnect.apple.com';
const TOKEN_AUDIENCE = 'appstoreconnect-v1';
/** Apple rejects tokens that live longer than 20 minutes. */
const TOKEN_LIFETIME_SECONDS = 15 * 60;
const REQUEST_TIMEOUT_MS = 30_000;
const PAGE_SIZE = 200;
const BUILD_FILTER_CHUNK = 50;
const PLATFORM_IOS = 'IOS';
const RELEASE_AFTER_APPROVAL = 'AFTER_APPROVAL';

const Method = { Get: 'GET', Post: 'POST', Patch: 'PATCH' };
const Resource = {
    Apps: 'apps',
    Builds: 'builds',
    AppStoreVersions: 'appStoreVersions',
    ReviewSubmissions: 'reviewSubmissions',
    ReviewSubmissionItems: 'reviewSubmissionItems',
    BetaAppReviewSubmissions: 'betaAppReviewSubmissions',
    BuildBetaDetails: 'buildBetaDetails',
    BuildBetaNotifications: 'buildBetaNotifications',
};
const PRE_RELEASE_TYPE = 'preReleaseVersions';
/** A build's `externalBuildState` in App Store Connect (what its external testers can see). */
export const ExternalState = {
    Approved: 'BETA_APPROVED',
    InTesting: 'IN_BETA_TESTING',
    Waiting: 'WAITING_FOR_BETA_REVIEW',
    InReview: 'IN_BETA_REVIEW',
};
/** States in which the build has passed Beta App Review. */
const RELEASED_STATES = [ExternalState.Approved, ExternalState.InTesting];
/** States in which a build is queued for Beta App Review. */
const PENDING_STATES = [ExternalState.Waiting, ExternalState.InReview];
const Action = { Skip: 'skip', Submit: 'submit', Fail: 'fail' };
const BETA_REJECTED = 'REJECTED';

/** Transient GET failures are retried this many times, waiting baseDelayMs * attempt between. Tests set the delay to 0. */
export const retryPolicy = { attempts: 3, baseDelayMs: 2000 };
const HTTP_TOO_MANY_REQUESTS = 429;
const HTTP_SERVER_ERROR = 500;

export const Mode = { TestFlight: 'testflight', AppStore: 'appstore' };
export const Outcome = { Submitted: 'submitted', Notified: 'notified', Skipped: 'skipped', Failed: 'failed' };

export const ProcessingState = { Valid: 'VALID' };
/** An App Store review in one of these states holds the app's single review slot. */
export const STORE_BUSY_STATES = ['WAITING_FOR_REVIEW', 'IN_REVIEW', 'UNRESOLVED_ISSUES', 'CANCELING', 'COMPLETING'];
const STORE_DRAFT_STATE = 'READY_FOR_REVIEW';
/** App Store version states in which the version (and its build) can still be changed. */
export const EDITABLE_VERSION_STATES = ['PREPARE_FOR_SUBMISSION', 'DEVELOPER_REJECTED', 'REJECTED', 'METADATA_REJECTED', 'INVALID_BINARY'];
/** App Store version states that mean its build was submitted, approved or is live. */
export const SUBMITTED_VERSION_STATES = [
    'WAITING_FOR_REVIEW', 'IN_REVIEW', 'PENDING_APPLE_RELEASE', 'PENDING_DEVELOPER_RELEASE',
    'PROCESSING_FOR_APP_STORE', 'PROCESSING_FOR_DISTRIBUTION', 'READY_FOR_DISTRIBUTION', 'READY_FOR_SALE', 'ACCEPTED',
];

function jwtSegment(object) {
    return Buffer.from(JSON.stringify(object)).toString('base64url');
}

/** An ES256 JWT for the App Store Connect API, signed with the API key. */
export function ascToken({ keyId, issuerId, privateKeyPem, nowSeconds }) {
    const header = jwtSegment({ alg: 'ES256', kid: keyId, typ: 'JWT' });
    const claims = jwtSegment({ iss: issuerId, iat: nowSeconds, exp: nowSeconds + TOKEN_LIFETIME_SECONDS, aud: TOKEN_AUDIENCE });
    const signer = createSign('SHA256');
    signer.update(`${header}.${claims}`);
    const signature = signer.sign({ key: privateKeyPem, dsaEncoding: 'ieee-p1363' });
    return `${header}.${claims}.${signature.toString('base64url')}`;
}

/** An error body Apple's gateway sent as HTML must not hide the status behind a SyntaxError. */
function parseJson(text) {
    if (!text) return null;
    try {
        return JSON.parse(text);
    } catch {
        return { errors: [{ detail: text.slice(0, 200) }] };
    }
}

/** Minimal JSON client. A fresh token per request. */
export function createAscClient({ token, fetchImpl = fetch }) {
    async function request(method, path, body) {
        const response = await fetchImpl(`${API_BASE}${path}`, {
            method,
            headers: { authorization: `Bearer ${token()}`, 'content-type': 'application/json' },
            body: body === undefined ? undefined : JSON.stringify(body),
            signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
        });
        const json = parseJson(await response.text());
        return { status: response.status, ok: response.ok, json };
    }
    return {
        get: (path) => request(Method.Get, path),
        post: (path, body) => request(Method.Post, path, body),
        patch: (path, body) => request(Method.Patch, path, body),
    };
}

function describeErrors(json) {
    const errors = json?.errors ?? [];
    return errors.map((error) => error.detail ?? error.title ?? error.code).join('; ') || 'no detail';
}

function failure(method, path, response) {
    const error = new Error(`${method} ${path} failed (${response.status}): ${describeErrors(response.json)}`);
    error.status = response.status;
    return error;
}

async function getOnce(asc, path) {
    const response = await asc.get(path);
    if (!response.ok) throw failure(Method.Get, path, response);
    return response.json;
}

function pause(milliseconds) {
    return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

/** GETs are safe to repeat, so a transient failure is retried a few times; writes never are. */
async function getOrThrow(asc, path) {
    for (let attempt = 1; ; attempt++) {
        try {
            return await getOnce(asc, path);
        } catch (error) {
            if (attempt >= retryPolicy.attempts || !isTransient(error)) throw error;
            await pause(retryPolicy.baseDelayMs * attempt);
        }
    }
}

async function writeOrThrow(asc, method, path, body) {
    const response = await asc[method.toLowerCase()](path, body);
    if (!response.ok) throw failure(method, path, response);
    return response.json;
}

/** A network error, a 5xx or a 429 may clear on its own; a 401/403/400 never will. */
export function isTransient(error) {
    if (error.status === undefined) return true;
    return error.status >= HTTP_SERVER_ERROR || error.status === HTTP_TOO_MANY_REQUESTS;
}

// ---------------------------------------------------------------- pure decisions

/** The newest processed, unexpired build. `builds` is newest-first, as the API returns it. */
export function pickNewestBuild(builds) {
    return builds.find((build) => build.processingState === ProcessingState.Valid && !build.expired) ?? null;
}

export function isStoreReviewBusy(submissions) {
    return submissions.some((submission) => STORE_BUSY_STATES.includes(submission.state));
}

function skip(reason) {
    return { action: Action.Skip, reason };
}

function fail(reason) {
    return { action: Action.Fail, reason };
}

export function describeBuild(build) {
    return `${build.marketingVersion ?? 'unknown version'} (${build.buildNumber})`;
}

/** What to do for TestFlight external testers: the newest build is submitted even while an older review waits. `submissions` are `{buildId, state}` of recent builds. */
export function decideTestFlight({ builds, submissions, groupId }) {
    const build = pickNewestBuild(builds);
    if (!build) return skip('no processed build');
    const own = submissions.find((submission) => submission.buildId === build.id);
    if (own?.state === BETA_REJECTED) {
        return fail(`newest build ${describeBuild(build)} was rejected by Beta App Review; read Apple's reason in App Store Connect`);
    }
    if (own) return skip(`newest build ${describeBuild(build)} already submitted (${own.state})`);
    return { action: Action.Submit, build, addToGroup: !build.groupIds.includes(groupId) };
}

/** The newest approved build, only if testers have not been told yet (BETA_APPROVED); older unsent ones are superseded. */
export function decideNotify(builds) {
    const newest = builds.find((build) => build.processingState === ProcessingState.Valid && !build.expired && RELEASED_STATES.includes(build.externalState));
    return newest?.externalState === ExternalState.Approved ? newest : null;
}

/** Builds queued for Beta App Review whose testers would not be told at approval. */
export function findAutoNotifyTargets(builds) {
    return builds.filter((build) => PENDING_STATES.includes(build.externalState) && build.betaDetailId && !build.autoNotify);
}

/** What to do for the App Store. `versions` are `{id, versionString, state, buildId}`. */
export function decideAppStore({ builds, versions, submissions }) {
    const build = pickNewestBuild(builds);
    if (!build) return skip('no processed build');
    if (isStoreReviewBusy(submissions)) return skip('a review is already waiting');
    if (!build.marketingVersion) return fail(`newest build ${describeBuild(build)} has no marketing version in App Store Connect`);
    const submitted = versions.filter((version) => SUBMITTED_VERSION_STATES.includes(version.state));
    if (submitted.some((version) => version.buildId === build.id)) {
        return skip(`newest build ${describeBuild(build)} is already submitted or live`);
    }
    if (submitted.some((version) => version.versionString === build.marketingVersion)) {
        return skip(`version ${build.marketingVersion} is already live or submitted: needs a new marketing version`);
    }
    const editable = versions.find((version) => EDITABLE_VERSION_STATES.includes(version.state));
    return { action: Action.Submit, build, version: editable ?? null, create: !editable };
}

/** A draft review submission left over from an earlier attempt, reused instead of creating another. */
export function findDraftSubmission(submissions) {
    return submissions.find((submission) => submission.state === STORE_DRAFT_STATE) ?? null;
}

// ---------------------------------------------------------------- reading Apple's data

function includedIndex(json) {
    const index = new Map();
    for (const item of json.included ?? []) index.set(`${item.type}:${item.id}`, item);
    return index;
}

/** Turns one `/v1/builds` entry plus its included resources into the shape the decisions use. */
export function normalizeBuild(build, index) {
    const preId = build.relationships?.preReleaseVersion?.data?.id;
    const pre = preId ? index.get(`${PRE_RELEASE_TYPE}:${preId}`) : null;
    return {
        id: build.id,
        buildNumber: build.attributes?.version,
        marketingVersion: pre?.attributes?.version ?? null,
        processingState: build.attributes?.processingState,
        expired: Boolean(build.attributes?.expired),
        groupIds: (build.relationships?.betaGroups?.data ?? []).map((group) => group.id),
        ...normalizeBetaDetail(build, index),
    };
}

/** The build's TestFlight detail: its id, whether testers are told on approval, and its external state. */
function normalizeBetaDetail(build, index) {
    const detailId = build.relationships?.buildBetaDetail?.data?.id ?? null;
    const detail = detailId ? index.get(`${Resource.BuildBetaDetails}:${detailId}`) : null;
    return {
        betaDetailId: detailId,
        autoNotify: detail?.attributes?.autoNotifyEnabled === true,
        externalState: detail?.attributes?.externalBuildState ?? null,
    };
}

export async function listBuilds(asc, appId) {
    const query = new URLSearchParams({
        'filter[app]': appId,
        sort: '-uploadedDate',
        limit: String(PAGE_SIZE),
        include: 'preReleaseVersion,buildBetaDetail,betaGroups,betaAppReviewSubmission',
    });
    const json = await getOrThrow(asc, `/v1/builds?${query}`);
    const index = includedIndex(json);
    return (json.data ?? []).map((build) => normalizeBuild(build, index));
}

/** Beta review submissions need `filter[build]`; there is no per-app filter. */
export async function listBetaSubmissions(asc, builds) {
    const ids = builds.map((build) => build.id);
    const found = [];
    for (let start = 0; start < ids.length; start += BUILD_FILTER_CHUNK) {
        found.push(...await listBetaSubmissionsChunk(asc, ids.slice(start, start + BUILD_FILTER_CHUNK)));
    }
    return found;
}

/** `include=build` makes Apple send relationships.build.data; without it only links come back. */
async function listBetaSubmissionsChunk(asc, buildIds) {
    const query = new URLSearchParams({ 'filter[build]': buildIds.join(','), include: 'build', limit: String(PAGE_SIZE) });
    const json = await getOrThrow(asc, `/v1/betaAppReviewSubmissions?${query}`);
    return (json.data ?? []).map((submission) => ({
        buildId: submission.relationships?.build?.data?.id,
        state: submission.attributes?.betaReviewState,
    }));
}

export async function findGroupId(asc, appId, groupName) {
    const json = await getOrThrow(asc, `/v1/apps/${appId}/betaGroups?limit=${PAGE_SIZE}`);
    const group = (json.data ?? []).find((candidate) => candidate.attributes?.name === groupName);
    if (!group) throw new Error(`No TestFlight group named "${groupName}" on app ${appId}.`);
    return group.id;
}

export async function listStoreSubmissions(asc, appId) {
    const query = new URLSearchParams({ 'filter[app]': appId, 'filter[platform]': PLATFORM_IOS, limit: String(PAGE_SIZE) });
    const json = await getOrThrow(asc, `/v1/reviewSubmissions?${query}`);
    return (json.data ?? []).map((submission) => ({ id: submission.id, state: submission.attributes?.state }));
}

export async function listVersions(asc, appId) {
    const query = new URLSearchParams({ 'filter[platform]': PLATFORM_IOS, include: 'build', limit: String(PAGE_SIZE) });
    const json = await getOrThrow(asc, `/v1/apps/${appId}/appStoreVersions?${query}`);
    return (json.data ?? []).map((version) => ({
        id: version.id,
        versionString: version.attributes?.versionString,
        state: version.attributes?.appVersionState,
        buildId: version.relationships?.build?.data?.id ?? null,
    }));
}

// ---------------------------------------------------------------- acting

function buildRef(id) {
    return { data: { type: Resource.Builds, id } };
}

/** Makes Apple send the build to testers when Beta App Review approves it. A build with no detail yet is left alone. */
export async function ensureAutoNotify(asc, build) {
    if (build.autoNotify || !build.betaDetailId) return false;
    const data = { type: Resource.BuildBetaDetails, id: build.betaDetailId, attributes: { autoNotifyEnabled: true } };
    await writeOrThrow(asc, Method.Patch, `/v1/buildBetaDetails/${build.betaDetailId}`, { data });
    return true;
}

/** Tells the testers about an approved build (the "Notify Testers" button). */
export async function notifyTesters(asc, build) {
    const relationships = { build: buildRef(build.id) };
    await writeOrThrow(asc, Method.Post, '/v1/buildBetaNotifications', { data: { type: Resource.BuildBetaNotifications, relationships } });
    return { outcome: Outcome.Notified, message: `notified testers of ${describeBuild(build)}` };
}

/** Releases the newest approved build to testers. */
async function releaseApproved(asc, builds) {
    const approved = decideNotify(builds);
    if (!approved) return null;
    return notifyTesters(asc, approved);
}

/** Switches auto-notify on for one waiting build; a refusal is returned, not thrown, so the other builds still get theirs. */
async function enableOne(asc, target) {
    try {
        await ensureAutoNotify(asc, target);
        return { build: target, error: null };
    } catch (error) {
        return { build: target, error: error.message };
    }
}

/** Switches auto-notify on for builds still waiting in Beta App Review, reporting every success and every failure. */
async function enableWaitingAutoNotify(asc, builds) {
    const targets = findAutoNotifyTargets(builds);
    if (targets.length === 0) return null;
    const attempts = [];
    for (const target of targets) attempts.push(await enableOne(asc, target));
    const failed = attempts.filter((item) => item.error);
    const enabled = attempts.filter((item) => !item.error).map((item) => describeBuild(item.build));
    const parts = [];
    if (enabled.length > 0) parts.push(`auto-notify on for ${enabled.join(', ')}`);
    for (const item of failed) parts.push(`auto-notify failed for ${describeBuild(item.build)}: ${item.error}`);
    return { outcome: failed.length > 0 ? Outcome.Failed : Outcome.Notified, message: parts.join('; ') };
}

/** Reads the build's TestFlight detail when the build list did not carry it. Null when Apple has none. */
async function fetchBetaDetail(asc, build) {
    const json = await getOrThrow(asc, `/v1/builds/${build.id}/buildBetaDetail`);
    const data = json?.data;
    if (!data?.id) return null;
    return { betaDetailId: data.id, autoNotify: data.attributes?.autoNotifyEnabled === true, externalState: data.attributes?.externalBuildState ?? null };
}

/** The build with its beta detail filled in; throws when Apple has none, so a build is never submitted with testers left unnotified. */
async function withBetaDetail(asc, build) {
    if (build.betaDetailId) return build;
    const detail = await fetchBetaDetail(asc, build);
    if (!detail) throw new Error(`no beta detail for ${describeBuild(build)}; not submitted because testers would not be told when it is approved`);
    return { ...build, ...detail };
}

/** A step that throws becomes a failed result, so the steps after it still run. */
async function attempt(step) {
    try {
        return await step();
    } catch (error) {
        return { outcome: Outcome.Failed, message: error.message };
    }
}

function overallOutcome(acted) {
    if (acted.some((result) => result.outcome === Outcome.Failed)) return Outcome.Failed;
    if (acted.some((result) => result.outcome === Outcome.Submitted)) return Outcome.Submitted;
    return Outcome.Notified;
}

/** One result for the app: failed if any step failed, submitted if a submission happened, notified if only testers were told, else skipped. Skip reasons stay in the message when another step acted. */
export function combineResults(results) {
    const present = results.filter(Boolean);
    const acted = present.filter((result) => result.outcome !== Outcome.Skipped);
    if (acted.length === 0) return present[0] ?? { outcome: Outcome.Skipped, message: 'nothing to do' };
    const skipped = present.filter((result) => result.outcome === Outcome.Skipped);
    return { outcome: overallOutcome(acted), message: [...acted, ...skipped].map((result) => result.message).join('; ') };
}

/** Adds to the group if needed, then submits. Any refusal, a 409 included, fails with Apple's text so it is visible. */
export async function submitToTestFlight(asc, groupId, decision) {
    const { build, addToGroup } = decision;
    await ensureAutoNotify(asc, await withBetaDetail(asc, build));
    if (addToGroup) {
        await writeOrThrow(asc, Method.Post, `/v1/betaGroups/${groupId}/relationships/builds`, { data: [{ type: Resource.Builds, id: build.id }] });
    }
    const body = { data: { type: Resource.BetaAppReviewSubmissions, relationships: { build: buildRef(build.id) } } };
    const response = await asc.post('/v1/betaAppReviewSubmissions', body);
    if (response.ok) return { outcome: Outcome.Submitted, message: `${describeBuild(build)} submitted for Beta App Review` };
    throw failure(Method.Post, '/v1/betaAppReviewSubmissions', response);
}

/** A decision that does not submit is either a benign skip or a failure someone must see. */
function outcomeOfStop(decision) {
    const outcome = decision.action === Action.Fail ? Outcome.Failed : Outcome.Skipped;
    return { outcome, message: decision.reason };
}

export async function runTestFlight(asc, app) {
    const builds = await listBuilds(asc, app.id);
    const groupId = await findGroupId(asc, app.id, EXTERNAL_GROUP_NAME);
    const submissions = await listBetaSubmissions(asc, builds);
    const notified = await attempt(() => releaseApproved(asc, builds));
    const waiting = await attempt(() => enableWaitingAutoNotify(asc, builds));
    const decision = decideTestFlight({ builds, submissions, groupId });
    const submitted = await attempt(() => (decision.action === Action.Submit ? submitToTestFlight(asc, groupId, decision) : outcomeOfStop(decision)));
    return combineResults([notified, waiting, submitted]);
}

async function createVersion(asc, app, build) {
    const attributes = { platform: PLATFORM_IOS, versionString: build.marketingVersion, releaseType: RELEASE_AFTER_APPROVAL };
    const body = { data: { type: Resource.AppStoreVersions, attributes, relationships: { app: { data: { type: Resource.Apps, id: app.id } } } } };
    const json = await writeOrThrow(asc, Method.Post, '/v1/appStoreVersions', body);
    return json.data.id;
}

/** The editable version carries the build's marketing version, or a new version is created. Returns its id. */
export async function prepareVersion(asc, app, decision) {
    const { build, version, create } = decision;
    if (create) return createVersion(asc, app, build);
    if (version.versionString !== build.marketingVersion) {
        const data = { type: Resource.AppStoreVersions, id: version.id, attributes: { versionString: build.marketingVersion } };
        await writeOrThrow(asc, Method.Patch, `/v1/appStoreVersions/${version.id}`, { data });
    }
    return version.id;
}

/** Reuses a draft left by an earlier attempt, else creates one. `existing` tells the caller the draft may hold items already. */
async function ensureDraftSubmission(asc, app, submissions) {
    const draft = findDraftSubmission(submissions);
    if (draft) return { id: draft.id, existing: true };
    const relationships = { app: { data: { type: Resource.Apps, id: app.id } } };
    const body = { data: { type: Resource.ReviewSubmissions, attributes: { platform: PLATFORM_IOS }, relationships } };
    const json = await writeOrThrow(asc, Method.Post, '/v1/reviewSubmissions', body);
    return { id: json.data.id, existing: false };
}

async function draftHoldsVersion(asc, submissionId, versionId) {
    const json = await getOrThrow(asc, `/v1/reviewSubmissions/${submissionId}/items?include=appStoreVersion&limit=${PAGE_SIZE}`);
    return (json.data ?? []).some((item) => item.relationships?.appStoreVersion?.data?.id === versionId);
}

async function addVersionToSubmission(asc, submissionId, versionId) {
    const item = {
        data: {
            type: Resource.ReviewSubmissionItems,
            relationships: {
                reviewSubmission: { data: { type: Resource.ReviewSubmissions, id: submissionId } },
                appStoreVersion: { data: { type: Resource.AppStoreVersions, id: versionId } },
            },
        },
    };
    await writeOrThrow(asc, Method.Post, '/v1/reviewSubmissionItems', item);
}

export async function submitToAppStore(asc, app, decision, submissions) {
    const versionId = await prepareVersion(asc, app, decision);
    await writeOrThrow(asc, Method.Patch, `/v1/appStoreVersions/${versionId}/relationships/build`, buildRef(decision.build.id));
    const { id: submissionId, existing } = await ensureDraftSubmission(asc, app, submissions);
    if (!existing || !(await draftHoldsVersion(asc, submissionId, versionId))) {
        await addVersionToSubmission(asc, submissionId, versionId);
    }
    const submit = { data: { type: Resource.ReviewSubmissions, id: submissionId, attributes: { submitted: true } } };
    await writeOrThrow(asc, Method.Patch, `/v1/reviewSubmissions/${submissionId}`, submit);
    return { outcome: Outcome.Submitted, message: `${describeBuild(decision.build)} submitted for App Store review` };
}

export async function runAppStore(asc, app) {
    const builds = await listBuilds(asc, app.id);
    const versions = await listVersions(asc, app.id);
    const submissions = await listStoreSubmissions(asc, app.id);
    const decision = decideAppStore({ builds, versions, submissions });
    if (decision.action !== Action.Submit) return outcomeOfStop(decision);
    return submitToAppStore(asc, app, decision, submissions);
}

// ---------------------------------------------------------------- entry point

const RUNNERS = { [Mode.TestFlight]: runTestFlight, [Mode.AppStore]: runAppStore };

/** One app never stops the next: a thrown error becomes a failed result. */
export async function runApp(asc, mode, key) {
    const app = APPS[key];
    if (!app) return { outcome: Outcome.Failed, message: `unknown app "${key}"` };
    try {
        return await RUNNERS[mode](asc, app);
    } catch (error) {
        return { outcome: Outcome.Failed, message: error.message };
    }
}

export function summaryLine(key, result) {
    const name = APPS[key]?.name ?? key;
    return `- ${name}: ${result.outcome}, ${result.message}`;
}

function requireEnv(name) {
    const value = process.env[name];
    if (!value) throw new Error(`${name} is not set.`);
    return value;
}

function createClientFromEnv() {
    const privateKeyPem = Buffer.from(requireEnv('ASC_API_KEY_P8_BASE64'), 'base64').toString('utf8');
    const keyId = requireEnv('ASC_API_KEY_ID');
    const issuerId = requireEnv('ASC_API_ISSUER_ID');
    const token = () => ascToken({ keyId, issuerId, privateKeyPem, nowSeconds: Math.floor(Date.now() / 1000) });
    return createAscClient({ token });
}

async function main() {
    const [mode, ...keys] = process.argv.slice(2);
    if (!RUNNERS[mode] || keys.length === 0) throw new Error(`Usage: ios-review-cadence.mjs <${Object.values(Mode).join('|')}> <app...>`);
    const asc = createClientFromEnv();
    const lines = [];
    let failed = false;
    for (const key of keys) {
        const result = await runApp(asc, mode, key);
        failed ||= result.outcome === Outcome.Failed;
        lines.push(summaryLine(key, result));
    }
    console.log(lines.join('\n'));
    if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, `### iOS ${mode} review\n${lines.join('\n')}\n`);
    if (failed) process.exitCode = 1;
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    main().catch((error) => {
        console.error(error.message);
        process.exitCode = 1;
    });
}
