import test from 'node:test';
import assert from 'node:assert/strict';

import { describe, missingRecords } from './check-audio-record.mjs';

const clip = 'content/dino/goldilocks/audio/de/0001.m4a';
const track = 'content/dino/goldilocks/audio/de/listen-de-v3.m4a';
const record = 'content/dino/goldilocks/audio-cast.json';

test('Slice 409: a changed clip with its track and record in the same change is fine', () => {
    assert.deepEqual(missingRecords([clip, track, record]), []);
});

test('a changed clip without the track and the record names the book, the language and both files', () => {
    const [problem] = missingRecords([clip]);
    assert.deepEqual(problem, { book: 'goldilocks', language: 'de', missing: ['listen-de-v*.m4a', 'goldilocks/audio-cast.json'] });
    assert.match(describe(problem), /goldilocks \[de\].*build-listen-all goldilocks/);
});

test('another language\'s track does not cover the changed one, and the record alone is not enough', () => {
    assert.equal(missingRecords([clip, 'content/dino/goldilocks/audio/fr/listen-fr-v1.m4a', record]).length, 1);
    assert.deepEqual(missingRecords([clip, record])[0].missing, ['listen-de-v*.m4a']);
});

test('a change with no clips, or only a track and record, is not checked', () => {
    assert.deepEqual(missingRecords(['content/dino/goldilocks/story.md', track, record]), []);
    assert.deepEqual(missingRecords([]), []);
});
