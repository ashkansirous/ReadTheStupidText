import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

import { Outcome, liveFromApi, planStoreUploads, readBuilt, runUploads, stillBehind, summaryText, uploadsFor } from './publish-store-books.mjs';

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
    assert.deepEqual(liveFromApi({ schemaVersion: 1, stories: [{ id: A, version: 3, title: 'x' }] }), [{ id: A, version: 3 }]);
    assert.throws(() => liveFromApi({}), /stories/);
});

function storeFolder(books) {
    const root = mkdtempSync(join(tmpdir(), 'store-books-'));
    for (const { id, version, withPackage = true, cover = true } of books) {
        mkdirSync(join(root, id));
        writeFileSync(join(root, id, 'meta.json'), JSON.stringify({ version }));
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

test('an existing package is never overwritten, but the meta still goes up', () => {
    withStore([{ id: A, version: 6 }], (root) => {
        const sent = [];
        const plan = planStoreUploads(readBuilt(root), [{ id: A, version: 5 }]);
        runUploads(plan, root, { put: (upload) => sent.push(upload.name), packageExists: () => true });
        assert.deepEqual(sent, ['cover.jpg', 'meta.json']);
    });
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
