import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { generateKeyPairSync } from 'node:crypto';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

import {
    ALL_STORES, MANUAL_STEPS, Outcome, Store, activateBody, addOnProductIds, appleAvailabilityBody, appleLocalizationBody, applePriceScheduleBody, appleProductBody, applyStore,
    findApplePricePoint, googleAssertion, createAppleProduct, createPlayProduct, PlayStage, playProductStage, parseServiceAccount, applePricePointId, applePagePath, microsoftNextUrl, microsoftAddOnBody, paidBooksFrom, parseArgs, planLines, playHasUsPrice, playProductBody, playUsdPrice, productIdProblem, readContentEntries,
    appleTerritoryIds, createMicrosoftProduct, cut, jsonOrNull, isNonJsonBody, readReviewScreenshot, appleScreenshotNeed, requireReviewScreenshot,
} from './store-products.mjs';

import {
    checkMicrosoftPrices, completeMicrosoftBook, createMicrosoftBook, itemsForStore, microsoftCatalogUrl, microsoftCommitVerdict, microsoftLivePrice, microsoftPriceProblem, microsoftRateLimitSeconds, microsoftSubmissionBody,
    microsoftSubmissionKind, MsSubmissionKind, RateLimitedError, resultOf, retryingMicrosoft, selectBooks, memoisedOnce, isEntryScript,
} from './store-products.mjs';

import { exitCodeOf, loadStoreEnv, missingKeys, parseEnvText, storeEnvFrom, isEntryScript as isLocalEntryScript } from './store-products-local.mjs';

import {
    applePlanForAvailability, appleRetryDelayMs, retryingAsc, SUBSCRIPTION_PLANS, SUBSCRIPTION_PRODUCT_ID, TRIAL_DAYS, appleSubscriptionAvailabilityBody, applePricePlan, appleSubscriptionBody, appleSubscriptionPriceBody, appleTrialBody, createAppleSubscription, createPlaySubscription, microsoftSubscriptionBody,
    retryingPlay, playPlanRegionalConfigs, playHeldRegions, playHasGbPrice, playRegionsVersion, playTaxExclusiveInput, playSubscriptionBody, playSubscriptionStage, playTrialOfferBody, subscriptionItemsFor, subscriptionPlanLines,
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
    assert.deepEqual(parseArgs([]), { apply: false, stores: ALL_STORES, contentRoot: 'content/dino', book: null, checkPrices: false });
    assert.deepEqual(parseArgs(['--apply', '--store', 'google', '--store', 'apple', '--content', 'x']), { apply: true, stores: ['google', 'apple'], contentRoot: 'x', book: null, checkPrices: false });
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
    return { calls, get: answer('GET'), post: answer('POST'), patch: answer('PATCH'), delete: answer('DELETE') };
}

const okJson = (json) => ({ status: 200, ok: true, json });

// ---- Slice 411: Microsoft book add-on completion, --book, --check-prices, the local runner ----

const MS = 'https://manage.devcenter.microsoft.com/v1.0/my';
const TEMPLATE_SUBMISSION = {
    id: 'tpl-sub', contentType: 'BookDownload', keywords: ['book'], lifetime: 'Forever', visibility: 'Private', privacyPolicyRequired: true, privacyPolicyUrl: 'https://dinoreader.sirous.uk/privacy',
    pricing: { priceId: 'Base', isAdvancedPricingModel: true, marketSpecificPricings: { LB: 'NotAvailable', EH: 'NotAvailable' } },
};
const SEAGULL = { stem: 'seagull', title: 'Love from Seagull Island', productId: 'uk.sirous.dinoreader.book.seagull' };

/** A fake Partner Center: `calls` records `METHOD path`; `answers` maps `METHOD path` to a response or a function (a list of responses is consumed in order). */
function fakeMs(answers) {
    const calls = [];
    const request = async (token, method, url, body) => {
        const key = `${method} ${url.replace(MS, '')}`;
        calls.push({ key, body });
        const answer = answers[key];
        if (answer === undefined) assert.fail(`unexpected call ${key}`);
        return Array.isArray(answer) ? answer.shift() : answer;
    };
    return { request, calls, keys: () => calls.map((call) => call.key) };
}

const pendingAddOn = (extra = {}) => okJson({ id: 'ADD1', pendingInAppProductSubmission: { id: 'sub1' }, ...extra });

test('Slice 411: a 429 waits the seconds Microsoft names and retries; waits past 10 minutes in total give up', async () => {
    assert.equal(microsoftRateLimitSeconds({ message: 'Too many requests, try again in 292 seconds' }), 292);
    assert.equal(microsoftRateLimitSeconds(null), 60);
    assert.equal(microsoftRateLimitSeconds({ message: 'try again in 5000 seconds' }), 600);
    const waits = [];
    const answers = [{ status: 429, ok: false, json: { message: 'try again in 2 seconds' } }, okJson({ done: true })];
    const retrying = retryingMicrosoft(async () => answers.shift(), { sleep: async (ms) => waits.push(ms) });
    assert.deepEqual((await retrying('t', 'GET', 'u')).json, { done: true });
    assert.deepEqual(waits, [2000]);
    const always = retryingMicrosoft(async () => ({ status: 429, ok: false, json: { message: 'try again in 300 seconds' } }), { sleep: noSleep });
    await assert.rejects(() => always('t', 'GET', 'u'), /rate limit/);
});

test('Slice 411: the submission body sets Tier1012, the en-us listing with the fixed description, the template fields and drops the read-only ones', () => {
    const body = microsoftSubmissionBody(SEAGULL, { id: 'sub1', status: 'PendingCommit', statusDetails: { errors: [] }, fileUploadUrl: 'x', pricing: { priceId: 'Base' } }, TEMPLATE_SUBMISSION);
    assert.deepEqual(body.pricing, { priceId: 'Tier1012', marketSpecificPricings: { LB: 'NotAvailable', EH: 'NotAvailable' }, sales: [] });
    assert.deepEqual(body.listings, { 'en-us': { title: 'Love from Seagull Island', description: 'A picture-book story in Dino.' } });
    assert.deepEqual([body.contentType, body.lifetime, body.visibility, body.privacyPolicyRequired, body.privacyPolicyUrl], ['BookDownload', 'Forever', 'Private', true, 'https://dinoreader.sirous.uk/privacy']);
    assert.deepEqual([body.id, body.targetPublishMode, 'status' in body, 'statusDetails' in body, 'fileUploadUrl' in body], ['sub1', 'Immediate', false, false, false]);
});

test('Slice 411: the submission body keeps the pending value of a field the template lacks', () => {
    const { keywords, ...noKeywords } = TEMPLATE_SUBMISSION; // eslint-disable-line no-unused-vars
    const body = microsoftSubmissionBody(SEAGULL, { id: 'sub1', status: 'PendingCommit', keywords: ['pending'] }, noKeywords);
    assert.deepEqual(body.keywords, ['pending']);
});

test('Slice 411 review #3: 429 waits share one 30-minute budget across requests; running it out is a RateLimitedError, and the book ends "rate limited, rerun later" with the rest of the store', async () => {
    const budget = { waited: 0 };
    const answers = () => [{ status: 429, ok: false, json: { message: 'try again in 400 seconds' } }, okJson({})];
    for (let i = 0; i < 4; i++) {
        const next = answers();
        await retryingMicrosoft(async () => next.shift(), { sleep: noSleep, budget })('t', 'GET', 'u');
    }
    assert.equal(budget.waited, 1600);
    const last = answers();
    await assert.rejects(() => retryingMicrosoft(async () => last.shift(), { sleep: noSleep, budget })('t', 'GET', 'u'), RateLimitedError);
    const calls = [];
    const results = await applyStore(Store.Microsoft, [{ ...SEAGULL, problem: null }, { ...SEAGULL, productId: 'two', problem: null }], async () => ({}), async (store, session, book) => {
        calls.push(book.productId);
        throw new RateLimitedError('Microsoft rate limit: rerun later');
    });
    assert.deepEqual(results.map((result) => result.outcome), [Outcome.RateLimited, Outcome.RateLimited]);
    assert.equal(calls.length, 1, 'the second book is not tried once the budget is spent');
    assert.match(Outcome.RateLimited, /rerun later/);
});

test('Slice 411 review #4: memoisedOnce runs once and forgets a rejected run; isEntryScript compares real paths', async () => {
    let runs = 0;
    const once = memoisedOnce(async () => ++runs);
    assert.deepEqual([await once(), await once()], [1, 1]);
    let tries = 0;
    const flaky = memoisedOnce(async () => {
        if (++tries === 1) throw new Error('boom');
        return tries;
    });
    await assert.rejects(flaky, /boom/);
    assert.equal(await flaky(), 2);
    const here = fileURLToPath(import.meta.url);
    assert.equal(isEntryScript(pathToFileURL(here).href, here), true);
    assert.equal(isLocalEntryScript(pathToFileURL(here).href, here.replace(/\\/g, '/')), true);
    assert.equal(isEntryScript(pathToFileURL(here).href, ''), false);
    assert.equal(isEntryScript(pathToFileURL(here).href, here + '.missing'), false);
});

const MS_STATUS_TABLE = [
    ['PendingCommit', MsSubmissionKind.NeedsWork],
    ...['CommitStarted', 'PreProcessing', 'Certification', 'InCertification', 'Release', 'Publishing', 'PendingPublication', 'Published'].map((status) => [status, MsSubmissionKind.InFlight]),
    ...['CommitFailed', 'Failed', 'CertificationFailed', 'ReleaseFailed', 'Canceled'].map((status) => [status, MsSubmissionKind.Failed]),
    ['Mystery', MsSubmissionKind.Unknown],
];

test('Slice 411: the status table maps every status to one kind, and every caller (pending submission, commit poll) acts on it', async () => {
    assert.equal(microsoftSubmissionKind(undefined), MsSubmissionKind.NeedsWork);
    for (const [status, kind] of MS_STATUS_TABLE) {
        assert.equal(microsoftSubmissionKind(status), kind, status);
        const api = fakeMs({
            'GET /inappproducts/ADD1': pendingAddOn(),
            'GET /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1', status }),
            'PUT /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1' }),
            'POST /inappproducts/ADD1/submissions/sub1/commit': okJson({}),
            'GET /inappproducts/ADD1/submissions/sub1/status': okJson({ status: 'Published' }),
        });
        const run = () => completeMicrosoftBook('t', SEAGULL, 'ADD1', { request: api.request, template: async () => TEMPLATE_SUBMISSION, sleep: noSleep });
        const touched = () => api.keys().some((key) => key.startsWith('PUT') || key.endsWith('/commit'));
        if (kind === MsSubmissionKind.NeedsWork) {
            assert.equal((await run()).outcome, Outcome.Completed, status);
            assert.equal(touched(), true, status);
        } else if (kind === MsSubmissionKind.InFlight) {
            const result = await run();
            assert.equal(result.outcome, Outcome.Exists, status);
            assert.match(result.message, status === 'Published' ? /already Published/ : new RegExp(`still committing \\(${status}\\), check later`));
            assert.equal(touched(), false, status);
        } else {
            await assert.rejects(run, kind === MsSubmissionKind.Failed ? new RegExp(`sub1 ended ${status}.*delete it in Partner Center, then rerun`) : /unknown status Mystery: check Partner Center/);
            assert.equal(touched(), false, status);
        }
    }
});

test('Slice 411: the commit poll: CommitStarted and PreProcessing keep polling, any later in-flight status or Published is done, a failed or unknown one throws', async () => {
    for (const status of ['Certification', 'InCertification', 'Release', 'Publishing', 'PendingPublication', 'Published']) assert.equal(microsoftCommitVerdict({ status }), 'done', status);
    for (const status of ['PendingCommit', 'CommitStarted', 'PreProcessing']) assert.equal(microsoftCommitVerdict({ status }), 'pending', status);
    const api = fakeMs({
        'GET /inappproducts/ADD1': pendingAddOn(),
        'GET /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1', status: 'PendingCommit' }),
        'PUT /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1' }),
        'POST /inappproducts/ADD1/submissions/sub1/commit': okJson({}),
        'GET /inappproducts/ADD1/submissions/sub1/status': [okJson({ status: 'CommitStarted' }), okJson({ status: 'PreProcessing' }), okJson({ status: 'Certification' })],
    });
    const result = await completeMicrosoftBook('t', SEAGULL, 'ADD1', { request: api.request, template: async () => TEMPLATE_SUBMISSION, sleep: noSleep });
    assert.match(result.message, /committed, now Certification; certification takes hours, read the price later with --check-prices/);
    assert.equal(api.keys().filter((key) => key.endsWith('/status')).length, 3);
    assert.throws(() => microsoftCommitVerdict({ status: 'CertificationFailed', statusDetails: { errors: [{ details: 'policy' }] } }), /commit failed: policy.*CertificationFailed/);
    assert.throws(() => microsoftCommitVerdict({ status: 'CommitFailed', statusDetails: { errors: [{ code: 'X', details: 'price tier unknown' }] } }), /commit failed: price tier unknown/);
    assert.throws(() => microsoftCommitVerdict({ status: 'Mystery' }), /unknown status Mystery: check Partner Center/);
});

