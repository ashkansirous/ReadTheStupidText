import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { generateKeyPairSync } from 'node:crypto';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

import {
    ALL_STORES, MANUAL_STEPS, Outcome, Store, activateBody, addOnProductIds, appleAvailabilityBody, appleLocalizationBody, applePriceScheduleBody, appleProductBody, applyStore,
    findApplePricePoint, googleAssertion, createAppleProduct, createPlayProduct, PlayStage, playProductStage, parseServiceAccount, applePricePointId, applePagePath, microsoftNextUrl, microsoftAddOnBody, paidBooksFrom, parseArgs, planLines, playHasUsPrice, playProductBody, playUsdPrice, productIdProblem, readContentEntries,
    appleTerritoryIds, createMicrosoftProduct, cut, jsonOrNull,
} from './store-products.mjs';

const SCRIPT = fileURLToPath(new URL('./store-products.mjs', import.meta.url));

const entries = [
    { stem: 'lion_and_mouse', info: { title: 'The Lion and the Mouse', access: 'free' } },
    { stem: 'goldilocks', info: { title: 'Goldilocks and the Three Bears' } },
    { stem: 'red_riding_hood', info: { title: '  Red Riding Hood ', access: 'paid', productId: ' uk.sirous.dinoreader.book.rrh ' } },
    { stem: 'billy-goats-gruff', info: { title: 'Billy Goats Gruff', productId: 'uk.sirous.dinoreader.book.billy-goats-gruff' } }, // a hand-written bad id
];

test('Slice 346: only paid books are listed, in stem order, with their product id and title', () => {
    const books = paidBooksFrom(entries);
    assert.deepEqual(books.map((book) => book.stem), ['billy-goats-gruff', 'goldilocks', 'red_riding_hood']);
    const derived = paidBooksFrom([{ stem: 'ugly-duckling', info: {} }])[0]; // the derived id turns hyphens into "_"
    assert.deepEqual([derived.productId, derived.problem], ['uk.sirous.dinoreader.book.ugly_duckling', null]);
    assert.equal(books[1].productId, 'uk.sirous.dinoreader.book.goldilocks');
    assert.equal(books[2].productId, 'uk.sirous.dinoreader.book.rrh');
    assert.equal(books[2].title, 'Red Riding Hood');
    assert.equal(paidBooksFrom([{ stem: 'x', info: {} }])[0].title, 'x'); // no title: the folder name
});

test('Slice 346: a product id with a hyphen is flagged (Google Play rejects it), a clean one is not', () => {
    assert.equal(productIdProblem('uk.sirous.dinoreader.book.goldilocks'), null);
    assert.equal(productIdProblem('uk.sirous.dinoreader.book.red_riding_hood'), null);
    assert.match(productIdProblem('uk.sirous.dinoreader.book.billy-goats-gruff'), /"-" is not/);
    assert.notEqual(productIdProblem('Uk.Upper'), null);
    assert.equal(paidBooksFrom(entries)[0].problem, productIdProblem('uk.sirous.dinoreader.book.billy-goats-gruff'));
});

test('Slice 346: the command line defaults to a dry run on all three stores; a typo never becomes a real run', () => {
    assert.deepEqual(parseArgs([]), { apply: false, stores: ALL_STORES, contentRoot: 'content/dino' });
    assert.deepEqual(parseArgs(['--apply', '--store', 'google', '--store', 'apple', '--content', 'x']), { apply: true, stores: ['google', 'apple'], contentRoot: 'x' });
    assert.throws(() => parseArgs(['--aply']), /Unknown argument/);
    assert.throws(() => parseArgs(['--store', 'amazon']), /Unknown store/);
    assert.throws(() => parseArgs(['--store']), /Unknown store/);
    assert.throws(() => parseArgs(['--content', '--apply']), /needs a value/);
});

