import { test } from 'node:test';
import assert from 'node:assert/strict';
import { isReusable, oneLine, packagesToFetch } from './fetch-shelf-packages.mjs';

const list = {
    stories: [
        { id: 'free1', title: 'Free', version: 3, access: 'free' },
        { id: 'free2', title: 'No access field', version: 1 },
        { id: 'paid1', title: 'Paid', version: 16, bytes: 2978407, access: 'paid' },
        { id: 'other', title: 'Not chosen', version: 2, access: 'paid' },
    ],
};

test('only a chosen book the live list sells as paid has a package to fetch, at its listed version', () => {
    const { packages, missing } = packagesToFetch(list, [{ id: 'free1' }, { id: 'free2' }, { id: 'paid1' }]);
    assert.deepEqual(packages, [{ id: 'paid1', title: 'Paid', version: 16, bytes: 2978407, key: 'dinoreader/stories/paid1/story-v16.dino' }]);
    assert.deepEqual(missing, []);
});

test('a chosen book the list does not hold is named as missing, never fetched', () => {
    const { packages, missing } = packagesToFetch(list, [{ id: 'gone' }, { id: 'paid1' }]);
    assert.equal(packages.length, 1);
    assert.deepEqual(missing, ['gone']);
});

test('a file on disk is reused only at exactly the listed byte count', () => {
    assert.equal(isReusable(2978407, 2978407), true);
    assert.equal(isReusable(1200, 2978407), false);
    assert.equal(isReusable(0, 0), false);
    assert.equal(isReusable(2978407, undefined), false);
});

test('the wrangler tail is one line, at most 300 characters', () => {
    const line = oneLine(`first\nsecond\r\n${'x'.repeat(400)}`);
    assert.equal(line.includes('\n'), false);
    assert.equal(line.length, 300);
});

test('an empty list fetches nothing', () => {
    assert.deepEqual(packagesToFetch({}, [{ id: 'paid1' }]), { packages: [], missing: ['paid1'] });
});
