import test from 'node:test';
import assert from 'node:assert/strict';

import { contentChanged, delistAllowed } from './content-changed.mjs';

test('a change under content/dino/ allows delisting, any other change or an unknown one does not', () => {
    assert.equal(contentChanged(['docs/a.md', 'content/dino/goldilocks/book.json']), true);
    assert.equal(contentChanged(['docs/a.md', 'src/x.cs']), false);
    assert.equal(contentChanged([]), false);
    assert.equal(contentChanged(null), false);
});

test('a manual run may delist, a push only with book content in it', () => {
    assert.equal(delistAllowed('workflow_dispatch', null), true);
    assert.equal(delistAllowed('push', ['content/dino/x/story.md']), true);
    assert.equal(delistAllowed('push', ['README.md']), false);
    assert.equal(delistAllowed('push', null), false);
});
