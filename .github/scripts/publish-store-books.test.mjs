import test from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

import { audioPackIsLive, pruneUnbuiltAudio, audioPacksBehind,audioUploadsFor, bytesFromMeta, MAX_DELETIONS, MAX_VERSION_BUMPS, Outcome, checkDeletionLimit, delistKey, hashFromMeta, highestPackage, contentBooks, starterDrift, liveFromApi, liveStateOf, metaDiffers, planDeletions, planStoreUploads, runDeletions, readBuilt, runMetaRefresh, runUploads, stillBehind, summaryText, uploadsFor } from './publish-store-books.mjs';

const A = 'a'.repeat(32);
const B = 'b'.repeat(32);
const outcomeOf = (built, live) => planStoreUploads(built, live).map((item) => item.outcome);

test('a book that is not live yet is uploaded', () => {
    assert.deepEqual(outcomeOf([{ id: A, version: 1 }], []), [Outcome.Upload]);
});

test('a book behind the live one is uploaded, an equal one is not', () => {
    assert.deepEqual(outcomeOf([{ id: A, version: 6 }, { id: B, version: 2 }], [{ id: A, version: 1 }, { id: B, version: 2 }]), [Outcome.Upload, Outcome.UpToDate]);
});

test('a built version lower than live is never uploaded', () => {
    assert.deepEqual(outcomeOf([{ id: A, version: 3 }], [{ id: A, version: 5 }]), [Outcome.LiveNewer]);
});

test('bad meta (no version, a string, zero, a bad id) is invalid and never uploaded', () => {
    assert.deepEqual(outcomeOf([{ id: A, version: null }, { id: A, version: '4' }, { id: A, version: 0 }, { id: 'nope', version: 2 }], []), Array(4).fill(Outcome.Invalid));
});

test('uploads go cover, package, then meta.json last', () => {
    const names = uploadsFor({ id: A, version: 6 }, '/s/a', true).map((upload) => upload.name);
    assert.deepEqual(names, ['cover.jpg', 'story-v6.dino', 'meta.json']);
    assert.deepEqual(uploadsFor({ id: A, version: 6 }, '/s/a', false).map((upload) => upload.name), ['story-v6.dino', 'meta.json']);
    assert.equal(uploadsFor({ id: A, version: 6 }, '/s/a', true)[2].key, `dinoreader/stories/${A}/meta.json`);
});

test('liveFromApi reads the stories list and rejects anything else', () => {
    assert.deepEqual(liveFromApi({ schemaVersion: 1, stories: [{ id: A, version: 3, title: 'x' }] }), [{ id: A, version: 3, title: 'x', audio: null }]);
    assert.deepEqual(liveFromApi({ stories: [{ id: A, version: 3, audio: { fa: { version: 1 } } }] })[0].audio, { fa: { version: 1 } });
    assert.throws(() => liveFromApi({}), /stories/);
});

const C = 'c'.repeat(32);

test('Slice 239: the live state carries each live meta hash, null for a book published before the hash existed', () => {
    const metas = { [A]: JSON.stringify({ version: 3, contentHash: 'abc' }), [B]: JSON.stringify({ version: 2 }) };
    assert.deepEqual(liveStateOf([{ id: A, version: 3 }, { id: B, version: 2 }, { id: C, version: 1 }], (id) => metas[id] ?? null), {
        [A]: { version: 3, contentHash: 'abc', maxPackage: 0 }, [B]: { version: 2, contentHash: null, maxPackage: 0 }, [C]: { version: 1, contentHash: null, maxPackage: 0 },
    });
    assert.equal(hashFromMeta('not json'), null);
    assert.equal(hashFromMeta(JSON.stringify({ contentHash: '' })), null);
});

