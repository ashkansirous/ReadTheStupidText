import test from 'node:test';
import assert from 'node:assert/strict';

import { siteChecksNeeded } from './site-checks-needed.mjs';

test('Slice 239: a push that only touches Dino book content needs no site checks', () => {
    assert.equal(siteChecksNeeded(['content/dino/new-book/story.md', 'content/dino/new-book/pictures/1.jpg', 'content/dino/old/book.json']), false);
});

test('anything else, an empty or an unknown change set runs the site checks', () => {
    assert.equal(siteChecksNeeded(['content/dino/new-book/story.md', 'worker/stories.ts']), true);
    assert.equal(siteChecksNeeded(['README.md']), true);
    assert.equal(siteChecksNeeded([]), true);
    assert.equal(siteChecksNeeded(null), true);
});
