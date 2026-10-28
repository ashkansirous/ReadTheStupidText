import test from 'node:test';
import assert from 'node:assert/strict';
import { createVerify, generateKeyPairSync } from 'node:crypto';

import {
    APPS,
    EXTERNAL_GROUP_NAME,
    Mode,
    Outcome,
    ascToken,
    createAscClient,
    decideAppStore,
    decideTestFlight,
    isStoreReviewBusy,
    isTransient,
    listBetaSubmissions,
    pickNewestBuild,
    retryPolicy,
    runApp,
    summaryLine,
} from './ios-review-cadence.mjs';

const build = (id, over = {}) => ({
    id, buildNumber: `n${id}`, marketingVersion: '1.54.1', processingState: 'VALID', expired: false, groupIds: [], ...over,
});

test('pickNewestBuild skips unprocessed and expired builds', () => {
    const builds = [build('a', { processingState: 'PROCESSING' }), build('b', { expired: true }), build('c'), build('d')];
    assert.equal(pickNewestBuild(builds).id, 'c');
    assert.equal(pickNewestBuild([build('x', { processingState: 'FAILED' })]), null);
});

test('store review busy states', () => {
    assert.equal(isStoreReviewBusy([{ state: 'UNRESOLVED_ISSUES' }]), true);
    assert.equal(isStoreReviewBusy([{ state: 'READY_FOR_REVIEW' }, { state: 'COMPLETE' }]), false);
});

test('testflight: submits the newest build and adds it to the group when missing', () => {
    const decision = decideTestFlight({ builds: [build('a'), build('b')], submissions: [], groupId: 'g' });
    assert.equal(decision.action, 'submit');
    assert.equal(decision.build.id, 'a');
    assert.equal(decision.addToGroup, true);
});

test('testflight: no group add when already in the group', () => {
    const decision = decideTestFlight({ builds: [build('a', { groupIds: ['g'] })], submissions: [], groupId: 'g' });
    assert.equal(decision.addToGroup, false);
});

test('testflight: submits the newer build even while an older review is waiting', () => {
    for (const state of ['WAITING_FOR_REVIEW', 'IN_REVIEW']) {
        const submissions = [{ buildId: 'old', state }];
        const decision = decideTestFlight({ builds: [build('new'), build('old')], submissions, groupId: 'g' });
        assert.equal(decision.action, 'submit', state);
        assert.equal(decision.build.id, 'new');
    }
});

test('testflight: skips when the newest build itself is already waiting', () => {
    const submissions = [{ buildId: 'new', state: 'WAITING_FOR_REVIEW' }];
    const decision = decideTestFlight({ builds: [build('new'), build('old')], submissions, groupId: 'g' });
    assert.equal(decision.action, 'skip');
    assert.match(decision.reason, /already submitted \(WAITING_FOR_REVIEW\)/);
});

test('testflight: skips when the newest build was already submitted', () => {
    const decision = decideTestFlight({ builds: [build('a')], submissions: [{ buildId: 'a', state: 'APPROVED' }], groupId: 'g' });
    assert.equal(decision.action, 'skip');
    assert.match(decision.reason, /already submitted/);
});

test('testflight: skips with no processed build', () => {
    assert.equal(decideTestFlight({ builds: [], submissions: [], groupId: 'g' }).reason, 'no processed build');
});

const version = (id, state, over = {}) => ({ id, versionString: '1.0', state, buildId: null, ...over });

test('appstore: edits the editable version', () => {
    const decision = decideAppStore({ builds: [build('a')], versions: [version('v1', 'PREPARE_FOR_SUBMISSION')], submissions: [] });
    assert.equal(decision.create, false);
    assert.equal(decision.version.id, 'v1');
});

test('appstore: creates a version when the last one is live', () => {
    const versions = [version('v1', 'READY_FOR_SALE', { buildId: 'old' })];
    const decision = decideAppStore({ builds: [build('a')], versions, submissions: [] });
    assert.equal(decision.create, true);
    assert.equal(decision.version, null);
});

test('appstore: skips when the newest build is already live or in review', () => {
    for (const state of ['READY_FOR_SALE', 'WAITING_FOR_REVIEW', 'IN_REVIEW']) {
        const versions = [version('v1', state, { buildId: 'a' })];
        assert.equal(decideAppStore({ builds: [build('a')], versions, submissions: [] }).action, 'skip', state);
    }
});

