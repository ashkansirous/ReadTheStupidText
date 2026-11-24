import { test } from 'node:test';
import assert from 'node:assert/strict';
import { shelfListingOf,isReusable, oneLine, packagesToFetch } from './fetch-shelf-packages.mjs';

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

test('with includeFree (the offline Windows VM) every chosen listed book has a package to fetch', () => {
    const { packages } = packagesToFetch(list, [{ id: 'free1' }, { id: 'free2' }, { id: 'paid1' }], true);
    assert.deepEqual(packages.map((item) => item.id), ['free1', 'free2', 'paid1']);
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

test('shelfListingOf keeps only the fetched books with a complete age range or a sale', () => {
    const listing = shelfListingOf({ stories: [{ id: 'a', ageMin: 3, ageMax: 5 }, { id: 'b', ageMin: 4 }, { id: 'c', ageMin: 6, ageMax: 9 }] }, [{ id: 'a' }, { id: 'b' }]);
    assert.deepEqual(listing, { a: { min: 3, max: 5 } });
});

test('shelfListingOf records a paid book\'s sale fields and nothing for a free one', () => {
    const stories = [
        { id: 'p', access: 'paid', productId: ' prod.p ', unlockedBy: ['prod.p', 'sub', 7, ' '], ageMin: 3, ageMax: 5 },
        { id: 'f', access: 'free', productId: 'ignored', unlockedBy: ['x'] },
        { id: 'q', access: 'paid' },
    ];
    assert.deepEqual(shelfListingOf({ stories }, [{ id: 'p' }, { id: 'f' }, { id: 'q' }]), {
        p: { access: 'paid', productId: 'prod.p', unlockedBy: ['prod.p', 'sub'], min: 3, max: 5 },
        q: { access: 'paid' },
    });
});

test('an empty list fetches nothing', () => {
    assert.deepEqual(packagesToFetch({}, [{ id: 'paid1' }]), { packages: [], missing: ['paid1'] });
});