test('Slice 346: Apple bodies carry the product id, a non-consumable type, the 0.99 price point and the app', () => {
    const book = { title: 'A very long story title that must be cut for Apple', productId: 'uk.sirous.dinoreader.book.long' };
    const product = appleProductBody(book, '6825053435').data;
    assert.equal(product.attributes.inAppPurchaseType, 'NON_CONSUMABLE');
    assert.equal(product.attributes.productId, book.productId);
    assert.ok(product.attributes.name.length <= 30);
    assert.equal(product.relationships.app.data.id, '6825053435');
    assert.equal(product.attributes.familySharable, true);
    assert.equal('familyAvailable' in product.attributes, false);
    assert.ok(appleLocalizationBody(book, '77').data.attributes.description.length <= 45);
    assert.deepEqual(appleAvailabilityBody('77', ['USA', 'GBR']).data.relationships.availableTerritories.data.map((t) => t.id), ['USA', 'GBR']);
    const points = [{ id: 'p1', attributes: { customerPrice: '0.49' } }, { id: 'p99', attributes: { customerPrice: '0.99' } }];
    assert.equal(findApplePricePoint(points), 'p99');
    assert.equal(findApplePricePoint([{ id: 'p', attributes: { customerPrice: '1.99' } }]), null);
    assert.equal(findApplePricePoint(undefined), null);
    const schedule = applePriceScheduleBody('77', 'p99');
    assert.equal(schedule.included[0].relationships.inAppPurchasePricePoint.data.id, 'p99');
    assert.equal(schedule.data.relationships.manualPrices.data[0].id, schedule.included[0].id);
});

test('Slice 346: the Play product prices every converted region from the US 0.99 base and activates one buy option', () => {
    const converted = {
        convertedRegionPrices: { US: { price: playUsdPrice() }, GB: { price: { currencyCode: 'GBP', units: '0', nanos: 890000000 } } },
        convertedOtherRegionsPrice: { usdPrice: playUsdPrice(), eurPrice: { currencyCode: 'EUR', units: '1', nanos: 0 } },
    };
    assert.deepEqual(playUsdPrice(), { currencyCode: 'USD', units: '0', nanos: 990000000 });
    assert.ok(playHasUsPrice(converted));
    assert.ok(!playHasUsPrice({ convertedRegionPrices: {} }));
    const body = playProductBody({ title: 'Goldilocks', productId: 'uk.sirous.dinoreader.book.goldilocks' }, converted);
    assert.equal(body.productId, 'uk.sirous.dinoreader.book.goldilocks');
    assert.equal(body.purchaseOptions.length, 1);
    assert.deepEqual(body.purchaseOptions[0].regionalPricingAndAvailabilityConfigs.map((c) => c.regionCode), ['US', 'GB']);
    assert.equal(body.purchaseOptions[0].newRegionsConfig.eurPrice.currencyCode, 'EUR');
    assert.equal(activateBody({ productId: 'p' }).requests[0].activatePurchaseOptionRequest.purchaseOptionId, 'buy');
});

test('Slice 346: the Microsoft add-on is a durable shell for the app; the list of existing add-ons is read from value[]', () => {
    assert.deepEqual(microsoftAddOnBody({ productId: 'p' }, '9N1'), { applicationIds: ['9N1'], productId: 'p', productType: 'Durable' });
    assert.deepEqual(addOnProductIds({ value: [{ productId: 'a' }, { productId: 3 }, {}] }), ['a']);
    assert.deepEqual(addOnProductIds(null), []);
});

test('Slice 346: a Google assertion is a signed three-part token naming the scope', () => {
    const { privateKey } = generateKeyPairSync('rsa', { modulusLength: 2048 });
    const token = googleAssertion({ client_email: 'svc@x.iam', private_key: privateKey.export({ type: 'pkcs8', format: 'pem' }) }, 1000);
    const [, claims] = token.split('.');
    assert.equal(token.split('.').length, 3);
    assert.equal(JSON.parse(Buffer.from(claims, 'base64url')).scope, 'https://www.googleapis.com/auth/androidpublisher');
});

test('Slice 346: the dry-run plan says what would be created, flags a bad id, and lists the manual steps; Microsoft has the price step', () => {
    const lines = planLines(Store.Microsoft, paidBooksFrom(entries));
    assert.match(lines[0], /3 product\(s\) at 0.99 USD/);
    assert.ok(lines.some((line) => line.startsWith('  CANNOT create') && line.includes('billy-goats-gruff')));
    assert.ok(lines.some((line) => line.startsWith('  would create uk.sirous.dinoreader.book.goldilocks')));
    assert.ok(lines.some((line) => line.includes('0.99 USD')));
    for (const store of ALL_STORES) assert.ok(MANUAL_STEPS[store].length > 0);
});

test('Slice 346: applyStore creates through the seam, keeps going after a failure, and never asks for a book with a bad id', async () => {
    const books = paidBooksFrom(entries);
    const asked = [];
    const create = async (_store, _session, book) => {
        asked.push(book.stem);
        if (book.stem === 'goldilocks') throw new Error('boom');
        return Outcome.Created;
    };
    const results = await applyStore(Store.Google, books, async () => ({}), create);
    assert.deepEqual(asked, ['goldilocks', 'red_riding_hood']);
    assert.deepEqual(results.map((r) => r.outcome), [Outcome.Failed, Outcome.Failed, Outcome.Created]);
    assert.equal(results[1].message, 'boom');
});