test('appstore: skips while a store review is busy', () => {
    const decision = decideAppStore({
        builds: [build('a')], versions: [version('v1', 'PREPARE_FOR_SUBMISSION')], submissions: [{ id: 's', state: 'IN_REVIEW' }],
    });
    assert.equal(decision.action, 'skip');
});

test('ascToken is a verifiable ES256 JWT', () => {
    const { privateKey, publicKey } = generateKeyPairSync('ec', { namedCurve: 'P-256' });
    const pem = privateKey.export({ type: 'pkcs8', format: 'pem' });
    const [header, claims, signature] = ascToken({ keyId: 'K', issuerId: 'I', privateKeyPem: pem, nowSeconds: 1000 }).split('.');
    assert.equal(JSON.parse(Buffer.from(header, 'base64url')).kid, 'K');
    assert.equal(JSON.parse(Buffer.from(claims, 'base64url')).exp, 1000 + 15 * 60);
    const verifier = createVerify('SHA256').update(`${header}.${claims}`);
    assert.equal(verifier.verify({ key: publicKey, dsaEncoding: 'ieee-p1363' }, Buffer.from(signature, 'base64url')), true);
});

test('isTransient: network, 5xx and 429 retry; 4xx do not', () => {
    assert.equal(isTransient(new Error('network')), true);
    assert.equal(isTransient(Object.assign(new Error('x'), { status: 502 })), true);
    assert.equal(isTransient(Object.assign(new Error('x'), { status: 429 })), true);
    assert.equal(isTransient(Object.assign(new Error('x'), { status: 401 })), false);
});

test('client survives an HTML error body', async () => {
    const fetchImpl = async () => ({ status: 502, ok: false, text: async () => '<html>Bad gateway</html>' });
    const response = await createAscClient({ token: () => 't', fetchImpl }).get('/v1/x');
    assert.equal(response.status, 502);
    assert.match(response.json.errors[0].detail, /Bad gateway/);
});

/** A fake App Store Connect: the first route whose "METHOD path" prefix matches answers. */
function fakeAsc(routes) {
    const calls = [];
    const handle = (method) => async (path, body) => {
        calls.push([method, path, body]);
        const key = Object.keys(routes).find((candidate) => `${method} ${path}`.startsWith(candidate));
        const reply = key ? routes[key] : { status: 404, json: { errors: [{ detail: 'no route' }] } };
        return { status: reply.status ?? 200, ok: (reply.status ?? 200) < 300, json: reply.json ?? null };
    };
    return { asc: { get: handle('GET'), post: handle('POST'), patch: handle('PATCH') }, calls };
}

const buildsReply = {
    json: {
        data: [{
            id: 'b1',
            attributes: { version: '7', processingState: 'VALID', expired: false },
            relationships: { preReleaseVersion: { data: { id: 'p1' } }, betaGroups: { data: [] } },
        }],
        included: [{ type: 'preReleaseVersions', id: 'p1', attributes: { version: '1.54.1' } }],
    },
};
const groupsReply = { json: { data: [{ id: 'g1', attributes: { name: EXTERNAL_GROUP_NAME } }] } };

test('runApp testflight: adds to group then submits', async () => {
    const { asc, calls } = fakeAsc({
        'GET /v1/builds': buildsReply,
        [`GET /v1/apps/${APPS.rtst.id}/betaGroups`]: groupsReply,
        'GET /v1/betaAppReviewSubmissions': { json: { data: [] } },
        'POST /v1/betaGroups/g1/relationships/builds': { status: 204 },
        'POST /v1/betaAppReviewSubmissions': { status: 201 },
    });
    const result = await runApp(asc, Mode.TestFlight, 'rtst');
    assert.equal(result.outcome, Outcome.Submitted);
    assert.match(result.message, /1\.54\.1 \(7\)/);
    assert.ok(calls.some(([method, path]) => method === 'POST' && path === '/v1/betaAppReviewSubmissions'));
});

test('runApp testflight: a 409 STATE_ERROR fails with Apple text, a missing group is a failure', async () => {
    const groupsPath = `GET /v1/apps/${APPS.dino.id}/betaGroups`;
    const routes = {
        'GET /v1/builds': buildsReply,
        [groupsPath]: groupsReply,
        'GET /v1/betaAppReviewSubmissions': { json: { data: [] } },
        'POST /v1/betaGroups/g1/relationships/builds': { status: 204 },
        'POST /v1/betaAppReviewSubmissions': { status: 409, json: { errors: [{ code: 'STATE_ERROR', detail: 'busy' }] } },
    };
    const refused = await runApp(fakeAsc(routes).asc, Mode.TestFlight, 'dino');
    assert.equal(refused.outcome, Outcome.Failed);
    assert.match(refused.message, /busy/);
    routes[groupsPath] = { json: { data: [] } };
    const missing = await runApp(fakeAsc(routes).asc, Mode.TestFlight, 'dino');
    assert.equal(missing.outcome, Outcome.Failed);
    assert.match(missing.message, /Rofagha/);
});