test('Slice 411: a pending submission with no status is PUT and committed; an unknown status throws with no PUT', async () => {
    const missing = fakeMs({
        'GET /inappproducts/ADD1': pendingAddOn(),
        'GET /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1' }),
        'PUT /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1' }),
        'POST /inappproducts/ADD1/submissions/sub1/commit': okJson({}),
        'GET /inappproducts/ADD1/submissions/sub1/status': okJson({ status: 'Published' }),
    });
    assert.equal((await completeMicrosoftBook('t', SEAGULL, 'ADD1', { request: missing.request, template: async () => TEMPLATE_SUBMISSION, sleep: noSleep })).outcome, Outcome.Completed);
    assert.deepEqual(missing.keys().filter((key) => key.startsWith('PUT') || key.endsWith('/commit')).length, 2);
    const unknown = fakeMs({ 'GET /inappproducts/ADD1': pendingAddOn(), 'GET /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1', status: 'Draft' }) });
    await assert.rejects(() => completeMicrosoftBook('t', SEAGULL, 'ADD1', { request: unknown.request, template: async () => assert.fail('no template'), sleep: noSleep }), /unknown status Draft: check Partner Center/);
    assert.ok(!unknown.keys().some((key) => key.startsWith('PUT') || key.endsWith('/commit')));
});

test('Slice 411: a 409 on PUT with a missing status or a missing submission id throws and never builds a commit URL', async () => {
    const noStatus = fakeMs({
        'GET /inappproducts/ADD1': pendingAddOn(),
        'GET /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1', status: 'PendingCommit' }),
        'PUT /inappproducts/ADD1/submissions/sub1': { status: 409, ok: false, json: null },
        'GET /inappproducts/ADD1/submissions/sub1/status': okJson({}),
    });
    await assert.rejects(() => completeMicrosoftBook('t', SEAGULL, 'ADD1', { request: noStatus.request, template: async () => TEMPLATE_SUBMISSION, sleep: noSleep }), /edited in Partner Center/);
    const noId = fakeMs({ 'GET /inappproducts/ADD1': okJson({ id: 'ADD1' }), 'POST /inappproducts/ADD1/submissions': okJson({ status: 'PendingCommit' }) });
    await assert.rejects(() => completeMicrosoftBook('t', SEAGULL, 'ADD1', { request: noId.request, template: async () => TEMPLATE_SUBMISSION, sleep: noSleep }), /no id/);
    assert.ok(!noId.keys().some((key) => key.includes('undefined')));
    for (const status of ['Mystery', 'CertificationFailed']) {
        const bad = fakeMs({
            'GET /inappproducts/ADD1': pendingAddOn(),
            'GET /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1', status: 'PendingCommit' }),
            'PUT /inappproducts/ADD1/submissions/sub1': { status: 409, ok: false, json: null },
            'GET /inappproducts/ADD1/submissions/sub1/status': okJson({ status }),
        });
        await assert.rejects(() => completeMicrosoftBook('t', SEAGULL, 'ADD1', { request: bad.request, template: async () => TEMPLATE_SUBMISSION, sleep: noSleep }));
        assert.ok(!bad.keys().some((key) => key.endsWith('/commit')));
    }
});

test('Slice 411: a pending submission is PUT, committed and polled until it leaves CommitStarted', async () => {
    const api = fakeMs({
        'GET /inappproducts/ADD1': pendingAddOn(),
        'GET /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1', status: 'PendingCommit' }),
        'PUT /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1', status: 'PendingCommit' }),
        'POST /inappproducts/ADD1/submissions/sub1/commit': okJson({ status: 'CommitStarted' }),
        'GET /inappproducts/ADD1/submissions/sub1/status': [okJson({ status: 'CommitStarted' }), okJson({ status: 'Published' })],
    });
    const result = await completeMicrosoftBook('t', SEAGULL, 'ADD1', { request: api.request, template: async () => TEMPLATE_SUBMISSION, sleep: noSleep });
    assert.equal(result.outcome, Outcome.Completed);
    assert.match(result.message, /Store id ADD1, https:\/\/displaycatalog\.mp\.microsoft\.com\/v7\.0\/products\/ADD1\?market=US/);
    assert.equal(api.calls.find((call) => call.key.startsWith('PUT')).body.pricing.priceId, 'Tier1012');
    assert.equal(api.keys().filter((key) => key.endsWith('/status')).length, 2);
});

test('Slice 411: a published add-on and an already committed one are left alone; no PUT, no commit', async () => {
    const published = fakeMs({ 'GET /inappproducts/ADD1': okJson({ id: 'ADD1', lastPublishedInAppProductSubmission: { id: 'live' } }) });
    assert.equal((await completeMicrosoftBook('t', SEAGULL, 'ADD1', { request: published.request, template: async () => assert.fail('no template') })).outcome, Outcome.Exists);
    const certifying = fakeMs({ 'GET /inappproducts/ADD1': pendingAddOn(), 'GET /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1', status: 'Certification' }) });
    assert.equal((await completeMicrosoftBook('t', SEAGULL, 'ADD1', { request: certifying.request, template: async () => assert.fail('no template') })).outcome, Outcome.Exists);
});

test('Slice 411: a pending submission that already failed is an error before any PUT; a commit still running after the poll window is reported, not failed', async () => {
    const failed = fakeMs({ 'GET /inappproducts/ADD1': pendingAddOn(), 'GET /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1', status: 'CertificationFailed' }) });
    await assert.rejects(() => completeMicrosoftBook('t', SEAGULL, 'ADD1', { request: failed.request, template: async () => assert.fail('no template'), sleep: noSleep }), /sub1 ended CertificationFailed.*delete/);
    assert.ok(!failed.keys().some((key) => key.startsWith('PUT')));
    const slow = fakeMs({
        'GET /inappproducts/ADD1': pendingAddOn(),
        'GET /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1', status: 'PendingCommit' }),
        'PUT /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1' }),
        'POST /inappproducts/ADD1/submissions/sub1/commit': okJson({}),
        'GET /inappproducts/ADD1/submissions/sub1/status': { status: 200, ok: true, json: { status: 'CommitStarted' } },
    });
    const result = await completeMicrosoftBook('t', SEAGULL, 'ADD1', { request: slow.request, template: async () => TEMPLATE_SUBMISSION, sleep: noSleep });
    assert.equal(result.outcome, Outcome.Completed);
    assert.match(result.message, /still committing.*600 s, check later/);
    assert.ok(slow.keys().filter((key) => key.endsWith('/status')).length >= 120);
});

test('Slice 411: a shell with no submission gets one created; a 409 on PUT and a CommitFailed are errors, never skipped', async () => {
    const fresh = fakeMs({
        'GET /inappproducts/ADD1': okJson({ id: 'ADD1' }),
        'POST /inappproducts/ADD1/submissions': okJson({ id: 'sub9', status: 'PendingCommit' }),
        'PUT /inappproducts/ADD1/submissions/sub9': okJson({ id: 'sub9' }),
        'POST /inappproducts/ADD1/submissions/sub9/commit': okJson({}),
        'GET /inappproducts/ADD1/submissions/sub9/status': okJson({ status: 'CommitFailed', statusDetails: { errors: [{ details: 'bad price' }] } }),
    });
    await assert.rejects(() => completeMicrosoftBook('t', SEAGULL, 'ADD1', { request: fresh.request, template: async () => TEMPLATE_SUBMISSION, sleep: noSleep }), /commit failed: bad price/);
    const edited = fakeMs({
        'GET /inappproducts/ADD1': pendingAddOn(),
        'GET /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1', status: 'PendingCommit' }),
        'PUT /inappproducts/ADD1/submissions/sub1': { status: 409, ok: false, json: null },
        'GET /inappproducts/ADD1/submissions/sub1/status': okJson({ status: 'PendingCommit' }),
    });
    await assert.rejects(() => completeMicrosoftBook('t', SEAGULL, 'ADD1', { request: edited.request, template: async () => TEMPLATE_SUBMISSION, sleep: noSleep }), /edited in Partner Center.*delete/);
});

test('Slice 411 review #1 and #9: a submission still committing (CommitStarted, an unknown status, or a 409 whose status shows the commit) is reported "still committing", never PUT or committed again, never told to delete', async () => {
    for (const status of ['CommitStarted', 'PreProcessing']) {
        const api = fakeMs({ 'GET /inappproducts/ADD1': pendingAddOn(), 'GET /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1', status }) });
        const result = await completeMicrosoftBook('t', SEAGULL, 'ADD1', { request: api.request, template: async () => assert.fail('no template'), sleep: noSleep });
        assert.match(result.message, /still committing/);
        assert.ok(!api.keys().some((key) => key.startsWith('PUT') || key.endsWith('/commit')));
    }
    const lagging = fakeMs({
        'GET /inappproducts/ADD1': pendingAddOn(),
        'GET /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1', status: 'PendingCommit' }),
        'PUT /inappproducts/ADD1/submissions/sub1': { status: 409, ok: false, json: null },
        'GET /inappproducts/ADD1/submissions/sub1/status': okJson({ status: 'CommitStarted' }),
    });
    const result = await completeMicrosoftBook('t', SEAGULL, 'ADD1', { request: lagging.request, template: async () => TEMPLATE_SUBMISSION, sleep: noSleep });
    assert.match(result.message, /still committing \(CommitStarted\), check later/);
    assert.ok(!lagging.keys().some((key) => key.endsWith('/commit')));
});

test('Slice 411: a new book creates its shell, copies the newest published book add-on and reports Created; an existing pending one is Completed', async () => {
    const list = (rows) => okJson({ value: rows });
    const old = { id: 'OLD', productId: 'uk.sirous.dinoreader.book.goldilocks' };
    const newest = { id: 'NEW', productId: 'uk.sirous.dinoreader.book.tortoise' };
    const lists = [list([old, newest]), list([old, newest, { id: 'ADD1', productId: SEAGULL.productId }])];
    const api = fakeMs({
        'GET /inappproducts?top=100': lists,
        'POST /inappproducts': okJson({ id: 'ADD1' }),
        'GET /inappproducts/NEW': okJson({ id: 'NEW', lastPublishedInAppProductSubmission: { id: 'p1' } }),
        'GET /inappproducts/NEW/submissions/p1': okJson(TEMPLATE_SUBMISSION),
        'GET /inappproducts/ADD1': okJson({ id: 'ADD1' }),
        'POST /inappproducts/ADD1/submissions': okJson({ id: 'sub1', status: 'PendingCommit' }),
        'PUT /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1' }),
        'POST /inappproducts/ADD1/submissions/sub1/commit': okJson({}),
        'GET /inappproducts/ADD1/submissions/sub1/status': okJson({ status: 'PendingPublication' }),
    });
    const session = { storeAppId: '9N1' };
    const result = await createMicrosoftBook('t', SEAGULL, session, { request: api.request, sleep: noSleep, log: () => {} });
    assert.equal(result.outcome, Outcome.Created);
    assert.ok(!api.keys().includes('GET /inappproducts/OLD'), 'the newest published add-on is the template, the older one is not read');
    const pending = fakeMs({
        'GET /inappproducts?top=100': list([{ id: 'ADD1', productId: SEAGULL.productId }, newest]),
        'GET /inappproducts/NEW': okJson({ id: 'NEW', lastPublishedInAppProductSubmission: { id: 'p1' } }),
        'GET /inappproducts/NEW/submissions/p1': okJson(TEMPLATE_SUBMISSION),
        'GET /inappproducts/ADD1': pendingAddOn(),
        'GET /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1', status: 'PendingCommit' }),
        'PUT /inappproducts/ADD1/submissions/sub1': okJson({ id: 'sub1' }),
        'POST /inappproducts/ADD1/submissions/sub1/commit': okJson({}),
        'GET /inappproducts/ADD1/submissions/sub1/status': okJson({ status: 'Certification' }),
    });
    assert.equal((await createMicrosoftBook('t', SEAGULL, { storeAppId: '9N1' }, { request: pending.request, sleep: noSleep, log: () => {} })).outcome, Outcome.Completed);
});