test('Slice 260: the live state carries the live meta bytes (the live package size) when there is one, and no key otherwise', () => {
    const metas = { [A]: JSON.stringify({ version: 3, contentHash: 'abc', bytes: 4096 }), [B]: JSON.stringify({ version: 2, contentHash: 'x', bytes: 'big' }) };
    const state = liveStateOf([{ id: A, version: 3 }, { id: B, version: 2 }], (id) => metas[id] ?? null);
    assert.equal(state[A].bytes, 4096);
    assert.equal('bytes' in state[B], false);
    assert.equal(bytesFromMeta(JSON.stringify({ bytes: 0 })), null);
});

test('Slice 239: the live state carries the highest package in the bucket, probed from live + 1 (from 1 for a store book that is not live)', () => {
    const D = 'd'.repeat(32);
    const calls = [];
    const state = liveStateOf([{ id: A, version: 4 }], () => null, (id, from) => { calls.push([id, from]); return id === A ? 5 : 2; }, [A, D]);
    assert.deepEqual(calls, [[A, 5], [D, 1]]);
    assert.deepEqual(state, { [A]: { version: 4, contentHash: null, maxPackage: 5 }, [D]: { version: 0, contentHash: null, maxPackage: 2 } });
});

test('Slice 239: package probing climbs past a gap smaller than the window and stops after a run of absent versions', () => {
    assert.equal(highestPackage((v) => [5, 6, 8].includes(v), 5), 8);
    assert.equal(highestPackage((v) => v === 9, 5), 0);
    assert.equal(highestPackage(() => false, 1), 0);
});

test('Slice 239: a Starter folder missing from dino-starters.json, or listed but not a Starter, fails the content check', () => {
    const books = contentBooks(['a', 'b', 'c'], (stem) => ({ id: stem, kind: stem === 'c' ? 'Store' : 'Starter' }));
    assert.deepEqual(starterDrift(books, ['a', 'b']), []);
    assert.match(starterDrift(books, ['a']).join(), /b is a Starter .* missing from .*dino-starters\.json/);
    assert.match(starterDrift(books, ['a', 'b', 'c']).join(), /lists c but its book\.json kind is store/);
    assert.match(starterDrift(books, ['a', 'b', 'gone']).join(), /lists gone but there is no content\/dino\/gone/);
});

test('Slice 239: a live story with no built folder is planned for delisting, never one that is built', () => {
    const deletions = planDeletions([{ id: A, version: 1 }], [{ id: A, version: 1 }, { id: B, version: 4, title: 'Gone' }]);
    assert.deepEqual(deletions.map((d) => d.id), [B]);
});

test('Slice 239: delisting removes meta.json only, logs first, and reports each id', () => {
    const removed = [];
    assert.deepEqual(runDeletions([{ id: B, version: 4, title: 'Gone' }], { remove: (key) => removed.push(key) }), [B]);
    assert.deepEqual(removed, [delistKey(B)]);
    assert.equal(delistKey(B), `dinoreader/stories/${B}/meta.json`);
});

test('Slice 239: a failed delisting is reported after the others ran', () => {
    const removed = [];
    assert.throws(() => runDeletions([{ id: A }, { id: B }], { remove: (key) => { if (key.includes(A)) throw new Error('boom'); removed.push(key); } }), (error) => error.done.length === 1 && /boom/.test(error.message));
    assert.equal(removed.length, 1);
});

test('Slice 239: too many delistings at once (a broken checkout) is refused', () => {
    const live = Array.from({ length: 10 }, (_, i) => ({ id: String(i).repeat(32) }));
    assert.doesNotThrow(() => checkDeletionLimit([], live));
    assert.doesNotThrow(() => checkDeletionLimit(live.slice(0, 1), live));
    assert.throws(() => checkDeletionLimit(live.slice(0, MAX_DELETIONS + 1), live), /Refusing/);
    assert.throws(() => checkDeletionLimit(live.slice(0, 2), live.slice(0, 3)), /Refusing/);
});

