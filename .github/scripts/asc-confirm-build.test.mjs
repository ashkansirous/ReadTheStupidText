import test from 'node:test';
import assert from 'node:assert/strict';

import { findBuilds, waitForBuild } from './asc-confirm-build.mjs';

const build = (buildNumber = '7', processingState = 'PROCESSING') => ({ buildNumber, processingState });
const noSleep = async () => { };
const quiet = (fn) => async (...args) => {
    const log = console.log;
    console.log = () => { };
    try { return await fn(...args); } finally { console.log = log; }
};

test('findBuilds asks for the app and the build number and maps the entries', async () => {
    let path = '';
    const asc = { get: async (p) => { path = p; return { ok: true, json: { data: [{ attributes: { version: '7', processingState: 'PROCESSING' } }] } }; } };
    assert.deepEqual(await findBuilds(asc, '123', '7'), [build('7')]);
    const query = new URL(`https://x${path}`).searchParams;
    assert.equal(query.get('filter[app]'), '123');
    assert.equal(query.get('filter[version]'), '7');
    assert.equal(query.get('include'), null);
});

test('findBuilds throws on an API error and returns empty for no data', async () => {
    await assert.rejects(findBuilds({ get: async () => ({ ok: false, status: 503, json: null }) }, '1', '7'), /503/);
    assert.deepEqual(await findBuilds({ get: async () => ({ ok: true, json: {} }) }, '1', '7'), []);
});

test('a build that shows on a later poll is found', async () => {
    let calls = 0;
    const list = async () => (++calls < 3 ? [] : [build('7')]);
    const found = await waitForBuild({ list, waitSeconds: 300, pollSeconds: 30, sleep: noSleep });
    assert.equal(found.buildNumber, '7');
    assert.equal(calls, 3);
});

test('an API error inside the window is polled again and the build is then found', quiet(async () => {
    let calls = 0;
    const list = async () => {
        if (++calls < 3) throw new Error('GET /v1/builds failed (503)');
        return [build('7', 'VALID')];
    };
    const found = await waitForBuild({ list, waitSeconds: 300, pollSeconds: 30, sleep: noSleep });
    assert.equal(found.processingState, 'VALID');
    assert.equal(calls, 3);
}));

test('only errors for the whole window ends null, not a throw', quiet(async () => {
    let calls = 0;
    const list = async () => { calls++; throw new Error('network'); };
    assert.equal(await waitForBuild({ list, waitSeconds: 90, pollSeconds: 30, sleep: noSleep }), null);
    assert.equal(calls, 4);
}));

test('a build that never shows ends null after the bounded wait', async () => {
    let calls = 0;
    const list = async () => { calls++; return []; };
    assert.equal(await waitForBuild({ list, waitSeconds: 90, pollSeconds: 30, sleep: noSleep }), null);
    assert.equal(calls, 4);
});
