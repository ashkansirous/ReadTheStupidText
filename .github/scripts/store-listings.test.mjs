import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

import {
  APPS, LANGUAGES, buildListings, listingsFolder, findListingKey, mergeListings, parseDino, parseRtst, sectionBody, validateListing, labelValue,
} from './store-listings.mjs';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..');

const RTST_TEXT = `# Store listing: French (\`fr\`)

Search terms (Microsoft Store, 7, each <= 30 chars): lecture; tts; pdf
Short description (Play <= 80; first line of the Microsoft description): Courte.

## Long description (Microsoft Store <= 10,000 chars; Google Play <= 4,000)

Longue.

## Google Play only: download note (append to the full description)

Play note.

## Microsoft Store only: download note (append to the full description)

MS note.

## App Store description (<= 4,000)

App Store text.
`;

const DINO_TEXT = `# Store listing: French (\`fr\`), Dino Book Reader

Title (<= 30): Dino Book Reader
Play short description (<= 80): Courte.
Microsoft search terms (7): a b; c; d

## Description

Via Google Play, un adulte achete. Google Play.
`;

test('labelValue and sectionBody read one line and one section', () => {
  assert.equal(labelValue(RTST_TEXT, 'Short description'), 'Courte.');
  assert.equal(sectionBody(RTST_TEXT, 'Google Play only'), 'Play note.');
  assert.equal(sectionBody(RTST_TEXT, 'Nope'), undefined);
});

test('parseRtst joins short, long and the Microsoft note only', () => {
  const l = parseRtst(RTST_TEXT);
  assert.equal(l.title, 'Read The Stupid Text');
  assert.equal(l.description, 'Courte.\n\nLongue.\n\nMS note.');
  assert.deepEqual(l.keywords, ['lecture', 'tts', 'pdf']);
});

test('parseRtst handles CRLF files', () => {
  assert.equal(parseRtst(RTST_TEXT.replaceAll('\n', '\r\n')).description, 'Courte.\n\nLongue.\n\nMS note.');
});

test('parseDino swaps Google Play for Microsoft Store', () => {
  const l = parseDino(DINO_TEXT);
  assert.equal(l.title, 'Dino Book Reader');
  assert.equal(l.shortDescription, 'Courte.');
  assert.equal(l.description, 'Via Microsoft Store, un adulte achete. Microsoft Store.');
  assert.deepEqual(l.keywords, ['a b', 'c', 'd']);
});

test('validateListing reports missing fields and limits', () => {
  assert.deepEqual(validateListing(parseDino('# nothing')), ['missing title', 'missing shortDescription', 'missing description', 'missing keywords']);
  const ok = parseDino(DINO_TEXT);
  assert.deepEqual(validateListing(ok), []);
  assert.match(validateListing({ ...ok, keywords: Array(8).fill('x') }).join(), /8 search terms/);
  assert.match(validateListing({ ...ok, keywords: ['x'.repeat(31)] }).join(), /31 chars/);
  assert.match(validateListing({ ...ok, description: 'x'.repeat(10001) }).join(), /description is 10001/);
});

test('buildListings maps each language to its Partner Center code and names every problem', () => {
  const { listings, errors } = buildListings(APPS.dino, (stem) => (stem === 'zh' ? DINO_TEXT : stem === 'fr' ? '# empty' : undefined));
  assert.deepEqual(Object.keys(listings), ['zh-cn']);
  assert.equal(listings['zh-cn'].baseListing.title, 'Dino Book Reader');
  assert.ok(errors.includes('ar: file not found'));
  assert.ok(errors.includes('fr: missing title'));
});

const SUBMISSION = {
  Pricing: { PriceId: 'Free', Other: 1 },
  ApplicationPackages: [{ FileName: 'a.msixbundle' }],
  Listings: {
    'en-us': { BaseListing: { Title: 'Keep', Images: [{ A: 1 }], Features: ['f'] } },
    'fr': { BaseListing: { Title: 'old', ReleaseNotes: 'notes' } },
  },
};