test('Slice 411: the live price comes from the displaycatalog Purchase availability; only USD 0.99 passes', async () => {
    const catalog = (price, currency = 'USD') => ({ Products: [{ DisplaySkuAvailabilities: [{ Availabilities: [{ Actions: ['Details'] }, { Actions: ['Purchase'], OrderManagementData: { Price: { ListPrice: price, CurrencyCode: currency } } }] }] }] });
    assert.deepEqual(microsoftLivePrice(catalog(0.99)), { currency: 'USD', price: 0.99 });
    assert.equal(microsoftLivePrice({ Products: [{ DisplaySkuAvailabilities: [] }] }), null);
    assert.equal(microsoftLivePrice(null), null);
    const live = { Product: { DisplaySkuAvailabilities: [{ Availabilities: [{ Actions: ['Details', 'Purchase'], OrderManagementData: { Price: { CurrencyCode: 'USD', ListPrice: 0.99 } } }] }] } }; // v7.0 shape, read live 2026-11-18 for 9P7HG6S98TVG
    assert.deepEqual(microsoftLivePrice(live), { currency: 'USD', price: 0.99 });
    assert.equal(microsoftPriceProblem(microsoftLivePrice(live)), null);
    assert.equal(microsoftLivePrice({ Product: { DisplaySkuAvailabilities: [{ Availabilities: [{ Actions: ['Details'], OrderManagementData: { Price: { CurrencyCode: 'USD', ListPrice: 0.99 } } }] }] } }), null);
    const httpError = (status) => Object.assign(new Error(`displaycatalog answered HTTP ${status}`), { status });
    const unreadable = await checkMicrosoftPrices([SEAGULL], new Map([[SEAGULL.productId, 'ADD1']]), async () => { throw httpError(503); }, noSleep);
    assert.match(unreadable[0].unreadable, /could not read the catalog.*503/);
    assert.equal(unreadable[0].problem, null); // a catalog that cannot be read is not a wrong price
    let reads = 0;
    const retried = await checkMicrosoftPrices([SEAGULL], new Map([[SEAGULL.productId, 'ADD1']]), async () => { if (++reads === 1) throw httpError(429); return catalog(0.99); }, noSleep);
    assert.deepEqual([reads, retried[0].problem, retried[0].unreadable], [2, null, null]);
    reads = 0;
    await checkMicrosoftPrices([SEAGULL], new Map([[SEAGULL.productId, 'ADD1']]), async () => { reads++; throw httpError(503); }, noSleep);
    assert.equal(reads, 2, 'one retry only');
    reads = 0;
    await checkMicrosoftPrices([SEAGULL], new Map([[SEAGULL.productId, 'ADD1']]), async () => { reads++; throw httpError(403); }, noSleep);
    assert.equal(reads, 1, 'no retry on a 4xx');
    assert.equal(microsoftPriceProblem({ currency: 'USD', price: 0.99 }), null);
    assert.match(microsoftPriceProblem({ currency: 'USD', price: 1.99 }), /not USD 0.99/);
    assert.match(microsoftPriceProblem(null), /no live price/);
    assert.equal(microsoftCatalogUrl('9P7HG6S98TVG'), 'https://displaycatalog.mp.microsoft.com/v7.0/products/9P7HG6S98TVG?market=US&languages=en-us');
    const addOns = new Map([['uk.sirous.dinoreader.book.seagull', 'ADD1'], ['uk.sirous.dinoreader.book.goldilocks', 'GOLD']]);
    const books = [SEAGULL, { stem: 'goldilocks', title: 'G', productId: 'uk.sirous.dinoreader.book.goldilocks' }, { stem: 'x', title: 'X', productId: 'uk.sirous.dinoreader.book.x' }];
    const results = await checkMicrosoftPrices(books, addOns, async (id) => (id === 'GOLD' ? catalog(0.99) : catalog(2.99)));
    assert.deepEqual(results.map((result) => result.problem === null), [false, true, false]);
    assert.match(results[2].problem, /no add-on/);
});

test('Slice 411: --book keeps one paid book and drops the subscriptions in every store; an unknown or free stem is an error', () => {
    assert.equal(parseArgs(['--book', 'seagull']).book, 'seagull');
    assert.equal(parseArgs(['--check-prices']).checkPrices, true);
    assert.throws(() => parseArgs(['--book']), /needs a value/);
    const books = paidBooksFrom(entries);
    assert.deepEqual(selectBooks(books, 'goldilocks').map((book) => book.stem), ['goldilocks']);
    assert.equal(selectBooks(books, null).length, books.length);
    assert.throws(() => selectBooks(books, 'lion_and_mouse'), /no paid book/);
    for (const store of ALL_STORES) {
        assert.deepEqual(itemsForStore(store, books, { book: 'goldilocks' }), books);
        assert.ok(itemsForStore(store, books, { book: null }).length > books.length);
    }
    assert.deepEqual(resultOf('created'), { outcome: 'created', message: '' });
    assert.deepEqual(resultOf({ outcome: 'exists', message: 'id' }), { outcome: 'exists', message: 'id' });
});

