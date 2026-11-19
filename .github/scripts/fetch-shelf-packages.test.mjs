import { test } from 'node:test';
import assert from 'node:assert/strict';
import { packagesToFetch } from './fetch-shelf-packages.mjs';

const list = {
    stories: [
        { id: 'free1', title: 'Free', version: 3, access: 'free' },
        { id: 'free2', title: 'No access field', version: 1 },
        { id: 'paid1', title: 'Paid', version: 16, access: 'paid' },
        { id: 'other', title: 'Not chosen', version: 2, access: 'paid' },
    ],
};

test('only a chosen book the live list sells as paid has a package to fetch, at its listed version', () => {
    const { packages, missing } = packagesToFetch(list, [{ id: 'free1' }, { id: 'free2' }, { id: 'paid1' }]);
    assert.deepEqual(packages, [{ id: 'paid1', title: 'Paid', version: 16, key: 'dinoreader/stories/paid1/story-v16.dino' }]);
    assert.deepEqual(missing, []);
});

test('a chosen book the list does not hold is named as missing, never fetched', () => {
    const { packages, missing } = packagesToFetch(list, [{ id: 'gone' }, { id: 'paid1' }]);
    assert.equal(packages.length, 1);
    assert.deepEqual(missing, ['gone']);
});

test('an empty list fetches nothing', () => {
    assert.deepEqual(packagesToFetch({}, [{ id: 'paid1' }]), { packages: [], missing: ['paid1'] });
});