test('mergeListings changes only the four fields of built languages', () => {
  const built = { fr: { baseListing: { title: 'T', shortDescription: 'S', description: 'D', keywords: ['k'] } }, da: { baseListing: { title: 'T2', shortDescription: 'S2', description: 'D2', keywords: ['k2'] } } };
  const { submission: out, warnings } = mergeListings(SUBMISSION, built);
  assert.deepEqual(warnings, []);
  assert.deepEqual(out.Pricing, SUBMISSION.Pricing);
  assert.deepEqual(out.ApplicationPackages, SUBMISSION.ApplicationPackages);
  assert.deepEqual(out.Listings['en-us'], SUBMISSION.Listings['en-us']);
  assert.deepEqual(out.Listings.fr.BaseListing, { Title: 'T', ReleaseNotes: 'notes', ShortDescription: 'S', Description: 'D', Keywords: ['k'] });
  assert.equal(out.Listings.da.BaseListing.Title, 'T2');
  assert.equal(SUBMISSION.Listings.fr.BaseListing.Title, 'old');
  assert.deepEqual(out.Listings.da.BaseListing.Images, [{ A: 1 }]);
});

test('Brazilian Portuguese goes to pt-br', () => {
  assert.equal(LANGUAGES.pt, 'pt-br');
});

test('findListingKey matches case-insensitively and by language', () => {
  assert.equal(findListingKey(['en-us', 'fr-FR'], 'fr'), 'fr-FR');
  assert.equal(findListingKey(['FR'], 'fr'), 'FR');
  assert.equal(findListingKey(['fr-ca', 'fr-fr'], 'fr'), 'fr-fr');
  assert.equal(findListingKey(['pt-br', 'pt-pt'], 'pt-br'), 'pt-br');
  assert.equal(findListingKey(['en-us', 'pt-pt'], 'pt-br'), 'pt-pt');
  assert.equal(findListingKey(['en-us'], 'pt-br'), undefined);
  assert.equal(findListingKey(['fr-ca', 'fr-be'], 'fr'), undefined);
  assert.equal(findListingKey(['pt'], 'pt-br'), 'pt');
  assert.equal(findListingKey(['zh-Hans'], 'zh-cn'), 'zh-Hans');
});

test('mergeListings updates an existing regional key instead of adding a bare one', () => {
  const sub = { Pricing: { PriceId: 'Free' }, Listings: { 'en-us': { BaseListing: { Images: [1] } }, 'fr-fr': { BaseListing: { Title: 'old', Images: [2] } } } };
  const built = { fr: { baseListing: { title: 'T', shortDescription: 'S', description: 'D', keywords: ['k'] } } };
  const { submission: out } = mergeListings(sub, built);
  assert.deepEqual(Object.keys(out.Listings), ['en-us', 'fr-fr']);
  assert.equal(out.Listings['fr-fr'].BaseListing.Title, 'T');
  assert.deepEqual(out.Listings['fr-fr'].BaseListing.Images, [2]);
});

test('mergeListings skips a new language with a warning when there are no English images to copy', () => {
  const sub = { Pricing: { PriceId: 'Free' }, Listings: { 'en-us': { BaseListing: { Title: 'x' } } } };
  const built = { fr: { baseListing: { title: 'T', shortDescription: 'S', description: 'D', keywords: ['k'] } } };
  const { submission: out, warnings } = mergeListings(sub, built);
  assert.equal(out.Listings.fr, undefined);
  assert.equal(warnings.length, 1);
});

test('validateListing rejects an over-limit text so the pre-publish check can fail the job', () => {
  const ok = { title: 't', shortDescription: 's', description: 'd', keywords: ['k'] };
  assert.deepEqual(validateListing(ok), []);
  assert.equal(validateListing({ ...ok, keywords: Array(8).fill('k') }).length, 1);
});

test('mergeListings matches existing casing and refuses a submission without a price', () => {
  const built = { fr: { baseListing: { title: 'T', shortDescription: 'S', description: 'D', keywords: ['k'] } } };
  const camel = { pricing: { priceId: 'Tier2' }, listings: { fr: { baseListing: { title: 'old' } } } };
  const out = mergeListings(camel, built).submission;
  assert.equal(out.listings.fr.baseListing.title, 'T');
  assert.equal(out.listings.fr.baseListing.keywords[0], 'k');
  assert.throws(() => mergeListings({ Listings: {} }, built), /PriceId/);
  assert.throws(() => mergeListings({ Pricing: { PriceId: '' } }, built), /PriceId/);
});

for (const app of Object.values(APPS)) {
  test(`the committed ${app} listing files build all ${Object.keys(LANGUAGES).length} languages within the limits`, () => {
    const folder = listingsFolder(ROOT, app);
    const { listings, errors } = buildListings(app, (stem) => readFileSync(join(folder, `${stem}.md`), 'utf8'));
    assert.deepEqual(errors, []);
    assert.equal(Object.keys(listings).length, Object.keys(LANGUAGES).length);
  });
}