test('Slice 346: reading the content folder skips folders without a book.json', () => {
    const fs = {
        readdirSync: () => [{ name: 'a', isDirectory: () => true }, { name: 'b', isDirectory: () => true }, { name: 'f.txt', isDirectory: () => false }],
        existsSync: (path) => path.includes('a'),
        readFileSync: () => '{"title":"A"}',
    };
    assert.deepEqual(readContentEntries('root', fs), [{ stem: 'a', info: { title: 'A' } }]);
});

test('Slice 346: run with no flag it is a dry run: it prints the plan, calls no store and needs no credentials', () => {
    const env = { PATH: process.env.PATH, SystemRoot: process.env.SystemRoot };
    const result = spawnSync(process.execPath, [SCRIPT], { env, encoding: 'utf8', cwd: fileURLToPath(new URL('../..', import.meta.url)) });
    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /^DRY RUN/);
    assert.match(result.stdout, /App Store Connect: /);
    assert.match(result.stdout, /Partner Center: /);
    assert.doesNotMatch(result.stdout, /^ {2}(created|exists|failed): /m); // only the real run prints outcomes
});

// ---- review round (s346d high) ----

const BOOK = { title: 'Goldilocks', productId: 'uk.sirous.dinoreader.book.goldilocks' };

function fakeAsc(routes) {
    const calls = [];
    const answer = (method) => async (path, body) => {
        calls.push(`${method} ${path}`);
        const key = Object.keys(routes).find((route) => `${method} ${path}`.startsWith(route));
        const value = typeof routes[key] === 'function' ? routes[key](path, body) : routes[key];
        return value ?? { status: 404, ok: false, json: null };
    };
    return { calls, get: answer('GET'), post: answer('POST') };
}

const okJson = (json) => ({ status: 200, ok: true, json });

test('Slice 346 review #2: an Apple product that exists but has no price or listing is completed, a whole one is left alone', async () => {
    const half = fakeAsc({
        'GET /v1/apps/1/inAppPurchasesV2': okJson({ data: [{ id: '77' }] }),
        'GET /v2/inAppPurchases/77/inAppPurchaseLocalizations': okJson({ data: [{ id: 'l' }] }),
        'GET /v2/inAppPurchases/77/inAppPurchaseAvailability': okJson({ data: { id: 'a' } }),
        'GET /v2/inAppPurchases/77/iapPriceSchedule': { status: 404, ok: false, json: null },
        'GET /v2/inAppPurchases/77/pricePoints': okJson({ data: [{ id: 'p99', attributes: { customerPrice: '0.99' } }] }),
        'POST /v1/inAppPurchasePriceSchedules': okJson({ data: {} }),
    });
    assert.equal(await createAppleProduct(half, BOOK, '1'), Outcome.Completed);
    assert.deepEqual(half.calls.filter((c) => c.startsWith('POST')), ['POST /v1/inAppPurchasePriceSchedules']);
    const whole = fakeAsc({
        'GET /v1/apps/1/inAppPurchasesV2': okJson({ data: [{ id: '77' }] }),
        'GET /v2/inAppPurchases/77/inAppPurchaseLocalizations': okJson({ data: [{ id: 'l' }] }),
        'GET /v2/inAppPurchases/77/inAppPurchaseAvailability': okJson({ data: { id: 'a' } }),
        'GET /v2/inAppPurchases/77/iapPriceSchedule': okJson({ data: { id: 's' } }),
    });
    assert.equal(await createAppleProduct(whole, BOOK, '1'), Outcome.Exists);
    assert.equal(whole.calls.filter((c) => c.startsWith('POST')).length, 0);
});