test('runApp appstore: renames the editable version, attaches the build, submits, never cancels', async () => {
    const editable = { id: 'v1', attributes: { versionString: '1.0', appVersionState: 'PREPARE_FOR_SUBMISSION' }, relationships: {} };
    const { asc, calls } = fakeAsc({
        'GET /v1/builds': buildsReply,
        [`GET /v1/apps/${APPS.rtst.id}/appStoreVersions`]: { json: { data: [editable] } },
        'GET /v1/reviewSubmissions': { json: { data: [] } },
        'PATCH /v1/appStoreVersions/v1/relationships/build': { status: 204 },
        'PATCH /v1/appStoreVersions/v1': { json: {} },
        'POST /v1/reviewSubmissions': { status: 201, json: { data: { id: 'rs1' } } },
        'POST /v1/reviewSubmissionItems': { status: 201 },
        'PATCH /v1/reviewSubmissions/rs1': { json: {} },
    });
    const result = await runApp(asc, Mode.AppStore, 'rtst');
    assert.equal(result.outcome, Outcome.Submitted);
    const rename = calls.find(([method, path]) => method === 'PATCH' && path === '/v1/appStoreVersions/v1');
    assert.equal(rename[2].data.attributes.versionString, '1.54.1');
    const submit = calls.find(([, path]) => path === '/v1/reviewSubmissions/rs1');
    assert.deepEqual(submit[2].data.attributes, { submitted: true });
    assert.ok(!JSON.stringify(calls).includes('canceled'));
});

test('runApp: an unknown app fails without throwing; summaryLine names the app', async () => {
    assert.equal((await runApp(fakeAsc({}).asc, Mode.TestFlight, 'nope')).outcome, Outcome.Failed);
    assert.match(summaryLine('dino', { outcome: Outcome.Skipped, message: 'x' }), /Dino Reader: skipped, x/);
});

const editableVersion = { id: 'v1', attributes: { versionString: '1.0', appVersionState: 'PREPARE_FOR_SUBMISSION' }, relationships: {} };

function appStoreRoutes(extra) {
    return {
        'GET /v1/builds': buildsReply,
        [`GET /v1/apps/${APPS.rtst.id}/appStoreVersions`]: { json: { data: [editableVersion] } },
        'PATCH /v1/appStoreVersions/v1/relationships/build': { status: 204 },
        'PATCH /v1/appStoreVersions/v1': { json: {} },
        'PATCH /v1/reviewSubmissions/rs1': { json: {} },
        ...extra,
    };
}

test('appstore: a reused draft that already holds the version is not given a second item', async () => {
    const draft = { id: 'rs1', attributes: { state: 'READY_FOR_REVIEW' } };
    const { asc, calls } = fakeAsc(appStoreRoutes({
        'GET /v1/reviewSubmissions/rs1/items': { json: { data: [{ relationships: { appStoreVersion: { data: { id: 'v1' } } } }] } },
        'GET /v1/reviewSubmissions?': { json: { data: [draft] } },
    }));
    const result = await runApp(asc, Mode.AppStore, 'rtst');
    assert.equal(result.outcome, Outcome.Submitted);
    assert.ok(!calls.some(([method, path]) => method === 'POST' && path === '/v1/reviewSubmissionItems'));
    assert.ok(calls.some(([method, path]) => method === 'PATCH' && path === '/v1/reviewSubmissions/rs1'));
});

test('appstore: a reused draft without the version gets the item', async () => {
    const draft = { id: 'rs1', attributes: { state: 'READY_FOR_REVIEW' } };
    const { asc, calls } = fakeAsc(appStoreRoutes({
        'GET /v1/reviewSubmissions/rs1/items': { json: { data: [] } },
        'GET /v1/reviewSubmissions?': { json: { data: [draft] } },
        'POST /v1/reviewSubmissionItems': { status: 201 },
    }));
    assert.equal((await runApp(asc, Mode.AppStore, 'rtst')).outcome, Outcome.Submitted);
    assert.ok(calls.some(([method, path]) => method === 'POST' && path === '/v1/reviewSubmissionItems'));
});