test('Slice 411: --book dry run lists that one book with no subscription; an unknown stem exits 1', () => {
    const root = mkdtempSync(join(tmpdir(), 'book-'));
    try {
        mkdirSync(join(root, 'goldilocks'));
        writeFileSync(join(root, 'goldilocks', 'book.json'), '{"title":"Goldilocks"}');
        const env = { PATH: process.env.PATH, SystemRoot: process.env.SystemRoot };
        const dry = spawnSync(process.execPath, [SCRIPT, '--content', root, '--book', 'goldilocks'], { env, encoding: 'utf8' });
        assert.equal(dry.status, 0);
        assert.doesNotMatch(dry.stdout, /subscription/i);
        assert.match(dry.stdout, /would create uk\.sirous\.dinoreader\.book\.goldilocks/);
        const typo = spawnSync(process.execPath, [SCRIPT, '--content', root, '--book', 'nope'], { env, encoding: 'utf8' });
        assert.equal(typo.status, 1);
        assert.match(typo.stderr, /no paid book/);
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
});

test('Slice 411: the local runner reads the keys from the secrets files and maps the Microsoft names; a missing key is left out, never invented', () => {
    assert.deepEqual(parseEnvText('# c\r\nA=1\r\nB="two words"\r\nC=\'x\'\r\nnoequals\r\n'), { A: '1', B: 'two words', C: 'x' });
    const env = storeEnvFrom(
        { MS_STORE_TENANT_ID: 't', MS_STORE_CLIENT_ID: 'c', MS_STORE_CLIENT_SECRET: 's', DINOREADER_PLAY_SERVICE_ACCOUNT_JSON: 'play.json' },
        { ASC_API_KEY_P8_BASE64: 'p8', ASC_API_KEY_ID: 'kid' },
        (path) => `{"file":"${path}"}`,
    );
    assert.deepEqual(env, {
        MS_STORE_APP_ID: '9N9VHRLCZXN5', MS_SUBMISSION_TENANT_ID: 't', MS_SUBMISSION_CLIENT_ID: 'c', MS_SUBMISSION_CLIENT_SECRET: 's',
        ASC_API_KEY_P8_BASE64: 'p8', ASC_API_KEY_ID: 'kid', DINOREADER_PLAY_SERVICE_ACCOUNT_JSON: '{"file":"play.json"}',
    });
    assert.deepEqual(storeEnvFrom({}, {}, () => assert.fail('no file read')), { MS_STORE_APP_ID: '9N9VHRLCZXN5' });
});

test('Slice 411 review #6 and #7: export lines and inline comments parse; a failed or signalled child is a nonzero exit; a missing secrets file or key is named', () => {
    assert.deepEqual(parseEnvText('export A=1\nB=two # note\nC="x # not a comment" # real\nD=#hash'), { A: '1', B: 'two', C: 'x # not a comment', D: '#hash' });
    assert.equal(exitCodeOf({ status: 0 }), 0);
    assert.equal(exitCodeOf({ status: 3 }), 3);
    assert.equal(exitCodeOf({ status: null, signal: 'SIGTERM' }), 1);
    assert.equal(exitCodeOf({ status: null, error: new Error('spawn failed') }), 1);
    assert.deepEqual(missingKeys({}, ['--check-prices']), ['MS_SUBMISSION_TENANT_ID', 'MS_SUBMISSION_CLIENT_ID', 'MS_SUBMISSION_CLIENT_SECRET']);
    assert.equal(missingKeys({}, ['--apply']).includes('ASC_API_KEY_ID'), true);
    const root = mkdtempSync(join(tmpdir(), 'secrets-'));
    try {
        assert.throws(() => loadStoreEnv(root), /secrets file .*does not exist/);
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
});

test('Slice 411: loadStoreEnv reads a secrets folder from SECRETS_ROOT and the Play file relative to the dinoreader folder', () => {
    const root = mkdtempSync(join(tmpdir(), 'secrets-'));
    try {
        mkdirSync(join(root, 'dinoreader'));
        mkdirSync(join(root, 'lets-call-mom'));
        writeFileSync(join(root, 'dinoreader', 'secrets.env'), 'MS_STORE_TENANT_ID=tt\nDINOREADER_PLAY_SERVICE_ACCOUNT_JSON=sa.json\n');
        writeFileSync(join(root, 'dinoreader', 'sa.json'), '{"client_email":"x"}');
        writeFileSync(join(root, 'lets-call-mom', 'secrets.env'), 'ASC_API_KEY_ID=kk\n');
        const env = loadStoreEnv(root);
        assert.deepEqual([env.MS_SUBMISSION_TENANT_ID, env.ASC_API_KEY_ID, env.DINOREADER_PLAY_SERVICE_ACCOUNT_JSON], ['tt', 'kk', '{"client_email":"x"}']);
        rmSync(join(root, 'lets-call-mom'), { recursive: true, force: true });
        rmSync(join(root, 'dinoreader', 'sa.json'));
        const microsoftOnly = loadStoreEnv(root, true); // --check-prices: neither the Apple file nor the Play file is read
        assert.deepEqual([microsoftOnly.MS_SUBMISSION_TENANT_ID, microsoftOnly.ASC_API_KEY_ID, microsoftOnly.DINOREADER_PLAY_SERVICE_ACCOUNT_JSON], ['tt', undefined, undefined]);
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
});

/** Routes of a product (id 77) whose screenshot is COMPLETE and which is already in review (Slice 416): the earlier steps' tests need nothing more. */
const SCREENSHOT_DONE = {
    'GET /v2/inAppPurchases/77/appStoreReviewScreenshot': { status: 200, ok: true, json: { data: { id: 'sc', attributes: { assetDeliveryState: { state: 'COMPLETE' } } } } },
    'GET /v2/inAppPurchases/77': { status: 200, ok: true, json: { data: { attributes: { state: 'WAITING_FOR_REVIEW' } } } },
};

test('Slice 346 review #2: an Apple product that exists but has no price or listing is completed, a whole one is left alone', async () => {
    const half = fakeAsc({
        'GET /v1/apps/1/inAppPurchasesV2': okJson({ data: [{ id: '77' }] }),
        'GET /v2/inAppPurchases/77/inAppPurchaseLocalizations': okJson({ data: [{ id: 'l' }] }),
        'GET /v2/inAppPurchases/77/inAppPurchaseAvailability': okJson({ data: { id: 'a' } }),
        'GET /v2/inAppPurchases/77/iapPriceSchedule': { status: 404, ok: false, json: null },
        'GET /v2/inAppPurchases/77/pricePoints': okJson({ data: [{ id: 'p99', attributes: { customerPrice: '0.99' } }] }),
        'POST /v1/inAppPurchasePriceSchedules': okJson({ data: {} }),
        ...SCREENSHOT_DONE,
    });
    assert.equal(await createAppleProduct(half, BOOK, '1'), Outcome.Completed);
    assert.deepEqual(half.calls.filter((c) => c.startsWith('POST')), ['POST /v1/inAppPurchasePriceSchedules']);
    const whole = fakeAsc({
        'GET /v1/apps/1/inAppPurchasesV2': okJson({ data: [{ id: '77' }] }),
        'GET /v2/inAppPurchases/77/inAppPurchaseLocalizations': okJson({ data: [{ id: 'l' }] }),
        'GET /v2/inAppPurchases/77/inAppPurchaseAvailability': okJson({ data: { id: 'a' } }),
        'GET /v2/inAppPurchases/77/iapPriceSchedule': okJson({ data: { id: 's' } }),
        ...SCREENSHOT_DONE,
    });
    assert.equal(await createAppleProduct(whole, BOOK, '1'), Outcome.Exists);
    assert.equal(whole.calls.filter((c) => c.startsWith('POST')).length, 0);
});

// ---- Slice 416: review screenshot and the product's own submission ----

const IMAGE = Buffer.from('png-bytes');
const OP = { method: 'PUT', url: 'https://up.example/1', offset: 0, length: IMAGE.length, requestHeaders: [{ name: 'Content-Type', value: 'image/png' }] };

function completeProduct(extra) {
    return {
        'GET /v1/apps/1/inAppPurchasesV2?filter[productId]': okJson({ data: [{ id: '77' }] }),
        'GET /v2/inAppPurchases/77/inAppPurchaseLocalizations': okJson({ data: [{ id: 'l' }] }),
        'GET /v2/inAppPurchases/77/inAppPurchaseAvailability': okJson({ data: { id: 'a' } }),
        'GET /v2/inAppPurchases/77/iapPriceSchedule': okJson({ data: { id: 's' } }),
        ...extra,
    };
}

test('Slice 416: a missing review screenshot is reserved, uploaded and committed with the md5; the image is read only then', async () => {
    const puts = [];
    let reads = 0;
    const asc = fakeAsc(completeProduct({
        'GET /v2/inAppPurchases/77/appStoreReviewScreenshot': { status: 404, ok: false, json: null },
        'POST /v1/inAppPurchaseAppStoreReviewScreenshots': (path, body) => ({ status: 201, ok: true, json: { data: { id: 'sc9', attributes: { uploadOperations: [OP] } }, sent: body } }),
        'PATCH /v1/inAppPurchaseAppStoreReviewScreenshots/sc9': (path, body) => okJson({ data: { attributes: { assetDeliveryState: { state: 'UPLOAD_COMPLETE' } } }, sent: body }),
        'GET /v2/inAppPurchases/77': okJson({ data: { attributes: { state: 'WAITING_FOR_REVIEW' } } }),
    }));
    const io = { readImage: () => { reads++; return IMAGE; }, put: async (operation, bytes) => { puts.push([operation.url, bytes.toString()]); } };
    assert.equal(await createAppleProduct(asc, BOOK, '1', io), Outcome.Completed);
    assert.deepEqual(puts, [['https://up.example/1', 'png-bytes']]);
    assert.equal(reads, 1);
    assert.deepEqual(asc.calls.filter((c) => c.startsWith('POST') || c.startsWith('PATCH')), ['POST /v1/inAppPurchaseAppStoreReviewScreenshots', 'PATCH /v1/inAppPurchaseAppStoreReviewScreenshots/sc9']);
});

test('Slice 416: a COMPLETE (or processing) screenshot is left alone and the image file is never read', async () => {
    const asc = fakeAsc(completeProduct(SCREENSHOT_DONE));
    const io = { readImage: () => assert.fail('must not read'), put: () => assert.fail('must not upload') };
    assert.equal(await createAppleProduct(asc, BOOK, '1', io), Outcome.Exists);
    assert.equal(appleScreenshotNeed({ attributes: { assetDeliveryState: { state: 'UPLOAD_COMPLETE' } } }).action, 'none');
    assert.equal(appleScreenshotNeed({ id: 'x', attributes: { assetDeliveryState: { state: 'AWAITING_UPLOAD' }, uploadOperations: [{ url: 'u' }] } }).action, 'upload');
    assert.equal(appleScreenshotNeed({ id: 'x', attributes: { assetDeliveryState: { state: 'AWAITING_UPLOAD' } } }).action, 'replace'); // check pass #4: nothing to PUT, so re-reserve
});

test('Slice 416: a failed upload is reported with the reason, and a missing image file fails the step with a clear message', async () => {
    const failing = fakeAsc(completeProduct({
        'POST /v1/inAppPurchaseAppStoreReviewScreenshots': okJson({ data: { id: 'sc9', attributes: { uploadOperations: [OP] } } }),
        'PATCH /v1/inAppPurchaseAppStoreReviewScreenshots/sc9': okJson({ data: { attributes: { assetDeliveryState: { state: 'FAILED', errors: [{ description: 'bad image' }] } } } }),
    }));
    await assert.rejects(createAppleProduct(failing, BOOK, '1', { readImage: () => IMAGE, put: async () => {} }), /review screenshot upload failed: bad image/);
    assert.throws(() => readReviewScreenshot(join(tmpdir(), 'no-such-dir-416', 'x.png')), /does not exist; capture the in-app Store screen/);
    const put = fakeAsc(completeProduct({ 'POST /v1/inAppPurchaseAppStoreReviewScreenshots': okJson({ data: { id: 'sc9', attributes: { uploadOperations: [OP] } } }) }));
    await assert.rejects(createAppleProduct(put, BOOK, '1', { readImage: () => IMAGE, put: async () => { throw new Error('upload the review screenshot: HTTP 403'); } }), /HTTP 403/);
});

test('Slice 416: a READY_TO_SUBMIT product is submitted when the app has an approved purchase; a refusal carries Apple\'s message; none approved means no submission', async () => {
    const ready = { 'GET /v2/inAppPurchases/77': okJson({ data: { attributes: { state: 'READY_TO_SUBMIT' } } }) };
    const ok = fakeAsc(completeProduct({ ...SCREENSHOT_DONE, ...ready, 'GET /v1/apps/1/inAppPurchasesV2?filter[state]=APPROVED': okJson({ data: [{ id: 'old' }] }), 'POST /v1/inAppPurchaseSubmissions': { status: 201, ok: true, json: {} } }));
    assert.equal(await createAppleProduct(ok, BOOK, '1', {}), Outcome.Completed);
    assert.ok(ok.calls.includes('POST /v1/inAppPurchaseSubmissions'));
    const refusal = { status: 409, ok: false, json: { errors: [{ detail: 'The product is missing a screenshot.' }] } };
    const refused = fakeAsc(completeProduct({ ...SCREENSHOT_DONE, ...ready, 'GET /v1/apps/1/inAppPurchasesV2?filter[state]=APPROVED': okJson({ data: [{ id: 'old' }] }), 'POST /v1/inAppPurchaseSubmissions': refusal }));
    await assert.rejects(createAppleProduct(refused, BOOK, '1', {}), /submit the product for review: The product is missing a screenshot\./);
    const none = fakeAsc(completeProduct({ ...SCREENSHOT_DONE, ...ready, 'GET /v1/apps/1/inAppPurchasesV2?filter[state]=APPROVED': okJson({ data: [] }) }));
    assert.equal(await createAppleProduct(none, BOOK, '1', {}), Outcome.Exists);
    assert.equal(none.calls.some((c) => c.startsWith('POST')), false);
});

test('Slice 416 review #2: a FAILED screenshot is deleted and re-uploaded; an unknown state is left alone', async () => {
    const failed = { 'GET /v2/inAppPurchases/77/appStoreReviewScreenshot': okJson({ data: { id: 'bad', attributes: { assetDeliveryState: { state: 'FAILED', errors: [{ code: 'IMAGE_INCORRECT_SIZE' }] } } } }) };
    const asc = fakeAsc(completeProduct({
        ...failed,
        'DELETE /v1/inAppPurchaseAppStoreReviewScreenshots/bad': { status: 204, ok: true, json: null },
        'POST /v1/inAppPurchaseAppStoreReviewScreenshots': okJson({ data: { id: 'sc9', attributes: { uploadOperations: [OP] } } }),
        'PATCH /v1/inAppPurchaseAppStoreReviewScreenshots/sc9': okJson({ data: { attributes: { assetDeliveryState: { state: 'UPLOAD_COMPLETE' } } } }),
        'GET /v2/inAppPurchases/77': okJson({ data: { attributes: { state: 'WAITING_FOR_REVIEW' } } }),
    }));
    assert.equal(await createAppleProduct(asc, BOOK, '1', { readImage: () => IMAGE, put: async () => {} }), Outcome.Completed);
    assert.deepEqual(asc.calls.filter((c) => /^(DELETE|POST|PATCH)/.test(c)), ['DELETE /v1/inAppPurchaseAppStoreReviewScreenshots/bad', 'POST /v1/inAppPurchaseAppStoreReviewScreenshots', 'PATCH /v1/inAppPurchaseAppStoreReviewScreenshots/sc9']);
    const refused = fakeAsc(completeProduct({ ...failed, 'DELETE /v1/inAppPurchaseAppStoreReviewScreenshots/bad': { status: 403, ok: false, json: { errors: [{ detail: 'no' }] } } }));
    await assert.rejects(createAppleProduct(refused, BOOK, '1', { readImage: () => IMAGE }), /delete the failed review screenshot \(IMAGE_INCORRECT_SIZE\): no/);
    assert.equal(appleScreenshotNeed({ id: 'x', attributes: { assetDeliveryState: { state: 'SOMETHING_NEW' }, uploadOperations: [{ url: 'u' }] } }).action, 'none'); // check2 #1: unknown states are left alone
    assert.equal(appleScreenshotNeed({ id: 'x', attributes: {} }).action, 'none'); // check2 #2: never delete a screenshot whose state is not reported
    const failedNeed = appleScreenshotNeed({ id: 'f', attributes: { assetDeliveryState: { state: 'FAILED', errors: [{ code: 'E' }] } } });
    assert.deepEqual([failedNeed.screenshot.id, failedNeed.message], ['f', 'E']); // check2 #3
    assert.equal(appleScreenshotNeed(null).action, 'reserve');
});

test('Slice 416 review #1: a failed approved-purchase lookup is not remembered, the next book asks again', async () => {
    const ready = { 'GET /v2/inAppPurchases/77': okJson({ data: { attributes: { state: 'READY_TO_SUBMIT' } } }) };
    const answers = [{ status: 500, ok: false, json: { errors: [{ detail: 'busy' }] } }, okJson({ data: [{ id: 'old' }] })];
    const asc = fakeAsc(completeProduct({ ...SCREENSHOT_DONE, ...ready, 'GET /v1/apps/1/inAppPurchasesV2?filter[state]=APPROVED': () => answers.shift(), 'POST /v1/inAppPurchaseSubmissions': { status: 201, ok: true, json: {} } }));
    await assert.rejects(createAppleProduct(asc, BOOK, '1', {}), /look up approved products/);
    assert.equal(await createAppleProduct(asc, BOOK, '1', {}), Outcome.Completed);
});

test('Slice 416 review #4: a screenshot a former run uploaded and Apple still processes is waited for, though this run uploaded nothing', async () => {
    const states = ['MISSING_METADATA', 'READY_TO_SUBMIT'];
    const waits = [];
    const asc = fakeAsc(completeProduct({
        ...SCREENSHOT_DONE,
        'GET /v2/inAppPurchases/77': () => okJson({ data: { attributes: { state: states.shift() ?? 'READY_TO_SUBMIT' } } }),
        'GET /v1/apps/1/inAppPurchasesV2?filter[state]=APPROVED': okJson({ data: [{ id: 'old' }] }),
        'POST /v1/inAppPurchaseSubmissions': { status: 201, ok: true, json: {} },
    }));
    await createAppleProduct(asc, BOOK, '1', { notes: [], sleep: async (ms) => { waits.push(ms); } });
    assert.ok(asc.calls.includes('POST /v1/inAppPurchaseSubmissions'));
    assert.deepEqual(waits, [5000]);
});

test('Slice 416 review #7: the Apple store fails before any call when a book needs the review screenshot and the PNG is missing', () => {
    const book = { ...BOOK, problem: null };
    const missing = join(tmpdir(), 'no-such-dir-s416', 'shot.png');
    assert.throws(() => requireReviewScreenshot([book], () => readReviewScreenshot(missing)), /does not exist/);
    assert.doesNotThrow(() => requireReviewScreenshot([{ ...book, kind: 'subscription' }], () => assert.fail('subscriptions need no screenshot')));
});

test('Slice 416 review #3: after an upload the state is re-read until READY_TO_SUBMIT, then submitted; if it never gets there a NOT DONE note says the next run submits', async () => {
    const states = ['MISSING_METADATA', 'MISSING_METADATA', 'READY_TO_SUBMIT'];
    const waits = [];
    const slow = fakeAsc(completeProduct({
        'GET /v2/inAppPurchases/77/appStoreReviewScreenshot': { status: 404, ok: false, json: null },
        'POST /v1/inAppPurchaseAppStoreReviewScreenshots': okJson({ data: { id: 'sc9', attributes: { uploadOperations: [OP] } } }),
        'PATCH /v1/inAppPurchaseAppStoreReviewScreenshots/sc9': okJson({ data: { attributes: { assetDeliveryState: { state: 'UPLOAD_COMPLETE' } } } }),
        'GET /v2/inAppPurchases/77': () => okJson({ data: { attributes: { state: states.shift() ?? 'READY_TO_SUBMIT' } } }),
        'GET /v1/apps/1/inAppPurchasesV2?filter[state]=APPROVED': okJson({ data: [{ id: 'old' }] }),
        'POST /v1/inAppPurchaseSubmissions': { status: 201, ok: true, json: {} },
    }));
    const io = { readImage: () => IMAGE, put: async () => {}, notes: [], sleep: async (ms) => { waits.push(ms); } };
    await createAppleProduct(slow, BOOK, '1', io);
    assert.ok(slow.calls.includes('POST /v1/inAppPurchaseSubmissions'));
    assert.deepEqual(waits, [5000, 5000]);
    assert.deepEqual(io.notes, []);
    const stuck = fakeAsc(completeProduct({
        'GET /v2/inAppPurchases/77/appStoreReviewScreenshot': { status: 404, ok: false, json: null },
        'POST /v1/inAppPurchaseAppStoreReviewScreenshots': okJson({ data: { id: 'sc9', attributes: { uploadOperations: [OP] } } }),
        'PATCH /v1/inAppPurchaseAppStoreReviewScreenshots/sc9': okJson({ data: { attributes: { assetDeliveryState: { state: 'UPLOAD_COMPLETE' } } } }),
        'GET /v2/inAppPurchases/77': okJson({ data: { attributes: { state: 'MISSING_METADATA' } } }),
    }));
    const stuckIo = { readImage: () => IMAGE, put: async () => {}, notes: [], sleep: async () => {} };
    await createAppleProduct(stuck, BOOK, '1', stuckIo);
    assert.equal(stuck.calls.filter((c) => c === 'GET /v2/inAppPurchases/77').length, 6);
    assert.match(stuckIo.notes[0], /uk\.sirous\.dinoreader\.book\.goldilocks: not submitted.*MISSING_METADATA.*next run submits it/);
});

test('Slice 416 review #4 and #5: every skipped submit says why, an in-review product says nothing, the approved-purchase lookup runs once per client', async () => {
    const ready = { 'GET /v2/inAppPurchases/77': okJson({ data: { attributes: { state: 'READY_TO_SUBMIT' } } }) };
    const asc = fakeAsc(completeProduct({ ...SCREENSHOT_DONE, ...ready, 'GET /v1/apps/1/inAppPurchasesV2?filter[state]=APPROVED': okJson({ data: [] }) }));
    const notes = [];
    await createAppleProduct(asc, BOOK, '1', { notes });
    await createAppleProduct(asc, BOOK, '1', { notes });
    assert.equal(notes.length, 2);
    assert.match(notes[0], /no approved in-app purchase yet/);
    assert.equal(asc.calls.filter((c) => c.includes('filter[state]=APPROVED')).length, 1);
    const inReview = fakeAsc(completeProduct(SCREENSHOT_DONE));
    const quietNotes = [];
    await createAppleProduct(inReview, BOOK, '1', { notes: quietNotes });
    assert.deepEqual(quietNotes, []);
});

test('Slice 416 review #6: the Apple dry-run plan lists the screenshot and submission steps per book', () => {
    const lines = planLines(Store.Apple, paidBooksFrom(entries));
    assert.ok(lines.some((line) => line.includes('would add the review screenshot')));
    assert.ok(lines.some((line) => line.includes('would submit it for review')));
});

test('Slice 416: the manual steps no longer claim there is no API', () => {
    assert.doesNotMatch(MANUAL_STEPS[Store.Apple].join(' '), /no API/);
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
    assert.equal(await createPlayProduct('t', BOOK, request({ status: 404, ok: false, json: { error: { code: 404, status: 'NOT_FOUND' } } })), Outcome.Created);
    assert.ok(log.some((line) => line.startsWith('PATCH onetimeproducts/')) && log.some((line) => line.includes('batchUpdateStates')));
});

test('Slice 346 review pass 7 #6: a 200 HTML page for the create or the activate is an error, never "Created"', async () => {
    const html = { status: 200, ok: true, json: null, empty: false, nonJson: true, head: '<html>login</html>' };
    const request = (failing) => async (_token, method, path) => {
        if (method === 'GET') return { status: 404, ok: false, json: { error: { code: 404 } } };
        if (path.startsWith('pricing')) return okJson({ convertedRegionPrices: { US: { price: playUsdPrice() } } });
        return path.includes(failing) ? html : okJson({});
    };
    await assert.rejects(() => createPlayProduct('t', BOOK, request('onetimeproducts/')), /create product: answer is not JSON/);
    await assert.rejects(() => createPlayProduct('t', BOOK, request('batchUpdateStates')), /activate the buy option: answer is not JSON/);
});

test('Slice 346 E4: a non-JSON answer to the product lookup (Google\'s HTML 404 of a wrong route) fails loudly and never creates', async () => {
    const log = [];
    const request = async (_token, method, path) => {
        log.push(`${method} ${path.split('?')[0]}`);
        return { status: 404, ok: false, json: null, empty: false, nonJson: true, head: '<html>Not Found</html>' };
    };
    await assert.rejects(() => createPlayProduct('t', BOOK, request), /not JSON \(status 404, first 80 chars: "<html>Not Found<\/html>"\)/);
    assert.deepEqual(log, ['GET oneTimeProducts/uk.sirous.dinoreader.book.goldilocks']);
});

test('Slice 346 E4 (review #7): an empty body on the product lookup is not the wrong-route trap, so the product is created as before', async () => {
    const log = [];
    const request = async (_token, method, path) => {
        log.push(`${method} ${path.split('?')[0]}`);
        if (method === 'GET') return { status: 404, ok: false, json: null, empty: true, nonJson: false };
        if (path === 'pricing:convertRegionPrices') return { status: 200, ok: true, json: { convertedRegionPrices: { US: { price: { currencyCode: 'USD', units: '3' } } } }, empty: false };
        return { status: 200, ok: true, json: {}, empty: false };
    };
    assert.equal(await createPlayProduct('t', BOOK, request), Outcome.Created);
    assert.ok(log.some((line) => line.startsWith('PATCH onetimeproducts/')), 'the product was created, so the lookup was not treated as a wrong route');
});

test('Slice 346 review #3: only a body with content that is not JSON is non-JSON; empty and a real JSON null are not', () => {
    assert.equal(isNonJsonBody('<html>404</html>'), true);
    assert.equal(isNonJsonBody(''), false);
    assert.equal(isNonJsonBody('  \n'), false);
    assert.equal(isNonJsonBody('null'), false);
    assert.equal(isNonJsonBody('{"error":{}}'), false);
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

// ---- Slice 384: the subscription ----

test('Slice 384: Play gets one subscription with both plans, Apple and Microsoft one product per plan; the ids match the Worker and the app', () => {
    assert.equal(SUBSCRIPTION_PRODUCT_ID, 'uk.sirous.dinoreader.sub');
    assert.deepEqual(subscriptionItemsFor(Store.Google).map((item) => item.productId), [SUBSCRIPTION_PRODUCT_ID]);
    for (const store of [Store.Apple, Store.Microsoft]) {
        assert.deepEqual(subscriptionItemsFor(store).map((item) => item.productId), ['uk.sirous.dinoreader.sub.monthly', 'uk.sirous.dinoreader.sub.yearly']);
    }
    assert.deepEqual(SUBSCRIPTION_PLANS.map((plan) => [plan.id, plan.gbpLabel, plan.playPeriod]), [['monthly', '1.99', 'P1M'], ['yearly', '14.99', 'P1Y']]);
    assert.equal(TRIAL_DAYS, 7);
});

test('Slice 384: the Play body has a priced base plan per plan from its converted price, and the trial offer is free for 7 days for new subscribers of this subscription', () => {
    const converted = (units) => ({ convertedRegionPrices: { GB: { price: { currencyCode: 'GBP', units, nanos: 990000000 } } }, convertedOtherRegionsPrice: { usdPrice: { currencyCode: 'USD', units: '2', nanos: 0 }, eurPrice: { currencyCode: 'EUR', units: '2', nanos: 0 } } });
    const body = playSubscriptionBody({ monthly: converted('1'), yearly: converted('14') });
    assert.equal(body.productId, SUBSCRIPTION_PRODUCT_ID);
    assert.deepEqual(body.basePlans.map((plan) => [plan.basePlanId, plan.autoRenewingBasePlanType.billingPeriodDuration, plan.regionalConfigs[0].price.units]), [['monthly', 'P1M', '1'], ['yearly', 'P1Y', '14']]);
    assert.equal(body.basePlans[0].otherRegionsConfig.newSubscriberAvailability, true);
    const offer = playTrialOfferBody(SUBSCRIPTION_PLANS[0], ['GB', 'US']);
    assert.equal(offer.offerId, 'trial');
    assert.equal(offer.phases[0].duration, 'P7D');
    assert.deepEqual(offer.phases[0].regionalConfigs.map((c) => c.regionCode), ['GB', 'US']);
    assert.deepEqual(offer.phases[0].regionalConfigs[0].free, {});
    assert.deepEqual(offer.targeting, { acquisitionRule: { scope: { thisSubscription: {} } } });
});

test('Slice 384: a Play subscription is created whole, a half-made one is finished, and a live one is left alone', async () => {
    const log = [];
    const live = { basePlans: [{ basePlanId: 'monthly', state: 'ACTIVE' }, { basePlanId: 'yearly', state: 'ACTIVE' }] };
    assert.equal(playSubscriptionStage(live, { monthly: 'ACTIVE', yearly: 'ACTIVE' }), PlayStage.Active);
    assert.equal(playSubscriptionStage(live, { monthly: 'ACTIVE' }), PlayStage.Inactive);
    assert.equal(playSubscriptionStage({ basePlans: [{ basePlanId: 'monthly', state: 'ACTIVE' }] }, {}), PlayStage.Missing);
    const request = (subscription, offerState) => async (_token, method, path) => {
        log.push(`${method} ${path.split('?')[0]}`);
        if (method === 'GET' && path.includes('/offers/')) return offerState ? okJson({ state: offerState }) : { status: 404, ok: false, json: null };
        if (method === 'GET') return subscription ? okJson(subscription) : { status: 404, ok: false, json: null };
        if (path.startsWith('pricing')) return okJson({ convertedRegionPrices: { GB: { price: { currencyCode: 'GBP', units: '1', nanos: 990000000 } } } });
        return okJson({});
    };
    const [item] = subscriptionItemsFor(Store.Google);
    assert.equal(await createPlaySubscription('t', item, request(live, 'ACTIVE')), Outcome.Exists);
    assert.deepEqual(log.filter((line) => !line.startsWith('GET')), []);
    log.length = 0;
    assert.equal(await createPlaySubscription('t', item, request(null, null)), Outcome.Created);
    assert.ok(log.some((line) => line === 'POST subscriptions'), log.join('\n'));
    assert.equal(log.filter((line) => line.includes(':activate')).length, 4); // two base plans and two trial offers
    assert.equal(log.filter((line) => line.includes('/offers') && line.startsWith('POST') && !line.includes(':activate')).length, 2);
    log.length = 0;
    const draft = { basePlans: [{ basePlanId: 'monthly', state: 'DRAFT', regionalConfigs: [{ regionCode: 'GB', price: { currencyCode: 'GBP', units: '1', nanos: 990000000 } }] }, { basePlanId: 'yearly', state: 'ACTIVE', regionalConfigs: [{ regionCode: 'GB', price: { currencyCode: 'GBP', units: '14', nanos: 990000000 } }] }] };
    assert.equal(await createPlaySubscription('t', item, request(draft, 'ACTIVE')), Outcome.Completed);
    assert.deepEqual(log.filter((line) => line.startsWith('POST')), ['POST pricing:convertRegionPrices', 'POST subscriptions/uk.sirous.dinoreader.sub/basePlans/monthly:activate']); // one conversion call for the regions version, then only the activate
    assert.ok(log.some((line) => line.includes('basePlans/monthly:activate')));
});

const quiet = { sleep: async () => {}, log: () => {} };

test('Slice 384: Play subscription shapes: GB keeps the plan price, no grace override, the trial offer has one phase config per region and the same regions as the plan', () => {
    const converted = { convertedRegionPrices: { GB: { price: { currencyCode: 'GBP', units: '2', nanos: 390000000 } }, US: { price: { currencyCode: 'USD', units: '2', nanos: 490000000 } } }, convertedOtherRegionsPrice: { usdPrice: { currencyCode: 'USD', units: '2', nanos: 0 }, eurPrice: { currencyCode: 'EUR', units: '2', nanos: 0 } } };
    const plan = SUBSCRIPTION_PLANS[0];
    const regional = playPlanRegionalConfigs(plan, converted);
    assert.deepEqual(regional.map((entry) => entry.regionCode), ['GB', 'US']);
    assert.deepEqual(regional[0].price, plan.gbp); // not Play's tax-inclusive 2.39
    const body = playSubscriptionBody({ monthly: converted, yearly: converted });
    assert.deepEqual(body.basePlans[0].autoRenewingBasePlanType, { billingPeriodDuration: 'P1M', resubscribeState: 'RESUBSCRIBE_STATE_ACTIVE' });
    assert.deepEqual(body.basePlans[0].otherRegionsConfig.usdPrice, converted.convertedOtherRegionsPrice.usdPrice);
    const offer = playTrialOfferBody(plan, regional.map((entry) => entry.regionCode));
    assert.deepEqual(offer.regionalConfigs.map((c) => c.regionCode), offer.phases[0].regionalConfigs.map((c) => c.regionCode));
    assert.equal(offer.phases[0].recurrenceCount, 1);
    assert.deepEqual(offer.phases[0].otherRegionsConfig, { free: {} });
    assert.deepEqual(offer.otherRegionsConfig, { otherRegionsNewSubscriberAvailability: true });
    assert.ok(playHasGbPrice(converted) && !playHasGbPrice({ convertedRegionPrices: { US: {} } }));
    assert.deepEqual(playHeldRegions({ basePlans: [{ basePlanId: 'monthly', regionalConfigs: [{ regionCode: 'DE' }] }] }, 'monthly'), ['DE']);
    assert.throws(() => playHeldRegions({ basePlans: [] }, 'monthly'), /lists no regions for the monthly base plan/);
    assert.equal(playRegionsVersion({ regionVersion: { version: '2025/03' } }), process.env.PLAY_REGIONS_VERSION ?? '2025/03');
});

test('Slice 384: Play requests: a GET is retried after 429 or 5xx, a write only after 429, never after a 5xx', async () => {
    const sequence = (method, statuses) => {
        const calls = [];
        const raw = async () => {
            const status = statuses[calls.length] ?? 200;
            calls.push(status);
            return { status, ok: status < 300, json: {}, retryAfter: null };
        };
        return { calls, run: () => retryingPlay(raw, async () => {})('t', method, 'x') };
    };
    const get = sequence('GET', [503, 429, 200]);
    assert.equal((await get.run()).status, 200);
    assert.equal(get.calls.length, 3);
    const throttled = sequence('POST', [429, 200]);
    assert.equal((await throttled.run()).status, 200);
    assert.equal(throttled.calls.length, 2);
    const broken = sequence('POST', [503, 200]);
    assert.equal((await broken.run()).status, 503);
    assert.equal(broken.calls.length, 1);
});

test('Slice 384: Play rerun cases: a missing trial offer is created without a second subscription, a refused offer read stops, a subscription short of plans stops, one failed plan does not stop the other', async () => {
    const [item] = subscriptionItemsFor(Store.Google);
    const held = { basePlans: [{ basePlanId: 'monthly', state: 'ACTIVE', regionalConfigs: [{ regionCode: 'GB', price: { currencyCode: 'GBP', units: '1', nanos: 990000000 } }, { regionCode: 'DE' }] }, { basePlanId: 'yearly', state: 'ACTIVE', regionalConfigs: [{ regionCode: 'GB', price: { currencyCode: 'GBP', units: '14', nanos: 990000000 } }] }] };
    const posts = [];
    const make = (offers, failPath) => async (_t, method, path, body) => {
        if (method === 'GET' && path.includes('/offers/')) { const plan = path.split('/')[3]; const offer = offers[plan]; return offer === 'forbidden' ? { status: 403, ok: false, json: {} } : offer ? okJson({ state: offer }) : { status: 404, ok: false, json: {} }; }
        if (method === 'GET') return okJson(held);
        posts.push({ path, body });
        if (failPath && path.includes(failPath)) return { status: 400, ok: false, json: { error: 'bad' } };
        return path.startsWith('pricing') ? okJson({ regionVersion: { version: '2025/03' }, convertedRegionPrices: {} }) : okJson({});
    };
    assert.equal(await createPlaySubscription('t', item, make({ monthly: 'ACTIVE', yearly: undefined }), quiet), Outcome.Completed);
    const created = posts.filter((post) => post.path.includes('/offers?offerId=trial'));
    assert.equal(created.length, 1);
    assert.ok(created[0].path.includes('basePlans/yearly/offers') && created[0].path.includes(`regionsVersion.version=${encodeURIComponent(process.env.PLAY_REGIONS_VERSION ?? '2025/03')}`), 'the offer uses the version Play names (finding 2)');
    assert.equal(posts.filter((post) => post.path.startsWith('pricing')).length, 1, 'one conversion call on the held path, for the regions version only');
    assert.deepEqual(created[0].body.regionalConfigs.map((c) => c.regionCode), ['GB']);
    assert.ok(!posts.some((post) => post.path.startsWith('subscriptions?')), 'never a second subscription');
    await assert.rejects(() => createPlaySubscription('t', item, make({ monthly: 'forbidden', yearly: 'ACTIVE' }), quiet), /look up the monthly trial offer: HTTP 403/);
    const short = async (_t, method) => (method === 'GET' ? okJson({ basePlans: [held.basePlans[0]] }) : okJson({}));
    await assert.rejects(() => createPlaySubscription('t', item, short, quiet), /without all its base plans/);
    posts.length = 0;
    await assert.rejects(() => createPlaySubscription('t', item, make({ monthly: undefined, yearly: undefined }, 'basePlans/monthly/offers?'), quiet), /1 of 2 plans failed.*monthly.*create the monthly trial offer/);
    assert.ok(posts.some((post) => post.path.includes('basePlans/yearly/offers/trial:activate')), 'the yearly plan is still finished');
});

test('Slice 384: a held base plan is activated only at the plan GB price, its trial gets otherRegionsConfig only when the plan has one, and unknown regions stop the plan', async () => {
    const [item] = subscriptionItemsFor(Store.Google);
    const gb = (units, nanos) => ({ regionCode: 'GB', price: { currencyCode: 'GBP', units, nanos } });
    const yearlyLive = { basePlanId: 'yearly', state: 'ACTIVE', regionalConfigs: [gb('14', 990000000)] };
    const run = async (monthly) => {
        const posts = [];
        const request = async (_t, method, path, body) => {
            if (method === 'GET' && path.includes('/offers/')) return path.includes('/yearly/') ? okJson({ state: 'ACTIVE' }) : { status: 404, ok: false, json: {} };
            if (method === 'GET') return okJson({ basePlans: [monthly, yearlyLive] });
            posts.push({ path, body });
            return okJson({});
        };
        const outcome = await createPlaySubscription('t', item, request, quiet).catch((error) => error);
        return { outcome, posts };
    };
    const wrong = await run({ basePlanId: 'monthly', state: 'DRAFT', regionalConfigs: [gb('2', 390000000)] });
    assert.match(wrong.outcome.message, /1 of 2 plans failed.*monthly.*GB price is not 1\.99.*not activated/);
    assert.ok(!wrong.posts.some((post) => post.path.includes('activate') || post.path.includes('/offers')));
    const wrongActive = await run({ basePlanId: 'monthly', state: 'ACTIVE', regionalConfigs: [gb('2', 390000000)] }); // finding 3: an ACTIVE plan is checked too
    assert.match(wrongActive.outcome.message, /monthly.*GB price is not 1\.99/);
    assert.ok(!wrongActive.posts.some((post) => post.path.includes('/offers')));
    const noPrice = await run({ basePlanId: 'monthly', state: 'DRAFT', regionalConfigs: [{ regionCode: 'DE' }] });
    assert.match(noPrice.outcome.message, /GB price is not 1\.99/);
    const withOther = await run({ basePlanId: 'monthly', state: 'ACTIVE', regionalConfigs: [gb('1', 990000000)], otherRegionsConfig: { usdPrice: {}, eurPrice: {} } });
    assert.equal(withOther.outcome, Outcome.Completed);
    assert.deepEqual(withOther.posts.find((post) => post.path.includes('/offers?')).body.otherRegionsConfig, { otherRegionsNewSubscriberAvailability: true });
    const without = await run({ basePlanId: 'monthly', state: 'ACTIVE', regionalConfigs: [gb('1', 990000000)] });
    const body = without.posts.find((post) => post.path.includes('/offers?')).body;
    assert.equal(body.otherRegionsConfig, undefined);
    assert.equal(body.phases[0].otherRegionsConfig, undefined);
    const noRegions = await run({ basePlanId: 'monthly', state: 'ACTIVE' });
    assert.match(noRegions.outcome.message, /monthly.*GB price is not 1\.99/); // no GB price stops the plan before any region is read
    assert.ok(!noRegions.posts.some((post) => post.path.includes('/offers?')));
    assert.equal(playTrialOfferBody(SUBSCRIPTION_PLANS[0], ['GB'], false).otherRegionsConfig, undefined);
});

test('Slice 384: Play prices: other regions are converted from the tax-exclusive GB price, so GB comes back as the plan price (finding 1)', async () => {
    const [item] = subscriptionItemsFor(Store.Google);
    const vat = 1.2;
    const money = (code, amount) => ({ currencyCode: code, units: String(Math.floor(amount)), nanos: Math.round((amount % 1) * 1e9) });
    const inputs = [];
    const request = async (_t, method, path, body) => {
        if (method === 'GET') return { status: 404, ok: false, json: null };
        if (path.startsWith('pricing')) {
            const net = Number(body.price.units) + body.price.nanos / 1e9;
            inputs.push(net);
            return okJson({ convertedRegionPrices: { GB: { price: money('GBP', net * vat) }, US: { price: money('USD', net * vat * 1.25) } } });
        }
        return okJson({});
    };
    const lines = [];
    await createPlaySubscription('t', item, request, { sleep: async () => {}, log: (line) => lines.push(line) });
    assert.equal(inputs.length, 4); // per plan: the as-is conversion, then the tax-exclusive one
    assert.ok(Math.abs(inputs[0] - 1.99) < 1e-9 && Math.abs(inputs[1] - 1.99 / vat) < 1e-5, `second input ${inputs[1]}`);
    assert.ok(lines.some((line) => /monthly prices.*GB 1\.99 GBP, US 2\.49 USD/.test(line)), lines.join('\n'));
    assert.deepEqual(playTaxExclusiveInput(SUBSCRIPTION_PLANS[0], { convertedRegionPrices: {} }), null);
});

test('Slice 384: a held trial offer that is INACTIVE or UNKNOWN is left alone and reported, a DRAFT one is activated (finding 4)', async () => {
    const [item] = subscriptionItemsFor(Store.Google);
    const gb = (units) => ({ regionCode: 'GB', price: { currencyCode: 'GBP', units, nanos: 990000000 } });
    const held = { basePlans: [{ basePlanId: 'monthly', state: 'ACTIVE', regionalConfigs: [gb('1')] }, { basePlanId: 'yearly', state: 'ACTIVE', regionalConfigs: [gb('14')] }] };
    const run = async (state) => {
        const posts = [];
        const lines = [];
        const request = async (_t, method, path) => {
            if (method === 'GET' && path.includes('/offers/')) return path.includes('/yearly/') ? okJson({ state: 'ACTIVE' }) : okJson({ state });
            if (method === 'GET') return okJson(held);
            posts.push(path);
            return okJson({});
        };
        await createPlaySubscription('t', item, request, { sleep: async () => {}, log: (line) => lines.push(line) });
        return { posts, lines };
    };
    for (const state of ['INACTIVE', 'STATE_UNSPECIFIED', 'UNKNOWN']) {
        const result = await run(state);
        assert.ok(!result.posts.some((path) => path.includes('offers')), `${state} is not touched`);
        assert.ok(result.lines.some((line) => line.includes(`trial offer is ${state}; left alone`)));
    }
    assert.ok((await run('DRAFT')).posts.some((path) => path.includes('monthly/offers/trial:activate')));
});

test('Slice 384: the Apple subscription bodies carry the product id, the plan period, the price point and a one-week free trial', () => {
    const [monthly, yearly] = subscriptionItemsFor(Store.Apple);
    const body = appleSubscriptionBody(monthly, 'g1').data;
    assert.deepEqual([body.attributes.productId, body.attributes.subscriptionPeriod, body.attributes.groupLevel, body.relationships.group.data.id], ['uk.sirous.dinoreader.sub.monthly', 'ONE_MONTH', 2, 'g1']);
    assert.deepEqual([appleSubscriptionBody(yearly, 'g1').data.attributes.subscriptionPeriod, appleSubscriptionBody(yearly, 'g1').data.attributes.groupLevel], ['ONE_YEAR', 1]);
    assert.equal('availableInAllTerritories' in body.attributes, false, 'Apple rejected this attribute on the create call (2026-11-15)');
    assert.equal(appleSubscriptionPriceBody('s1', 'pp').data.relationships.subscriptionPricePoint.data.id, 'pp');
    const availability = appleSubscriptionAvailabilityBody('s1', ['GBR', 'USA']).data;
    assert.deepEqual([availability.type, availability.attributes.availableInNewTerritories, availability.relationships.subscription.data.id, availability.relationships.availableTerritories.data], ['subscriptionAvailabilities', true, 's1', [{ type: 'territories', id: 'GBR' }, { type: 'territories', id: 'USA' }]]);
    const trial = appleTrialBody('s1', 'FRA').data;
    assert.deepEqual([trial.attributes.offerMode, trial.attributes.duration, trial.attributes.numberOfPeriods, trial.relationships.territory.data.id], ['FREE_TRIAL', 'ONE_WEEK', 1, 'FRA']);
    const plan = applePricePlan('gbp', [{ id: 'fr', relationships: { territory: { data: { id: 'FRA' } } } }]);
    assert.deepEqual([...plan], [['GBR', 'gbp'], ['FRA', 'fr']]);
    assert.deepEqual(microsoftSubscriptionBody(monthly, 'store1'), { applicationIds: ['store1'], productId: monthly.productId, productType: 'Subscription' });
});

test('Slice 384: an Apple subscription is created with group, listing, price and trial; a whole one is left alone; a missing price point is an error', async () => {
    const [monthly] = subscriptionItemsFor(Store.Apple);
    const fresh = fakeAsc({
        'GET /v1/apps/1/subscriptionGroups': okJson({ data: [] }),
        'POST /v1/subscriptionGroups': okJson({ data: { id: 'g1' } }),
        'POST /v1/subscriptionGroupLocalizations': okJson({ data: {} }),
        'GET /v1/subscriptionGroups/g1/subscriptions': okJson({ data: [] }),
        'POST /v1/subscriptions': okJson({ data: { id: 's1' } }),
        'POST /v1/subscriptionLocalizations': okJson({ data: {} }),
        'GET /v1/territories': okJson({ data: [{ id: 'GBR' }, { id: 'FRA' }] }),
        'POST /v1/subscriptionAvailabilities': okJson({ data: {} }),
        'GET /v1/subscriptions/s1/subscriptionAvailability': okJson({ data: { id: 'a' } }),
        'GET /v1/subscriptionAvailabilities/a/availableTerritories': okJson({ data: [{ id: 'GBR' }, { id: 'FRA' }] }),
        'GET /v1/subscriptions/s1/pricePoints': okJson({ data: [{ id: 'p199', attributes: { customerPrice: '1.99' } }] }),
        'GET /v1/subscriptionPricePoints/p199/equalizations': okJson({ data: [{ id: 'pfr', relationships: { territory: { data: { id: 'FRA' } } } }] }),
        'POST /v1/subscriptionPrices': okJson({ data: {} }),
        'POST /v1/subscriptionIntroductoryOffers': okJson({ data: {} }),
    });
    assert.equal(await createAppleSubscription(fresh, monthly, '1', {}), Outcome.Created);
    assert.deepEqual(fresh.calls.filter((c) => c.startsWith('POST')), ['POST /v1/subscriptionGroups', 'POST /v1/subscriptionGroupLocalizations', 'POST /v1/subscriptions', 'POST /v1/subscriptionLocalizations', 'POST /v1/subscriptionAvailabilities', 'POST /v1/subscriptionPrices', 'POST /v1/subscriptionIntroductoryOffers', 'POST /v1/subscriptionPrices', 'POST /v1/subscriptionIntroductoryOffers']);
    const territoryEntries = (ids) => okJson({ data: ids.map((id) => ({ id: `x${id}`, relationships: { territory: { data: { id } } } })) });
    const whole = fakeAsc({
        'GET /v1/subscriptionGroups/g1/subscriptions': okJson({ data: [{ id: 's1', attributes: { productId: monthly.productId } }] }),
        'GET /v1/subscriptions/s1/subscriptionLocalizations': okJson({ data: [{ id: 'l' }] }),
        'GET /v1/subscriptions/s1/subscriptionAvailability': okJson({ data: { id: 'a' } }),
        'GET /v1/subscriptionAvailabilities/a/availableTerritories': okJson({ data: [{ id: 'GBR' }, { id: 'FRA' }] }),
        'GET /v1/subscriptions/s1/prices': territoryEntries(['GBR', 'FRA']),
        'GET /v1/subscriptions/s1/introductoryOffers': territoryEntries(['GBR', 'FRA']),
        'GET /v1/subscriptions/s1/pricePoints': okJson({ data: [{ id: 'p199', attributes: { customerPrice: '1.99' } }] }),
        'GET /v1/subscriptionPricePoints/p199/equalizations': okJson({ data: [{ id: 'pfr', relationships: { territory: { data: { id: 'FRA' } } } }] }),
    });
    assert.equal(await createAppleSubscription(whole, monthly, '1', { groupId: 'g1' }), Outcome.Exists);
    assert.equal(whole.calls.filter((c) => c.startsWith('POST')).length, 0);
    const half = fakeAsc({
        'GET /v1/subscriptionGroups/g1/subscriptions': okJson({ data: [{ id: 's1', attributes: { productId: monthly.productId } }] }),
        'GET /v1/subscriptions/s1/subscriptionLocalizations': okJson({ data: [{ id: 'l' }] }),
        'GET /v1/subscriptions/s1/subscriptionAvailability': okJson({ data: { id: 'a' } }),
        'GET /v1/subscriptionAvailabilities/a/availableTerritories': okJson({ data: [{ id: 'GBR' }, { id: 'FRA' }] }),
        'GET /v1/subscriptions/s1/prices': territoryEntries(['GBR']),
        'GET /v1/subscriptions/s1/introductoryOffers': territoryEntries(['GBR', 'FRA']),
        'GET /v1/subscriptions/s1/pricePoints': okJson({ data: [{ id: 'p199', attributes: { customerPrice: '1.99' } }] }),
        'GET /v1/subscriptionPricePoints/p199/equalizations': okJson({ data: [{ id: 'pfr', relationships: { territory: { data: { id: 'FRA' } } } }] }),
        'POST /v1/subscriptionPrices': okJson({ data: {} }),
    });
    assert.equal(await createAppleSubscription(half, monthly, '1', { groupId: 'g1' }), Outcome.Completed);
    assert.deepEqual(half.calls.filter((c) => c.startsWith('POST')), ['POST /v1/subscriptionPrices']);
    const noPoint = fakeAsc({ 'GET /v1/subscriptionGroups/g1/subscriptions': okJson({ data: [] }), 'POST /v1/subscriptions': okJson({ data: { id: 's1' } }), 'POST /v1/subscriptionLocalizations': okJson({ data: {} }), 'GET /v1/territories': okJson({ data: [{ id: 'GBR' }] }), 'POST /v1/subscriptionAvailabilities': okJson({ data: {} }), 'GET /v1/subscriptions/s1/pricePoints': okJson({ data: [] }) });
    await assert.rejects(createAppleSubscription(noPoint, monthly, '1', { groupId: 'g1' }), /no 1.99 GBP price point/);
});

test('Slice 384: a rerun after a failed first run finds the group, adds its missing display name and does not create a second group', async () => {
    const [monthly] = subscriptionItemsFor(Store.Apple);
    const asc = fakeAsc({
        'GET /v1/apps/1/subscriptionGroups': okJson({ data: [{ id: 'g1', attributes: { referenceName: 'Dino Book Reader subscription' } }] }),
        'GET /v1/subscriptionGroups/g1/subscriptionGroupLocalizations': okJson({ data: [] }),
        'POST /v1/subscriptionGroupLocalizations': okJson({ data: {} }),
        'GET /v1/subscriptionGroups/g1/subscriptions': okJson({ data: [{ id: 's1', attributes: { productId: monthly.productId } }] }),
        'GET /v1/subscriptions/s1/subscriptionLocalizations': okJson({ data: [{ id: 'l' }] }),
        'GET /v1/subscriptions/s1/subscriptionAvailability': okJson({ data: { id: 'a' } }),
        'GET /v1/subscriptionAvailabilities/a/availableTerritories': okJson({ data: [{ id: 'GBR' }] }),
        'GET /v1/subscriptions/s1/prices': okJson({ data: [] }),
        'GET /v1/subscriptions/s1/introductoryOffers': okJson({ data: [] }),
        'GET /v1/subscriptions/s1/pricePoints': okJson({ data: [{ id: 'p199', attributes: { customerPrice: '1.99' } }] }),
        'GET /v1/subscriptionPricePoints/p199/equalizations': okJson({ data: [] }),
        'POST /v1/subscriptionPrices': okJson({ data: {} }),
        'POST /v1/subscriptionIntroductoryOffers': okJson({ data: {} }),
    });
    assert.equal(await createAppleSubscription(asc, monthly, '1', {}), Outcome.Completed);
    assert.deepEqual(asc.calls.filter((c) => c.startsWith('POST')), ['POST /v1/subscriptionGroupLocalizations', 'POST /v1/subscriptionPrices', 'POST /v1/subscriptionIntroductoryOffers']);
});

const APPLE_NEXT = 'https://api.appstoreconnect.apple.com';
const link = (path) => ({ next: `${APPLE_NEXT}${path}` });
const territoryEntry = (id, territory) => ({ id, relationships: territory === undefined ? { territory: { links: { related: 'x' } } } : { territory: { data: { id: territory } } } });
const territoryList = (ids, extra = {}) => okJson({ data: ids.map((id) => territoryEntry(`e${id}`, id)), ...extra });

/** An existing, fully listed Apple subscription (s1) whose price, trial and equalisation answers the caller supplies. */
function existingSubscription(monthly, routes) {
    return fakeAsc({
        'GET /v1/subscriptionGroups/g1/subscriptions': okJson({ data: [{ id: 's1', attributes: { productId: monthly.productId } }] }),
        'GET /v1/subscriptions/s1/subscriptionLocalizations': okJson({ data: [{ id: 'l' }] }),
        'GET /v1/subscriptions/s1/subscriptionAvailability': okJson({ data: { id: 'a' } }),
        'GET /v1/subscriptionAvailabilities/a/availableTerritories': okJson({ data: [{ id: 'GBR' }, { id: 'FRA' }, { id: 'DEU' }] }),
        'GET /v1/subscriptions/s1/pricePoints': okJson({ data: [{ id: 'p199', attributes: { customerPrice: '1.99' } }] }),
        'GET /v1/subscriptionPricePoints/p199/equalizations': okJson({ data: [{ id: 'pfr', ...territoryEntry('pfr', 'FRA') }, { id: 'pde', ...territoryEntry('pde', 'DEU') }] }),
        'POST /v1/subscriptionPrices': okJson({ data: {} }),
        'POST /v1/subscriptionIntroductoryOffers': okJson({ data: {} }),
        ...routes,
    });
}
const noSleep = async () => {};

test('Slice 384 review #3: done-sets are read with include=territory; an entry that names no territory is not an error: nothing of that kind is posted and the end report says so', async () => {
    const [monthly] = subscriptionItemsFor(Store.Apple);
    const notes = [];
    const unplacedOffer = existingSubscription(monthly, { 'GET /v1/subscriptions/s1/prices': territoryList(['GBR', 'FRA', 'DEU']), 'GET /v1/subscriptions/s1/introductoryOffers': okJson({ data: [territoryEntry('x1', undefined)] }) });
    assert.equal(await createAppleSubscription(unplacedOffer, monthly, '1', { groupId: 'g1', sleep: noSleep, notes }), Outcome.Exists);
    assert.equal(unplacedOffer.calls.filter((c) => c.startsWith('POST')).length, 0, 'no offer is posted beside one that cannot be placed');
    assert.ok(unplacedOffer.calls.some((c) => c.includes('/v1/subscriptions/s1/introductoryOffers?include=territory')));
    assert.match(notes.join('\n'), /free-trial entry names no territory/);
    const unplacedPrice = existingSubscription(monthly, { 'GET /v1/subscriptions/s1/prices': okJson({ data: [{ id: 'x' }] }), 'GET /v1/subscriptions/s1/introductoryOffers': territoryList(['GBR', 'FRA', 'DEU']) });
    assert.equal(await createAppleSubscription(unplacedPrice, monthly, '1', { groupId: 'g1', sleep: noSleep, notes }), Outcome.Exists);
    assert.match(notes.join('\n'), /price entry names no territory/);
});

test('Slice 384 review #4: the plan covers the availability Apple holds (read back), not every territory', async () => {
    const [monthly] = subscriptionItemsFor(Store.Apple);
    const partial = existingSubscription(monthly, {
        'GET /v1/subscriptionAvailabilities/a/availableTerritories': okJson({ data: [{ id: 'GBR' }, { id: 'FRA' }] }),
        'GET /v1/subscriptions/s1/prices': okJson({ data: [] }),
        'GET /v1/subscriptions/s1/introductoryOffers': okJson({ data: [] }),
    });
    await createAppleSubscription(partial, monthly, '1', { groupId: 'g1', sleep: noSleep });
    assert.equal(partial.calls.filter((c) => c === 'POST /v1/subscriptionPrices').length, 2, 'GBR and FRA only, never DEU');
    assert.ok(partial.calls.some((c) => c.startsWith('GET /v1/subscriptionAvailabilities/a/availableTerritories')));
});

test('Slice 384 review #5: one progress line per 20 territories, with counts', async () => {
    const [monthly] = subscriptionItemsFor(Store.Apple);
    const ids = Array.from({ length: 45 }, (_, i) => `T${i}`);
    const asc = existingSubscription(monthly, {
        'GET /v1/subscriptionAvailabilities/a/availableTerritories': okJson({ data: ids.map((id) => ({ id })) }),
        'GET /v1/subscriptions/s1/prices': okJson({ data: [] }),
        'GET /v1/subscriptions/s1/introductoryOffers': okJson({ data: [] }),
        'GET /v1/subscriptionPricePoints/p199/equalizations': okJson({ data: ids.map((id) => ({ id: `p${id}`, ...territoryEntry(`p${id}`, id) })) }),
    });
    const lines = [];
    await createAppleSubscription(asc, monthly, '1', { groupId: 'g1', sleep: noSleep, log: (line) => lines.push(line) });
    assert.equal(lines.length, 2);
    assert.match(lines[0], /20 of 45 territories handled \(20 ok, 0 failed\)/);
    assert.match(lines[1], /40 of 45/);
});

test('Slice 384 review #6: the group display name is posted only when its list was read and is empty; a 404 or other failure stops with a clear message', async () => {
    const [monthly] = subscriptionItemsFor(Store.Apple);
    const group = { 'GET /v1/apps/1/subscriptionGroups': okJson({ data: [{ id: 'g1', attributes: { referenceName: 'Dino Book Reader subscription' } }] }), 'POST /v1/subscriptionGroupLocalizations': okJson({ data: {} }) };
    for (const status of [404, 500]) {
        const asc = fakeAsc({ ...group, 'GET /v1/subscriptionGroups/g1/subscriptionGroupLocalizations': { status, ok: false, json: null } });
        await assert.rejects(createAppleSubscription(asc, monthly, '1', {}), new RegExp(`could not read it \\(HTTP ${status}\\)`));
        assert.equal(asc.calls.filter((c) => c.startsWith('POST')).length, 0);
    }
    const present = fakeAsc({ ...group, 'GET /v1/subscriptionGroups/g1/subscriptionGroupLocalizations': okJson({ data: [{ id: 'l', attributes: { locale: 'en-US', name: 'Dino: Kids Books & Stories' } }] }), 'GET /v1/subscriptionGroups/g1/subscriptions': okJson({ data: [] }) });
    await assert.rejects(createAppleSubscription(present, monthly, '1', {}), /create subscription/);
    assert.equal(present.calls.includes('POST /v1/subscriptionGroupLocalizations'), false, 'a listed localization is never posted again');
    assert.equal(present.calls.some((c) => c.startsWith('PATCH')), false, 'a listing with the wanted name is not patched');
});

test('Slice 412 review #5: a live group whose listing has the old name is renamed with a PATCH, never a second group or listing', async () => {
    const [monthly] = subscriptionItemsFor(Store.Apple);
    const patched = [];
    const asc = fakeAsc({
        'GET /v1/apps/1/subscriptionGroups': okJson({ data: [{ id: 'g1', attributes: { referenceName: 'Dino Book Reader subscription' } }] }),
        'GET /v1/subscriptionGroups/g1/subscriptionGroupLocalizations': okJson({ data: [{ id: 'l1', attributes: { locale: 'en-US', name: 'Dino Book Reader' } }] }),
        'PATCH /v1/subscriptionGroupLocalizations/l1': (path, body) => { patched.push(body); return okJson({ data: {} }); },
        'GET /v1/subscriptionGroups/g1/subscriptions': okJson({ data: [] }),
    });
    await assert.rejects(createAppleSubscription(asc, monthly, '1', {}), /create subscription/);
    assert.equal(patched.length, 1);
    assert.deepEqual(patched[0], { data: { type: 'subscriptionGroupLocalizations', id: 'l1', attributes: { name: 'Dino: Kids Books & Stories' } } });
    assert.equal(asc.calls.includes('POST /v1/subscriptionGroups'), false);
    assert.equal(asc.calls.includes('POST /v1/subscriptionGroupLocalizations'), false);
});

test('Slice 412 review #1: a live group with no en-US listing gets one created, and the other locale is left alone', async () => {
    const [monthly] = subscriptionItemsFor(Store.Apple);
    const asc = fakeAsc({
        'GET /v1/apps/1/subscriptionGroups': okJson({ data: [{ id: 'g1', attributes: { referenceName: 'Dino Book Reader subscription' } }] }),
        'GET /v1/subscriptionGroups/g1/subscriptionGroupLocalizations': okJson({ data: [{ id: 'l9', attributes: { locale: 'de-DE', name: 'Dino' } }] }),
        'POST /v1/subscriptionGroupLocalizations': okJson({ data: {} }),
        'GET /v1/subscriptionGroups/g1/subscriptions': okJson({ data: [] }),
    });
    await assert.rejects(createAppleSubscription(asc, monthly, '1', {}), /create subscription/);
    assert.equal(asc.calls.includes('POST /v1/subscriptionGroupLocalizations'), true);
    assert.equal(asc.calls.some((c) => c.startsWith('PATCH')), false);
});

test('Slice 412 review #2: a rejected group rename logs a warning and the run goes on', async () => {
    const [monthly] = subscriptionItemsFor(Store.Apple);
    const lines = [];
    const asc = fakeAsc({
        'GET /v1/apps/1/subscriptionGroups': okJson({ data: [{ id: 'g1', attributes: { referenceName: 'Dino Book Reader subscription' } }] }),
        'GET /v1/subscriptionGroups/g1/subscriptionGroupLocalizations': okJson({ data: [{ id: 'l1', attributes: { locale: 'en-US', name: 'Dino Book Reader' } }] }),
        'PATCH /v1/subscriptionGroupLocalizations/l1': { status: 409, ok: false, json: null },
        'GET /v1/subscriptionGroups/g1/subscriptions': okJson({ data: [] }),
    });
    await assert.rejects(createAppleSubscription(asc, monthly, '1', { log: (line) => lines.push(line) }), /create subscription/);
    assert.ok(asc.calls.includes('GET /v1/subscriptionGroups/g1/subscriptions'), 'the run went past the rename');
    assert.ok(lines.some((line) => line.includes('WARNING') && line.includes('409')));
});

test('Slice 412 review #1: a group rename that throws (network, timeout) logs the warning and the run goes on', async () => {
    const [monthly] = subscriptionItemsFor(Store.Apple);
    const lines = [];
    const asc = fakeAsc({
        'GET /v1/apps/1/subscriptionGroups': okJson({ data: [{ id: 'g1', attributes: { referenceName: 'Dino Book Reader subscription' } }] }),
        'GET /v1/subscriptionGroups/g1/subscriptionGroupLocalizations': okJson({ data: [{ id: 'l1', attributes: { locale: 'en-US', name: 'Dino Book Reader' } }] }),
        'PATCH /v1/subscriptionGroupLocalizations/l1': () => { throw new Error('fetch timed out'); },
        'GET /v1/subscriptionGroups/g1/subscriptions': okJson({ data: [] }),
    });
    await assert.rejects(createAppleSubscription(asc, monthly, '1', { log: (line) => lines.push(line) }), /create subscription/);
    assert.ok(asc.calls.includes('GET /v1/subscriptionGroups/g1/subscriptions'), 'the run went past the rename');
    assert.ok(lines.some((line) => line.includes('WARNING') && line.includes('fetch timed out')));
});

test('Slice 384 review #1/#4/#7: prices and trials are read over every page, so a rerun posts nothing again; a list past the safety cap is an error', async () => {
    const [monthly] = subscriptionItemsFor(Store.Apple);
    const paged = (path) => (call) => (call.includes('page=2') ? territoryList(['DEU']) : territoryList(['GBR', 'FRA'], { links: link(`${path}?include=territory&page=2`) }));
    const asc = existingSubscription(monthly, {
        'GET /v1/subscriptions/s1/prices': (call) => paged('/v1/subscriptions/s1/prices')(call),
        'GET /v1/subscriptions/s1/introductoryOffers': (call) => paged('/v1/subscriptions/s1/introductoryOffers')(call),
    });
    assert.equal(await createAppleSubscription(asc, monthly, '1', { groupId: 'g1', sleep: noSleep }), Outcome.Exists);
    assert.equal(asc.calls.filter((c) => c.startsWith('POST')).length, 0);
    const endless = existingSubscription(monthly, { 'GET /v1/subscriptions/s1/prices': () => territoryList(['GBR'], { links: link('/v1/subscriptions/s1/prices?include=territory&page=2') }) });
    await assert.rejects(createAppleSubscription(endless, monthly, '1', { groupId: 'g1', sleep: noSleep }), /more than \d+ pages/);
});

test('Slice 384 review #2/#7: the GBR price point is found by territory filter over every page and must match exactly; none is a clear error', async () => {
    const [monthly] = subscriptionItemsFor(Store.Apple);
    const pointRoute = (pages) => (path) => (path.includes('page=2') ? okJson({ data: pages[1] }) : okJson({ data: pages[0], links: link('/v1/subscriptions/s1/pricePoints?filter[territory]=GBR&page=2') }));
    const late = existingSubscription(monthly, { 'GET /v1/subscriptions/s1/pricePoints': pointRoute([[{ id: 'p1', attributes: { customerPrice: '0.49' } }], [{ id: 'p199', attributes: { customerPrice: '1.99' } }]]) });
    await createAppleSubscription(late, monthly, '1', { groupId: 'g1', sleep: noSleep });
    assert.ok(late.calls.some((c) => c.startsWith('GET /v1/subscriptions/s1/pricePoints?filter[territory]=GBR')));
    const near = existingSubscription(monthly, { 'GET /v1/subscriptions/s1/pricePoints': okJson({ data: [{ id: 'p', attributes: { customerPrice: '1.9' } }, { id: 'q', attributes: { customerPrice: '2.99' } }] }) });
    await assert.rejects(createAppleSubscription(near, monthly, '1', { groupId: 'g1', sleep: noSleep }), /no 1.99 GBP price point/);
});

test('Slice 384 review #1: a territory with no equalised point is skipped and listed, the rest are priced; a point with no territory is left out of the plan', async () => {
    assert.equal(applePricePlan('gbp', [{ id: 'x', relationships: {} }]).size, 1);
    const plan = applePricePlan('gbp', [{ id: 'fr', ...territoryEntry('fr', 'FRA') }, { id: 'us', ...territoryEntry('us', 'USA') }]);
    assert.deepEqual([...applePlanForAvailability(plan, ['GBR', 'FRA']).plan], [['GBR', 'gbp'], ['FRA', 'fr']]);
    const gapped = applePlanForAvailability(plan, ['GBR', 'FRA', 'DEU', 'ITA']);
    assert.deepEqual([[...gapped.plan.keys()], gapped.skipped], [['GBR', 'FRA'], ['DEU', 'ITA']]);
    const [monthly] = subscriptionItemsFor(Store.Apple);
    const notes = [];
    const gap = existingSubscription(monthly, { 'GET /v1/subscriptions/s1/prices': okJson({ data: [] }), 'GET /v1/subscriptions/s1/introductoryOffers': okJson({ data: [] }), 'GET /v1/subscriptionPricePoints/p199/equalizations': territoryList(['FRA']) });
    assert.equal(await createAppleSubscription(gap, monthly, '1', { groupId: 'g1', sleep: noSleep, notes }), Outcome.Completed);
    assert.equal(gap.calls.filter((c) => c === 'POST /v1/subscriptionPrices').length, 2, 'GBR and FRA are priced');
    assert.match(notes.join('\n'), /1 territory\(ies\).*skipped: DEU/);
});

test('Slice 384 review #2: GETs are retried, a POST only after a 429 with Retry-After; any other POST failure is recorded, the loop goes on, and a rerun posts only the missing ones', async () => {
    const waits = [];
    let attempts = 0;
    const flaky = retryingAsc({ get: async () => (attempts++ === 0 ? { status: 429, ok: false, json: null, retryAfter: '2' } : okJson({ data: [] })), post: async () => okJson({}) }, async (ms) => waits.push(ms));
    assert.equal((await flaky.get('/x')).status, 200);
    assert.deepEqual(waits, [2000]);
    let serverErrors = 0;
    const post5xx = retryingAsc({ get: async () => okJson({}), post: async () => (serverErrors++, { status: 500, ok: false, json: null, retryAfter: null }) }, noSleep);
    assert.equal((await post5xx.post('/x')).status, 500);
    assert.equal(serverErrors, 1, 'a POST that got a 5xx may have been applied, so it is not sent again');
    let throttled = 0;
    const post429 = retryingAsc({ get: async () => okJson({}), post: async () => (throttled++ === 0 ? { status: 429, ok: false, json: null, retryAfter: '1' } : okJson({})) }, noSleep);
    assert.equal((await post429.post('/x')).status, 200);
    let bare = 0;
    const post429Bare = retryingAsc({ get: async () => okJson({}), post: async () => (bare++, { status: 429, ok: false, json: null, retryAfter: null }) }, noSleep);
    await post429Bare.post('/x');
    assert.equal(bare, 1, 'a 429 without Retry-After is not retried');
    assert.deepEqual([appleRetryDelayMs(null, 0), appleRetryDelayMs(undefined, 2), appleRetryDelayMs('0', 3), appleRetryDelayMs('9999', 0)], [1000, 4000, 0, 60000]);
    const [monthly] = subscriptionItemsFor(Store.Apple);
    let frPosts = 0;
    const failing = existingSubscription(monthly, {
        'GET /v1/subscriptions/s1/prices': okJson({ data: [] }),
        'GET /v1/subscriptions/s1/introductoryOffers': okJson({ data: [] }),
        'POST /v1/subscriptionPrices': (path, body) => (body.data.relationships.subscriptionPricePoint.data.id === 'pfr' ? (frPosts++, { status: 503, ok: false, json: null }) : okJson({ data: {} })),
    });
    await assert.rejects(createAppleSubscription(failing, monthly, '1', { groupId: 'g1', sleep: noSleep }), /1 of 3 territories failed, rerun.*FRA/);
    assert.equal(frPosts, 1, 'a failed POST is not repeated');
    assert.equal(failing.calls.filter((c) => c === 'POST /v1/subscriptionIntroductoryOffers').length, 2, 'GBR and DEU still got their trial');
    const rerun = existingSubscription(monthly, {
        'GET /v1/subscriptions/s1/prices': territoryList(['GBR', 'DEU']),
        'GET /v1/subscriptions/s1/introductoryOffers': territoryList(['GBR', 'DEU']),
    });
    assert.equal(await createAppleSubscription(rerun, monthly, '1', { groupId: 'g1', sleep: noSleep }), Outcome.Completed);
    assert.deepEqual(rerun.calls.filter((c) => c.startsWith('POST')), ['POST /v1/subscriptionPrices', 'POST /v1/subscriptionIntroductoryOffers']);
});

test('Slice 384: the dry run lists the subscription and what is left by hand for each store, and makes no store call', () => {
    for (const store of ALL_STORES) {
        const lines = subscriptionPlanLines(store).join('\n');
        assert.match(lines, /1\.99 GBP and yearly 14\.99 GBP, 7-day free trial/);
        assert.match(lines, /would create uk\.sirous\.dinoreader\.sub/);
        assert.match(lines, /by hand:/);
    }
    assert.match(subscriptionPlanLines(Store.Microsoft).join('\n'), /Partner Center/);
    const root = mkdtempSync(join(tmpdir(), 'dry-sub-'));
    try {
        mkdirSync(join(root, 'ok'));
        writeFileSync(join(root, 'ok', 'book.json'), '{"title":"Ok"}');
        const env = { PATH: process.env.PATH, SystemRoot: process.env.SystemRoot };
        const result = spawnSync(process.execPath, [SCRIPT, '--content', root], { env, encoding: 'utf8' });
        assert.equal(result.status, 0);
        assert.match(result.stdout, /subscription, monthly 1\.99 GBP and yearly 14\.99 GBP/);
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
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