test('Slice 346 review #2: a Play product that is created but not active is activated; an active one is left alone; a missing one is created', async () => {
    assert.equal(playProductStage({ purchaseOptions: [{ purchaseOptionId: 'buy', state: 'ACTIVE' }] }), PlayStage.Active);
    assert.equal(playProductStage({ purchaseOptions: [{ purchaseOptionId: 'buy', state: 'DRAFT' }] }), PlayStage.Inactive);
    assert.equal(playProductStage({}), PlayStage.Missing);
    const log = [];
    const request = (existing) => async (_token, method, path) => {
        log.push(`${method} ${path.split('?')[0]}`);
        if (method === 'GET') return existing;
        if (path.startsWith('pricing')) return okJson({ convertedRegionPrices: { US: { price: playUsdPrice() } } });
        return okJson({});
    };
    assert.equal(await createPlayProduct('t', BOOK, request({ status: 200, ok: true, json: { purchaseOptions: [{ purchaseOptionId: 'buy', state: 'DRAFT' }] } })), Outcome.Completed);
    assert.ok(log.some((line) => line.includes('batchUpdateStates')) && !log.some((line) => line.startsWith('PATCH')));
    log.length = 0;
    assert.equal(await createPlayProduct('t', BOOK, request({ status: 200, ok: true, json: { purchaseOptions: [{ purchaseOptionId: 'buy', state: 'ACTIVE' }] } })), Outcome.Exists);
    assert.deepEqual(log, ['GET oneTimeProducts/uk.sirous.dinoreader.book.goldilocks']);
    log.length = 0;
    assert.equal(await createPlayProduct('t', BOOK, request({ status: 404, ok: false, json: null })), Outcome.Created);
    assert.ok(log.some((line) => line.startsWith('PATCH onetimeproducts/')) && log.some((line) => line.includes('batchUpdateStates')));
});

test('Slice 346 review #6: a bad service account JSON names only the variable, never any of its text', () => {
    const secret = '{"private_key":"-----BEGIN PRIVATE KEY-----SECRETSECRET" oops';
    assert.throws(() => parseServiceAccount(secret), (error) => error.message === 'DINOREADER_PLAY_SERVICE_ACCOUNT_JSON is not valid JSON.' && !error.message.includes('SECRET'));
    assert.throws(() => parseServiceAccount('{"a":1}'), /has no client_email and private_key/);
    assert.equal(parseServiceAccount('{"client_email":"a","private_key":"b"}').client_email, 'a');
});

test('Slice 346 review #8: the Apple price point is searched across pages, and the next page is followed only on Apple\'s own host', async () => {
    const pages = {
        1: okJson({ data: [{ id: 'p1', attributes: { customerPrice: '5.99' } }], links: { next: 'https://api.appstoreconnect.apple.com/v2/inAppPurchases/77/pricePoints?cursor=2&limit=200' } }),
        2: okJson({ data: [{ id: 'p99', attributes: { customerPrice: '0.99' } }] }),
    };
    const asc = fakeAsc({ 'GET /v2/inAppPurchases/77/pricePoints': (path) => (path.includes('cursor=2') ? pages[2] : pages[1]) });
    assert.equal(await applePricePointId(asc, '77'), 'p99');
    assert.equal(asc.calls.length, 2);
    assert.equal(applePagePath('https://api.appstoreconnect.apple.com/v2/x?cursor=2'), '/v2/x?cursor=2');
    assert.equal(applePagePath(undefined), null);
    assert.throws(() => applePagePath('https://evil.example/v2/x'), /another host/);
    const none = fakeAsc({ 'GET /v2/inAppPurchases/77/pricePoints': okJson({ data: [{ id: 'p', attributes: { customerPrice: '5.99' } }] }) });
    await assert.rejects(() => applePricePointId(none, '77'), /no 0.99 price point/);
});

test('Slice 346 review #8: the Microsoft @nextLink works absolute or relative, and never leaves Microsoft\'s host', () => {
    assert.equal(microsoftNextUrl('inappproducts?top=100&skip=100'), 'https://manage.devcenter.microsoft.com/v1.0/my/inappproducts?top=100&skip=100');
    assert.equal(microsoftNextUrl('https://manage.devcenter.microsoft.com/v1.0/my/inappproducts?skip=100'), 'https://manage.devcenter.microsoft.com/v1.0/my/inappproducts?skip=100');
    assert.equal(microsoftNextUrl(undefined), null);
    assert.throws(() => microsoftNextUrl('https://evil.example/x'), /another host/);
});

// ---- review round (s346d medium) ----

test('Slice 346 medium #1: a cut name or description ends at a whole word with no full stop added; the Apple description is fixed and fits', () => {
    assert.equal(cut('The Three Little Pigs and the Wolf', 30), 'The Three Little Pigs and the');
    assert.equal(cut('Short', 30), 'Short');
    assert.equal(cut('Exactly thirty characters long!', 31), 'Exactly thirty characters long!');
    assert.equal(cut('Supercalifragilisticexpialidocious story', 10), 'Supercalif');
    const description = appleLocalizationBody({ title: 'The Three Little Pigs and the Wolf', productId: 'p' }, '1').data.attributes.description;
    assert.ok(description.length <= 45 && !description.endsWith('..'));
    assert.equal(description, appleLocalizationBody({ title: 'x', productId: 'p' }, '1').data.attributes.description);
});