test('appstore: an unknown marketing version fails and writes nothing', async () => {
    const noPre = { json: { data: [{ id: 'b1', attributes: { version: '7', processingState: 'VALID' }, relationships: {} }] } };
    const { asc, calls } = fakeAsc(appStoreRoutes({
        'GET /v1/builds': noPre,
        'GET /v1/reviewSubmissions': { json: { data: [] } },
    }));
    const result = await runApp(asc, Mode.AppStore, 'rtst');
    assert.equal(result.outcome, Outcome.Failed);
    assert.ok(calls.every(([method]) => method === 'GET'));
});

test('appstore: skips when the build marketing version is already live', () => {
    const versions = [version('v1', 'READY_FOR_SALE', { versionString: '1.54.1', buildId: 'old' })];
    const decision = decideAppStore({ builds: [build('a')], versions, submissions: [] });
    assert.equal(decision.action, 'skip');
    assert.match(decision.reason, /needs a new marketing version/);
});

test('testflight: a rejected newest build is a failure, not a quiet skip', async () => {
    const decision = decideTestFlight({ builds: [build('a')], submissions: [{ buildId: 'a', state: 'REJECTED' }], groupId: 'g' });
    assert.equal(decision.action, 'fail');
    assert.match(decision.reason, /read Apple's reason in App Store Connect/);
    const { asc } = fakeAsc({
        'GET /v1/builds': buildsReply,
        [`GET /v1/apps/${APPS.rtst.id}/betaGroups`]: groupsReply,
        'GET /v1/betaAppReviewSubmissions': { json: { data: [{ relationships: { build: { data: { id: 'b1' } } }, attributes: { betaReviewState: 'REJECTED' } }] } },
    });
    assert.equal((await runApp(asc, Mode.TestFlight, 'rtst')).outcome, Outcome.Failed);
});

test('listBetaSubmissions asks for include=build and chunks filter[build] by 50', async () => {
    const { asc, calls } = fakeAsc({
        'GET /v1/betaAppReviewSubmissions': { json: { data: [{ relationships: { build: { data: { id: 'b1' } } }, attributes: { betaReviewState: 'APPROVED' } }] } },
    });
    const builds = Array.from({ length: 120 }, (_, index) => ({ id: `id${index}` }));
    const found = await listBetaSubmissions(asc, builds);
    assert.equal(calls.length, 3);
    assert.ok(calls.every(([, path]) => path.includes('include=build')));
    assert.ok(calls.every(([, path]) => decodeURIComponent(path).split('filter[build]=')[1].split('&')[0].split(',').length <= 50));
    assert.equal(found.length, 3);
    assert.equal(found[0].buildId, 'b1');
});

test('testflight: a 409 that is not a "not now" error fails with Apple text', async () => {
    const { asc } = fakeAsc({
        'GET /v1/builds': buildsReply,
        [`GET /v1/apps/${APPS.rtst.id}/betaGroups`]: groupsReply,
        'GET /v1/betaAppReviewSubmissions': { json: { data: [] } },
        'POST /v1/betaGroups/g1/relationships/builds': { status: 204 },
        'POST /v1/betaAppReviewSubmissions': { status: 409, json: { errors: [{ code: 'ENTITY_ERROR', detail: 'Beta app description is missing' }] } },
    });
    const result = await runApp(asc, Mode.TestFlight, 'rtst');
    assert.equal(result.outcome, Outcome.Failed);
    assert.match(result.message, /Beta app description is missing/);
});

test('GETs retry a transient failure and give up on a fatal one', async () => {
    retryPolicy.baseDelayMs = 0;
    let attempts = 0;
    const flaky = {
        get: async () => (++attempts < 3 ? { status: 502, ok: false, json: null } : { status: 200, ok: true, json: { data: [] } }),
        post: async () => ({ status: 404, ok: false, json: null }),
        patch: async () => ({ status: 404, ok: false, json: null }),
    };
    const { runTestFlight } = await import('./ios-review-cadence.mjs');
    await assert.rejects(() => runTestFlight(flaky, APPS.rtst), /no TestFlight group|No TestFlight group/);
    assert.equal(attempts, 4);
    let fatal = 0;
    const denied = { ...flaky, get: async () => { fatal++; return { status: 401, ok: false, json: null }; } };
    await assert.rejects(() => runTestFlight(denied, APPS.rtst), /401/);
    assert.equal(fatal, 1);
});