function storeFolder(books) {
    const root = mkdtempSync(join(tmpdir(), 'store-books-'));
    for (const { id, version, withPackage = true, cover = true, hash, audio, zips = [] } of books) {
        mkdirSync(join(root, id));
        writeFileSync(join(root, id, 'meta.json'), JSON.stringify({ version, ...(hash ? { contentHash: hash } : {}), ...(audio ? { audio } : {}) }));
        zips.forEach((zip) => writeFileSync(join(root, id, zip), 'z'));
        if (withPackage) writeFileSync(join(root, id, `story-v${version}.dino`), 'x');
        if (cover) writeFileSync(join(root, id, 'cover.jpg'), 'x');
    }
    return root;
}

function withStore(books, body) {
    const root = storeFolder(books);
    try {
        body(root);
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
}

test('runUploads sends only what is behind, in order, and reports it', () => {
    withStore([{ id: A, version: 6 }, { id: B, version: 2 }], (root) => {
        const sent = [];
        const plan = planStoreUploads(readBuilt(root), [{ id: A, version: 1 }, { id: B, version: 2 }]);
        const done = runUploads(plan, root, { put: (upload) => sent.push(upload.key), packageExists: () => false });
        assert.deepEqual(sent, [`dinoreader/stories/${A}/cover.jpg`, `dinoreader/stories/${A}/story-v6.dino`, `dinoreader/stories/${A}/meta.json`]);
        assert.deepEqual(done, [{ id: A, version: 6 }]);
    });
});

test('Slice 265: the audio packs built in this run go up first, before the cover, package and meta.json', () => {
    const audio = { fa: { version: 2 }, de: { version: 1 } };
    withStore([{ id: A, version: 6, audio, zips: ['audio-fa-v2.zip'] }], (root) => {
        const sent = [];
        const plan = planStoreUploads(readBuilt(root), [{ id: A, version: 1 }]);
        runUploads(plan, root, { put: (upload) => sent.push(upload.name), packageExists: () => false });
        assert.deepEqual(sent, ['audio-fa-v2.zip', 'cover.jpg', 'story-v6.dino', 'meta.json']); // audio-de-v1.zip was not built here, so it is already live
    });
});

test('Slice 265: audioPackIsLive reads the live list, not the bucket', () => {
    const live = [{ id: A, version: 1, audio: { fa: { version: 2 } } }];
    assert.equal(audioPackIsLive(live, { key: `dinoreader/stories/${A}/audio-fa-v2.zip` }), true);
    assert.equal(audioPackIsLive(live, { key: `dinoreader/stories/${A}/audio-fa-v3.zip` }), false);
    assert.equal(audioPackIsLive(live, { key: `dinoreader/stories/${A}/audio-de-v2.zip` }), false);
    assert.equal(audioPackIsLive(live, { key: `dinoreader/stories/${B}/audio-fa-v2.zip` }), false);
});

test('Slice 265: pruneUnbuiltAudio drops packs that are neither live nor built here', () => {
    const meta = JSON.stringify({ version: 3, audio: { fa: { version: 2 }, de: { version: 1 }, no: { version: 1 } } });
    const { text, removed } = pruneUnbuiltAudio(meta, '/s/a', { de: { version: 1 } }, (path) => path.endsWith('audio-fa-v2.zip'));
    assert.deepEqual(removed, ['no']);
    assert.deepEqual(Object.keys(JSON.parse(text).audio), ['fa', 'de']);
    const none = pruneUnbuiltAudio(meta, '/s/a', {}, () => false);
    assert.equal(none.removed.length, 3);
    assert.equal('audio' in JSON.parse(none.text), false);
    assert.equal(pruneUnbuiltAudio('{"version":1}', '/s/a', {}, () => false).text, '{"version":1}');
});

test('Slice 265: an audio pack already in the bucket is not overwritten', () => {
    withStore([{ id: A, version: 6, audio: { fa: { version: 2 } }, zips: ['audio-fa-v2.zip'] }], (root) => {
        const sent = [];
        const plan = planStoreUploads(readBuilt(root), [{ id: A, version: 1 }]);
        runUploads(plan, root, { put: (upload) => sent.push(upload.name), packageExists: (upload) => upload.name === 'audio-fa-v2.zip' });
        assert.deepEqual(sent, ['cover.jpg', 'story-v6.dino', 'meta.json']);
    });
});

test('Slice 265: a same-version book with a newly built pack sends the pack, then its changed meta, and an unchanged meta still sends the pack', () => {
    const audio = { fa: { version: 2 } };
    withStore([{ id: A, version: 3, audio, zips: ['audio-fa-v2.zip'] }], (root) => {
        const plan = planStoreUploads(readBuilt(root), [{ id: A, version: 3 }]);
        const changed = [];
        runMetaRefresh(plan, root, { put: (upload) => changed.push(upload.name), liveMeta: () => '{"version":3}', packageExists: () => false });
        assert.deepEqual(changed, ['audio-fa-v2.zip', 'meta.json']);
        const same = [];
        runMetaRefresh(plan, root, { put: (upload) => same.push(upload.name), liveMeta: () => JSON.stringify({ version: 3, audio }), packageExists: () => false });
        assert.deepEqual(same, ['audio-fa-v2.zip']);
    });
});

test('Slice 265: audioUploadsFor ignores a meta with no audio, a broken meta and an odd language', () => {
    const exists = () => true;
    assert.deepEqual(audioUploadsFor({ id: A }, '/s', '{"version":1}', exists), []);
    assert.deepEqual(audioUploadsFor({ id: A }, '/s', 'not json', exists), []);
    assert.deepEqual(audioUploadsFor({ id: A }, '/s', '{"audio":{"../x":{"version":1}}}', exists), []);
    assert.deepEqual(audioUploadsFor({ id: A }, '/s', '{"audio":{"fa":{"version":3}}}', exists).map((u) => u.key), [`dinoreader/stories/${A}/audio-fa-v3.zip`]);
});

test('Slice 265: a language is behind when the live store lacks its pack or lists a lower version, never when it is equal or higher', () => {
    const books = [{ stem: 'pigs', id: A, packs: { de: 1, fa: 2, ja: 1 } }, { stem: 'hen', id: B, packs: { fa: 1 } }, { stem: 'done', id: 'c'.repeat(32), packs: { fa: 1 } }];
    const live = [{ id: A, audio: { de: { version: 1 }, fa: { version: 1 }, ja: { version: 5 } } }, { id: B, audio: null }, { id: 'c'.repeat(32), audio: { fa: { version: 1 } } }];
    assert.deepEqual(audioPacksBehind(books, live), [{ stem: 'pigs', languages: ['fa'] }, { stem: 'hen', languages: ['fa'] }]);
    assert.deepEqual(audioPacksBehind([{ stem: 'new', id: A, packs: { fa: 1 } }], []), [{ stem: 'new', languages: ['fa'] }]); // a book that is not live at all needs every pack
});

test('an existing package is never overwritten, but the meta still goes up', () => {
    withStore([{ id: A, version: 6 }], (root) => {
        const sent = [];
        const plan = planStoreUploads(readBuilt(root), [{ id: A, version: 5 }]);
        runUploads(plan, root, { put: (upload) => sent.push(upload.name), packageExists: () => true });
        assert.deepEqual(sent, ['cover.jpg', 'meta.json']);
    });
});

test('Slice 239: a hashed book never reuses a package version: it moves up until a free one, then package and meta go up under it', () => {
    withStore([{ id: A, version: 6, hash: 'h' }], (root) => {
        const sent = [];
        const asked = [];
        const plan = planStoreUploads(readBuilt(root), []);
        const packageExists = ({ key }) => { asked.push(key); return key.endsWith('story-v6.dino') || key.endsWith('story-v7.dino'); };
        const done = runUploads(plan, root, { put: (upload) => sent.push(upload.name), packageExists });
        assert.deepEqual(done, [{ id: A, version: 8 }]);
        assert.deepEqual(sent, ['cover.jpg', 'story-v8.dino', 'meta.json']);
        assert.equal(asked.length, 3);
        assert.equal(JSON.parse(readFileSync(join(root, A, 'meta.json'), 'utf8')).version, 8);
        assert.ok(existsSync(join(root, A, 'story-v8.dino')) && !existsSync(join(root, A, 'story-v6.dino')));
    });
});

test('Slice 239: a hashed book whose versions are all taken fails and sends nothing, not even the meta', () => {
    withStore([{ id: A, version: 6, hash: 'h' }], (root) => {
        const sent = [];
        const plan = planStoreUploads(readBuilt(root), []);
        assert.throws(() => runUploads(plan, root, { put: (upload) => sent.push(upload.name), packageExists: () => true }), new RegExp(`${MAX_VERSION_BUMPS} package versions`));
        assert.deepEqual(sent, []);
    });
});

test('Slice 239: a story is delisted only when this pipeline manages it (live hash) and no content folder has its id', () => {
    const live = [{ id: A, version: 1 }, { id: B, version: 4 }, { id: C, version: 2 }];
    const managed = (id) => id !== B;
    assert.deepEqual(planDeletions([], live, { contentIds: new Set([A]), isManaged: managed }).map((d) => d.id), [C]);
});

test('a meta that names a missing package stops the run before anything is sent', () => {
    withStore([{ id: A, version: 6, withPackage: false }], (root) => {
        const sent = [];
        const plan = planStoreUploads(readBuilt(root), []);
        assert.throws(() => runUploads(plan, root, { put: (upload) => sent.push(upload.name), packageExists: () => false }), /missing/);
        assert.deepEqual(sent, []);
    });
});

test('a failing upload stops the run so the meta is never sent', () => {
    withStore([{ id: A, version: 6 }], (root) => {
        const sent = [];
        const plan = planStoreUploads(readBuilt(root), []);
        const put = (upload) => { if (upload.isPackage) throw new Error('boom'); sent.push(upload.name); };
        assert.throws(() => runUploads(plan, root, { put, packageExists: () => false }), /boom/);
        assert.deepEqual(sent, ['cover.jpg']);
    });
});

test('one failing book does not stop the others, and the failure lists it', () => {
    withStore([{ id: A, version: 6, withPackage: false }, { id: B, version: 2 }], (root) => {
        const sent = [];
        const plan = planStoreUploads(readBuilt(root), []);
        let error;
        try { runUploads(plan, root, { put: (upload) => sent.push(upload.key), packageExists: () => false }); } catch (e) { error = e; }
        assert.match(error.message, new RegExp(A));
        assert.deepEqual(error.done, [{ id: B, version: 2 }]);
        assert.ok(sent.some((key) => key.includes(B) && key.endsWith('meta.json')));
    });
});

test('readBuilt gives a folder with unreadable meta the version null', () => {
    withStore([{ id: A, version: 2 }], (root) => {
        mkdirSync(join(root, B));
        assert.deepEqual(readBuilt(root), [{ id: A, version: 2 }, { id: B, version: null }]);
    });
});

test('stillBehind lists a lagging book, not an up-to-date one', () => {
    const behind = stillBehind([{ id: A, version: 3 }, { id: B, version: 1 }], [{ id: A, version: 2 }, { id: B, version: 1 }]);
    assert.deepEqual(behind.map((item) => item.id), [A]);
});

test('the summary names what was uploaded and what was skipped', () => {
    const plan = planStoreUploads([{ id: A, version: 6 }, { id: B, version: 1 }], [{ id: A, version: 1 }, { id: B, version: 4 }]);
    const text = summaryText(plan, [{ id: A, version: 6 }]);
    assert.match(text, /1 uploaded/);
    assert.match(text, new RegExp(`uploaded ${A} v6`));
    assert.match(text, /warning/);
});

test('readBuilt ignores folders that are not story ids and throws when no book is built', () => {
    withStore([{ id: A, version: 2 }], (root) => {
        mkdirSync(join(root, 'stray-folder'));
        assert.deepEqual(readBuilt(root).map((book) => book.id), [A]);
    });
    assert.throws(() => readBuilt(join(tmpdir(), 'no-such-store-folder-s158')), /No built store books/);
});

test('metaDiffers compares content, not key order or whitespace, and treats a missing live meta as different', () => {
    assert.equal(metaDiffers('{"a":1,"b":{"x":2,"y":3}}', '{ "b": {"y":3,"x":2}, "a": 1 }'), false);
    assert.equal(metaDiffers('{"a":1,"creditsByLanguage":{"fa":"x"}}', '{"a":1}'), true);
    assert.equal(metaDiffers('{"a":1}', null), true);
    assert.equal(metaDiffers('{"a":1}', 'not json'), true);
});

test('a same-version book with changed meta gets meta.json alone re-uploaded, never a package', () => {
    withStore([{ id: A, version: 3 }, { id: B, version: 2 }], (root) => {
        const plan = planStoreUploads([{ id: A, version: 3 }, { id: B, version: 2 }], [{ id: A, version: 3 }, { id: B, version: 2 }]);
        const sent = [];
        const liveMeta = (upload) => (upload.key.includes(A) ? '{"version":3}' : '{"version":2,"credits":"old"}');
        const done = runMetaRefresh(plan, root, { put: (upload) => sent.push(upload.name), liveMeta });
        assert.deepEqual(done, [B]);
        assert.deepEqual(sent, ['meta.json']);
    });
});

test('a bucket meta.json of a different (newer) version is never overwritten by the refresh', () => {
    withStore([{ id: A, version: 1 }], (root) => {
        const plan = planStoreUploads([{ id: A, version: 1 }], [{ id: A, version: 1 }]);
        const sent = [];
        assert.deepEqual(runMetaRefresh(plan, root, { put: (upload) => sent.push(upload.name), liveMeta: () => '{"version":2,"credits":"new"}' }), []);
        assert.deepEqual(sent, []);
    });
});

test('a book that is being uploaded or is behind live is not touched by the meta refresh', () => {
    withStore([{ id: A, version: 4 }, { id: B, version: 1 }], (root) => {
        const plan = planStoreUploads([{ id: A, version: 4 }, { id: B, version: 1 }], [{ id: A, version: 3 }, { id: B, version: 2 }]);
        const sent = [];
        assert.deepEqual(runMetaRefresh(plan, root, { put: (upload) => sent.push(upload.name), liveMeta: () => null }), []);
        assert.deepEqual(sent, []);
    });
});

test('a failed meta refresh is reported after the others ran', () => {
    withStore([{ id: A, version: 3 }, { id: B, version: 3 }], (root) => {
        const plan = planStoreUploads([{ id: A, version: 3 }, { id: B, version: 3 }], [{ id: A, version: 3 }, { id: B, version: 3 }]);
        const sent = [];
        const put = (upload) => { if (upload.key.includes(A)) throw new Error('boom'); sent.push(upload.key); };
        assert.throws(() => runMetaRefresh(plan, root, { put, liveMeta: () => null }), (error) => /boom/.test(error.message) && error.done.length === 1);
        assert.equal(sent.length, 1);
    });
});

test('the summary names a refreshed meta.json', () => {
    const plan = planStoreUploads([{ id: A, version: 3 }], [{ id: A, version: 3 }]);
    assert.match(summaryText(plan, [], [A]), new RegExp(`refreshed meta.json of ${A}`));
});