test('Slice 346 medium #2: a Microsoft add-on is matched by product id however it is cased; a 409 is re-checked against a fresh list before it is called unsupported', async () => {
    assert.deepEqual(addOnProductIds({ value: [{ productId: ' UK.Sirous.Book.Goldilocks ' }] }), ['uk.sirous.book.goldilocks']);
    const book = { productId: 'uk.sirous.book.goldilocks' };
    const conflict = async () => ({ status: 409, ok: false, json: null });
    assert.equal(await createMicrosoftProduct('t', book, new Set(['uk.sirous.book.goldilocks']), '9N1', { request: async () => assert.fail('no POST') }), Outcome.Exists);
    assert.equal(await createMicrosoftProduct('t', book, new Set(), '9N1', { request: conflict, list: async () => new Set(['uk.sirous.book.goldilocks']) }), Outcome.Exists);
    await assert.rejects(() => createMicrosoftProduct('t', book, new Set(), '9N1', { request: conflict, list: async () => new Set() }), /409.*does not support/);
    assert.equal(await createMicrosoftProduct('t', book, new Set(), '9N1', { request: async () => okJson({}) }), Outcome.Created);
});

test('Slice 346 medium #3: a body that is not JSON gives null, so the HTTP status is what is reported', () => {
    assert.equal(jsonOrNull('<html>502 Bad Gateway</html>'), null);
    assert.equal(jsonOrNull(''), null);
    assert.deepEqual(jsonOrNull('{"a":1}'), { a: 1 });
});

test('Slice 346 medium #4: two books that map to one product id both get a problem and neither is created', async () => {
    const books = paidBooksFrom([{ stem: 'a-b', info: { title: 'A' } }, { stem: 'a_b', info: { title: 'B' } }, { stem: 'c', info: {} }]);
    assert.deepEqual(books.map((book) => book.problem === null), [false, false, true]);
    assert.match(books[0].problem, /also the product id of a[-_]b/);
    const asked = [];
    await applyStore(Store.Google, books, async () => ({}), async (_s, _x, book) => { asked.push(book.stem); return Outcome.Created; });
    assert.deepEqual(asked, ['c']);
});

test('Slice 346 medium #5 (decided): every book gets the underscore id, book.json\'s own productId still wins, and no book is flagged', () => {
    const books = paidBooksFrom([{ stem: 'lion-and-mouse', info: { id: 'ABC' } }, { stem: 'ugly-duckling', info: {} }, { stem: 'x', info: { productId: 'own.id' } }]);
    assert.deepEqual(books.map((book) => book.productId), ['uk.sirous.dinoreader.book.lion_and_mouse', 'uk.sirous.dinoreader.book.ugly_duckling', 'own.id']);
    assert.ok(books.every((book) => book.problem === null));
});

test('Slice 346 medium #7: every Apple territory page is read, and a list that never ends is an error', async () => {
    const asc = fakeAsc({
        'GET /v1/territories': (path) => (path.includes('cursor=2')
            ? okJson({ data: [{ id: 'GBR' }] })
            : okJson({ data: [{ id: 'USA' }], links: { next: 'https://api.appstoreconnect.apple.com/v1/territories?cursor=2&limit=200' } })),
    });
    assert.deepEqual(await appleTerritoryIds(asc), ['USA', 'GBR']);
    const endless = fakeAsc({ 'GET /v1/territories': okJson({ data: [{ id: 'USA' }], links: { next: 'https://api.appstoreconnect.apple.com/v1/territories?cursor=9' } }) });
    await assert.rejects(() => appleTerritoryIds(endless), /more pages than expected/);
});

test('Slice 346 medium #8: a malformed book.json is named in the error', () => {
    const fs = { readdirSync: () => [{ name: 'bad', isDirectory: () => true }], existsSync: () => true, readFileSync: () => '{oops' };
    assert.throws(() => readContentEntries('root', fs), /bad.book\.json is not valid JSON/);
});

test('Slice 346 medium #9: a dry run exits non-zero when a book has an invalid product id', () => {
    const root = mkdtempSync(join(tmpdir(), 'dry-'));
    try {
        mkdirSync(join(root, 'bad'));
        writeFileSync(join(root, 'bad', 'book.json'), '{"title":"Bad","productId":"Bad-Id"}');
        const env = { PATH: process.env.PATH, SystemRoot: process.env.SystemRoot };
        const result = spawnSync(process.execPath, [SCRIPT, '--content', root], { env, encoding: 'utf8' });
        assert.equal(result.status, 1);
        assert.match(result.stdout, /CANNOT create/);
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
});
