#!/usr/bin/env node
/**
 * Creates one product per paid Dino Reader book at the 0.99 USD price point in the three stores (Slice 346, Part D), through each store's API where one exists.
 * The paid books are read from the repo the way publish-store-books.mjs reads them: every `content/dino/<stem>/book.json` whose `access` is not "free" (default paid), with the
 * product id from `accessOf` (`uk.sirous.dinoreader.book.<stem>` unless book.json names one). Nothing here is saved or logged about a buyer; no secret value is ever printed.
 *
 * DRY RUN IS THE DEFAULT: with no flag the script prints what it would create, makes no network call and needs no credentials. A real run needs --apply.
 *
 * Usage: node store-products.mjs [--apply] [--store apple|google|microsoft]... [--content <folder>]
 *   --apply            really create the products (each store is asked first: a complete product is left alone, a half-created one (Apple: missing listing, availability or price; Google: not activated) is completed, so a rerun is safe)
 *   --store <name>     only this store; repeat the flag for several (default: all three)
 *   --content <folder> the books' content folder (default content/dino)
 *
 * Env for --apply (a store whose variables are missing fails that store only, naming the variable):
 *   Apple:     ASC_API_KEY_P8_BASE64, ASC_API_KEY_ID, ASC_API_ISSUER_ID (the App Store Connect API key; D:/secrets/lets-call-mom holds the key, the same three the iOS review workflow uses)
 *   Google:    DINOREADER_PLAY_SERVICE_ACCOUNT_JSON (the service account's JSON text; it needs the Play Console permission to manage products)
 *   Microsoft: MS_SUBMISSION_TENANT_ID, MS_SUBMISSION_CLIENT_ID, MS_SUBMISSION_CLIENT_SECRET (an Entra app with the Manager role in Partner Center, NOT the Store collections app of the Worker),
 *              MS_STORE_APP_ID (Dino Book Reader's Store ID)
 *   PLAY_REGIONS_VERSION (optional, default below) names the Play region table the prices are built against.
 *
 * Coverage (what an API can do, and what is left for the user, printed at the end of every run):
 *   Apple:     API creates the non-consumable product, its en-US name and description, the territory availability and the 0.99 USD price (other territories follow Apple's equalisation).
 *              No API: the review screenshot, and submitting the products for review together with an app version (App Store Connect).
 *   Google:    API creates the one-time product with its en-GB listing and one buy option priced from 0.99 USD in every region Play converts (monetization.convertRegionPrices),
 *              then activates the buy option. No API step is left, apart from the merchant/bank setup that must already be done.
 *   Microsoft: API creates the durable add-on shell only (Store submission API, create add-on). No API for the price: an add-on on Pricing Version 2 makes the API answer an unknown tier,
 *              so its price (0.99 USD), listing and submission are set in Partner Center by hand, and the Partner Center payout and tax profile must exist.
 *
 * Run live on 2026-11-13 against all three stores (Play: 11 created and activated; Apple: 11 created; Partner Center: 11 add-on shells created). Request shapes the
 * docs did not settle are still marked UNVERIFIED below. Run --apply once with --store <one store> and read its output before running the others.
 */

import { createSign } from 'node:crypto';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { APPS, ascToken, createAscClient } from './ios-review-cadence.mjs';
import { accessOf } from './publish-store-books.mjs';

export const Store = { Apple: 'apple', Google: 'google', Microsoft: 'microsoft' };
export const ALL_STORES = [Store.Apple, Store.Google, Store.Microsoft];
export const Outcome = { Created: 'created', Completed: 'completed', Exists: 'exists', Failed: 'failed' };

export const PRICE_USD = '0.99';
const PRICE_UNITS = '0';
const PRICE_NANOS = 990000000;
export const PACKAGE_NAME = 'uk.sirous.dinoreader';
const DEFAULT_CONTENT_ROOT = 'content/dino';
const BOOK_FILE = 'book.json';
const ACCESS_FREE = 'free';
const APPLE_TERRITORY = 'USA';
const APPLE_LOCALE = 'en-US';
const PLAY_LISTING_LANGUAGE = 'en-GB';
const PLAY_BUY_OPTION_ID = 'buy';
const PLAY_REGION_US = 'US';
const PLAY_DEFAULT_REGIONS_VERSION = '2022/02'; // UNVERIFIED: the docs say the version is required; set PLAY_REGIONS_VERSION when Play names another
const PLAY_API = 'https://androidpublisher.googleapis.com/androidpublisher/v3/applications';
const GOOGLE_TOKEN_URL = 'https://oauth2.googleapis.com/token';
const GOOGLE_SCOPE = 'https://www.googleapis.com/auth/androidpublisher';
const MS_API = 'https://manage.devcenter.microsoft.com/v1.0/my';
const MS_RESOURCE = 'https://manage.devcenter.microsoft.com';
const MS_PRODUCT_TYPE = 'Durable';
const APPLE_NAME_MAX = 30;
const APPLE_DESCRIPTION_MAX = 45;
const APPLE_DESCRIPTION = 'A picture-book story in Dino Book Reader.'; // fixed, so no title is ever cut mid-word into the 45 characters Apple allows (the name carries the title)
const PLAY_TITLE_MAX = 55;
const PLAY_DESCRIPTION_MAX = 200;
const REQUEST_TIMEOUT_MS = 30000;
const APPLE_PRICE_POINT_LIMIT = 200;
const APPLE_PRICE_POINT_MAX_PAGES = 20;
const APPLE_TERRITORY_LIMIT = 200;
const APPLE_TERRITORY_MAX_PAGES = 10;
const APPLE_API_ORIGIN = 'https://api.appstoreconnect.apple.com';
const HTTP_NOT_FOUND = 404;
const HTTP_CONFLICT = 409;

/** Play and (as far as the docs say) the other stores accept only these characters in a product id; the strictest rule is applied to all three so one id works everywhere. */
const STORE_SAFE_PRODUCT_ID = /^[a-z0-9][a-z0-9._]*$/;

// ---- pure parts (unit-tested) ----

/** Pure: why this product id cannot be created on every store, or null when it can. */
export function productIdProblem(productId) {
    if (STORE_SAFE_PRODUCT_ID.test(productId)) return null;
    return `product id "${productId}" has a character Google Play rejects (only lower-case letters, digits, "_" and "." are allowed; "-" is not)`;
}

/**
 * Pure: the paid books of `entries` (`{ stem, info }`, as read from each book.json), in stem order, each with its product id and any problem with that id.
 * Two books with one product id both get a problem (review #4): they would share one product.
 */
export function paidBooksFrom(entries) {
    const books = [...entries]
        .sort((a, b) => a.stem.localeCompare(b.stem))
        .map(({ stem, info }) => ({ stem, info, ...accessOf(info, stem, { storeSafe: true }) }))
        .filter((book) => book.access !== ACCESS_FREE)
        .map(({ stem, info, productId }) => ({ stem, title: typeof info?.title === 'string' && info.title.trim() !== '' ? info.title.trim() : stem, productId, problem: productIdProblem(productId) }));
    return books.map((book) => (book.problem === null ? { ...book, problem: duplicateProblem(book, books) } : book));
}

function duplicateProblem(book, books) {
    const others = books.filter((other) => other !== book && other.productId === book.productId).map((other) => other.stem);
    return others.length === 0 ? null : `product id "${book.productId}" is also the product id of ${others.join(', ')} (one product cannot unlock two books by accident: give one a productId in its book.json)`;
}

/** Reads every `<root>/<stem>/book.json` (the folders without one are not books); a file that is not JSON is named in the error. */
export function readContentEntries(root, fs = { existsSync, readFileSync, readdirSync }) {
    return fs.readdirSync(root, { withFileTypes: true })
        .filter((entry) => entry.isDirectory() && fs.existsSync(join(root, entry.name, BOOK_FILE)))
        .map((entry) => ({ stem: entry.name, info: parseBookFile(join(root, entry.name, BOOK_FILE), fs.readFileSync(join(root, entry.name, BOOK_FILE), 'utf8')) }));
}

function parseBookFile(path, text) {
    try {
        return JSON.parse(text);
    } catch (error) {
        throw new Error(`${path} is not valid JSON: ${error.message}`);
    }
}

/** Pure: the JSON in a response body, or null when it is empty or not JSON (an HTML 502 page must not hide the status code). */
export function jsonOrNull(text) {
    if (!text) return null;
    try {
        return JSON.parse(text);
    } catch {
        return null;
    }
}

/** Pure: parses the command line; an unknown flag or store is an error, so a typo never turns into a real run. */
export function parseArgs(argv) {
    const options = { apply: false, stores: [], contentRoot: DEFAULT_CONTENT_ROOT };
    for (let i = 0; i < argv.length; i++) {
        const flag = argv[i];
        if (flag === '--apply') options.apply = true;
        else if (flag === '--store') options.stores.push(storeNamed(argv[++i]));
        else if (flag === '--content') options.contentRoot = requireValue(flag, argv[++i]);
        else throw new Error(`Unknown argument "${flag}". Usage: store-products.mjs [--apply] [--store apple|google|microsoft]... [--content <folder>]`);
    }
    if (options.stores.length === 0) options.stores = [...ALL_STORES];
    return options;
}

function requireValue(flag, value) {
    if (value === undefined || value.startsWith('--')) throw new Error(`${flag} needs a value.`);
    return value;
}

function storeNamed(name) {
    if (!ALL_STORES.includes(name)) throw new Error(`Unknown store "${name}" (use ${ALL_STORES.join(', ')}).`);
    return name;
}

/** Pure: `text` itself when it fits `max`, else cut at the last whole word that fits (a single long word is cut hard); never ends in punctuation or a space, and adds none. */
export function cut(text, max) {
    if (text.length <= max) return text;
    const head = text.slice(0, max);
    const atWord = text[max] === ' ' ? head : head.slice(0, Math.max(head.lastIndexOf(' '), 0));
    return (atWord === '' ? head : atWord).replace(/[\s.,;:!?"'-]+$/u, '');
}

/** Pure: App Store Connect's create-product body (POST /v2/inAppPurchases): a non-consumable, named after the book. */
export function appleProductBody(book, appId) {
    return {
        data: {
            type: 'inAppPurchases',
            attributes: { name: cut(book.title, APPLE_NAME_MAX), productId: book.productId, inAppPurchaseType: 'NON_CONSUMABLE', reviewNote: `Unlocks the story "${book.title}" in Dino Book Reader. Purchases sit behind the grown-ups gate.`, familySharable: true },
            relationships: { app: { data: { type: 'apps', id: appId } } },
        },
    };
}

/** Pure: the en-US name and description of an Apple product. UNVERIFIED: this is the v1 localization route, which Apple's docs call deprecated since 4.4.1 but still available. */
export function appleLocalizationBody(book, iapId) {
    return {
        data: {
            type: 'inAppPurchaseLocalizations',
            attributes: { locale: APPLE_LOCALE, name: cut(book.title, APPLE_NAME_MAX), description: cut(APPLE_DESCRIPTION, APPLE_DESCRIPTION_MAX) },
            relationships: { inAppPurchaseV2: { data: { type: 'inAppPurchases', id: iapId } } },
        },
    };
}

/** Pure: the territory availability (every territory, as Apple adds new ones). */
export function appleAvailabilityBody(iapId, territoryIds) {
    return {
        data: {
            type: 'inAppPurchaseAvailabilities',
            attributes: { availableInNewTerritories: true },
            relationships: {
                inAppPurchase: { data: { type: 'inAppPurchases', id: iapId } },
                availableTerritories: { data: territoryIds.map((id) => ({ type: 'territories', id })) },
            },
        },
    };
}

/** Pure: the id of the price point whose customer price is `price` in a list of Apple price points, or null. */
export function findApplePricePoint(points, price = PRICE_USD) {
    const match = (points ?? []).find((point) => Number(point?.attributes?.customerPrice) === Number(price));
    return match?.id ?? null;
}

/** Pure: the price schedule that sets the base territory's price (UNVERIFIED: the base-territory form of the schedule; the older docs example has no baseTerritory). */
export function applePriceScheduleBody(iapId, pricePointId) {
    const priceRef = '${price1}';
    return {
        data: {
            type: 'inAppPurchasePriceSchedules',
            relationships: {
                inAppPurchase: { data: { type: 'inAppPurchases', id: iapId } },
                baseTerritory: { data: { type: 'territories', id: APPLE_TERRITORY } },
                manualPrices: { data: [{ type: 'inAppPurchasePrices', id: priceRef }] },
            },
        },
        included: [{
            type: 'inAppPurchasePrices',
            id: priceRef,
            attributes: { startDate: null },
            relationships: { inAppPurchasePricePoint: { data: { type: 'inAppPurchasePricePoints', id: pricePointId } } },
        }],
    };
}

/** Pure: Google's Money value for 0.99 USD. */
export function playUsdPrice() {
    return { currencyCode: 'USD', units: PRICE_UNITS, nanos: PRICE_NANOS };
}

/** Pure: Play's one-time product (PATCH ...onetimeproducts/<id>?allowMissing=true&updateMask=...&regionsVersion.version=...; the docs spell the patch path all lower case, get and batchUpdateStates use oneTimeProducts): an en-GB listing and one buy option priced per region from the converted prices. */
export function playProductBody(book, converted) {
    const regions = Object.entries(converted?.convertedRegionPrices ?? {}).map(([regionCode, entry]) => ({ regionCode, price: entry.price, availability: 'AVAILABLE' }));
    const other = converted?.convertedOtherRegionsPrice;
    const buyOption = {
        purchaseOptionId: PLAY_BUY_OPTION_ID,
        buyOption: { legacyCompatible: true },
        regionalPricingAndAvailabilityConfigs: regions,
    };
    if (other?.usdPrice && other?.eurPrice) buyOption.newRegionsConfig = { usdPrice: other.usdPrice, eurPrice: other.eurPrice, availability: 'AVAILABLE' }; // shape per OneTimeProductPurchaseOptionNewRegionsConfig docs
    return {
        packageName: PACKAGE_NAME,
        productId: book.productId,
        listings: [{ languageCode: PLAY_LISTING_LANGUAGE, title: cut(book.title, PLAY_TITLE_MAX), description: cut(`Read "${book.title}" in Dino Book Reader, with a voice for every character.`, PLAY_DESCRIPTION_MAX) }],
        purchaseOptions: [buyOption],
    };
}

/** Pure: the regions Play converted, and the US one that must hold the 0.99 base. */
export function playHasUsPrice(converted) {
    const us = converted?.convertedRegionPrices?.[PLAY_REGION_US];
    return us?.price?.currencyCode === 'USD';
}

/** Pure: Partner Center's create-add-on body (POST .../inappproducts): the durable add-on shell. */
export function microsoftAddOnBody(book, storeAppId) {
    return { applicationIds: [storeAppId], productId: book.productId, productType: MS_PRODUCT_TYPE };
}

/** Pure: the lines a dry run prints for one store: what would be created per book, then the manual steps. */
export function planLines(store, books) {
    const lines = [`${STORE_TITLES[store]}: ${books.length} product(s) at ${PRICE_USD} USD`];
    for (const book of books) {
        lines.push(book.problem === null ? `  would create ${book.productId} ("${book.title}")` : `  CANNOT create ${book.productId}: ${book.problem}`);
    }
    return [...lines, ...MANUAL_STEPS[store].map((step) => `  by hand: ${step}`)];
}

const STORE_TITLES = { [Store.Apple]: 'App Store Connect', [Store.Google]: 'Play Console', [Store.Microsoft]: 'Partner Center' };

export const MANUAL_STEPS = {
    [Store.Apple]: [
        'add the review screenshot to each product (no API), then submit the products for review with an app version (https://appstoreconnect.apple.com/apps/6825053435)',
    ],
    [Store.Google]: [
        'nothing left once the merchant account and bank verification are done (Play Console > Monetise with Play > One-time products)',
    ],
    [Store.Microsoft]: [
        'set each add-on\'s price to 0.99 USD, its listing and submit it in Partner Center (the API cannot: Pricing Version 2 gives it an unknown tier)',
        'the Partner Center payout and tax profile must exist before anything can be sold',
    ],
};

// ---- Apple ----

function requireEnv(name) {
    const value = process.env[name];
    if (!value) throw new Error(`${name} is not set.`);
    return value;
}

function appleClientFromEnv() {
    const privateKeyPem = Buffer.from(requireEnv('ASC_API_KEY_P8_BASE64'), 'base64').toString('utf8');
    const keyId = requireEnv('ASC_API_KEY_ID');
    const issuerId = requireEnv('ASC_API_ISSUER_ID');
    return createAscClient({ token: () => appleToken({ keyId, issuerId, privateKeyPem }) });
}

/** The signing error of a bad key can quote key text, so only a fixed message leaves here (never the key, never the library's message). */
function appleToken({ keyId, issuerId, privateKeyPem }) {
    try {
        return ascToken({ keyId, issuerId, privateKeyPem, nowSeconds: Math.floor(Date.now() / 1000) });
    } catch {
        throw new Error('ASC_API_KEY_P8_BASE64 is not a usable private key.');
    }
}

function must(response, what) {
    if (!response.ok) {
        const detail = response.json?.errors?.[0]?.detail ?? response.json?.errors?.[0]?.title ?? `HTTP ${response.status}`;
        throw new Error(`${what}: ${detail}`);
    }
    return response.json;
}

/** Pure: the App Store Connect path (with query) of a `links.next` address, which Apple gives absolute; null when there is no next page. */
export function applePagePath(next) {
    if (typeof next !== 'string' || next === '') return null;
    const url = new URL(next, APPLE_API_ORIGIN);
    if (url.origin !== APPLE_API_ORIGIN) throw new Error('Apple gave a next page on another host; not following it.');
    return `${url.pathname}${url.search}`;
}

/** Walks the price points of the base territory page by page until the 0.99 USD one is found (the list is not assumed to be ordered or to fit one page). */
export async function applePricePointId(asc, iapId) {
    let path = `/v2/inAppPurchases/${iapId}/pricePoints?filter[territory]=${APPLE_TERRITORY}&limit=${APPLE_PRICE_POINT_LIMIT}`;
    for (let page = 0; path !== null && page < APPLE_PRICE_POINT_MAX_PAGES; page++) {
        const json = must(await asc.get(path), 'list price points');
        const id = findApplePricePoint(json.data);
        if (id !== null) return id;
        path = applePagePath(json.links?.next);
    }
    throw new Error(`no ${PRICE_USD} price point found for ${APPLE_TERRITORY}`);
}

/** Every Apple territory id, page by page (review #7: one page would leave the rest never sold). Exported for the test. */
export async function appleTerritoryIds(asc) {
    const ids = [];
    let path = `/v1/territories?limit=${APPLE_TERRITORY_LIMIT}`;
    for (let page = 0; path !== null && page < APPLE_TERRITORY_MAX_PAGES; page++) {
        const json = must(await asc.get(path), 'list territories');
        ids.push(...(json.data ?? []).map((territory) => territory.id));
        path = applePagePath(json.links?.next);
    }
    if (path !== null) throw new Error('list territories: more pages than expected, so the list would be incomplete');
    return ids;
}

/** Whether a read of one of a product's parts holds something: a 404 or an empty answer means the step was never done; any other failure is an error, never "done". */
async function appleStepDone(asc, path, what) {
    const response = await asc.get(path);
    if (response.status === HTTP_NOT_FOUND) return false;
    const data = must(response, `check ${what}`).data;
    return Array.isArray(data) ? data.length > 0 : data !== null && data !== undefined;
}

/**
 * Creates one Apple product, or completes a half-created one: the product, its en-US listing, its availability and its price are each checked on an existing product and only the missing ones are
 * done, so a run that failed half way is finished by the next run. UNVERIFIED: the three read routes follow Apple's endpoint list (read 2026-11-13); the answer shapes are guarded, not assumed.
 */
export async function createAppleProduct(asc, book, appId) {
    const found = must(await asc.get(`/v1/apps/${appId}/inAppPurchasesV2?filter[productId]=${encodeURIComponent(book.productId)}`), 'look up product');
    const existingId = (found.data ?? [])[0]?.id;
    const iapId = existingId ?? must(await asc.post('/v2/inAppPurchases', appleProductBody(book, appId)), 'create product').data.id;
    const fresh = existingId === undefined;
    let did = fresh;
    const missing = async (part, what) => fresh || !(await appleStepDone(asc, `/v2/inAppPurchases/${iapId}/${part}`, what));
    if (await missing('inAppPurchaseLocalizations', 'the en-US listing')) {
        must(await asc.post('/v1/inAppPurchaseLocalizations', appleLocalizationBody(book, iapId)), 'add the en-US listing');
        did = true;
    }
    if (await missing('inAppPurchaseAvailability', 'availability')) {
        must(await asc.post('/v1/inAppPurchaseAvailabilities', appleAvailabilityBody(iapId, await appleTerritoryIds(asc))), 'set availability');
        did = true;
    }
    if (await missing('iapPriceSchedule', 'the price')) {
        must(await asc.post('/v1/inAppPurchasePriceSchedules', applePriceScheduleBody(iapId, await applePricePointId(asc, iapId))), 'set the price');
        did = true;
    }
    if (fresh) return Outcome.Created;
    return did ? Outcome.Completed : Outcome.Exists;
}

// ---- Google ----

/** A Google service-account access token (JWT bearer grant, RS256) for the androidpublisher scope. */
export function googleAssertion(account, nowSeconds) {
    const segment = (object) => Buffer.from(JSON.stringify(object)).toString('base64url');
    const header = segment({ alg: 'RS256', typ: 'JWT' });
    const claims = segment({ iss: account.client_email, scope: GOOGLE_SCOPE, aud: GOOGLE_TOKEN_URL, iat: nowSeconds, exp: nowSeconds + 3600 });
    const signer = createSign('RSA-SHA256');
    signer.update(`${header}.${claims}`);
    return `${header}.${claims}.${signer.sign(account.private_key).toString('base64url')}`;
}

/** Pure: the service account from its JSON text. A parse error quotes part of the text (maybe the private key), so only the variable's name and a fixed message ever leave here. */
export function parseServiceAccount(text, name = 'DINOREADER_PLAY_SERVICE_ACCOUNT_JSON') {
    let account;
    try {
        account = JSON.parse(text);
    } catch {
        throw new Error(`${name} is not valid JSON.`);
    }
    if (typeof account?.client_email !== 'string' || typeof account?.private_key !== 'string') throw new Error(`${name} has no client_email and private_key.`);
    return account;
}

async function googleToken() {
    const account = parseServiceAccount(requireEnv('DINOREADER_PLAY_SERVICE_ACCOUNT_JSON'));
    const body = new URLSearchParams({ grant_type: 'urn:ietf:params:oauth:grant-type:jwt-bearer', assertion: googleAssertion(account, Math.floor(Date.now() / 1000)) });
    const response = await fetch(GOOGLE_TOKEN_URL, { method: 'POST', headers: { 'content-type': 'application/x-www-form-urlencoded' }, body, signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS) });
    if (!response.ok) throw new Error(`Google token: HTTP ${response.status}`);
    return (await response.json()).access_token;
}

async function playRequest(token, method, path, body) {
    const response = await fetch(`${PLAY_API}/${PACKAGE_NAME}/${path}`, {
        method,
        headers: { authorization: `Bearer ${token}`, 'content-type': 'application/json' },
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
    });
    return { status: response.status, ok: response.ok, json: jsonOrNull(await response.text()) };
}

/** Pure: where a Play product stands from its GET answer: no buy option (`missing`), a buy option that is not live (`inactive`), or live (`active`). */
export function playProductStage(product) {
    const buy = (product?.purchaseOptions ?? []).find((option) => option?.purchaseOptionId === PLAY_BUY_OPTION_ID);
    if (!buy) return PlayStage.Missing;
    return buy.state === PLAY_STATE_ACTIVE ? PlayStage.Active : PlayStage.Inactive;
}

export const PlayStage = { Missing: 'missing', Inactive: 'inactive', Active: 'active' };
const PLAY_STATE_ACTIVE = 'ACTIVE';

async function activatePlayBuyOption(token, book, request) {
    must(await request(token, 'POST', `oneTimeProducts/${encodeURIComponent(book.productId)}/purchaseOptions:batchUpdateStates`, activateBody(book)), 'activate the buy option');
}

/** Creates one Play product, or completes a half-created one (created but not activated, or created without its buy option). */
export async function createPlayProduct(token, book, request = playRequest) {
    const existing = await request(token, 'GET', `oneTimeProducts/${encodeURIComponent(book.productId)}`);
    if (!existing.ok && existing.status !== HTTP_NOT_FOUND) throw new Error(`look up product: HTTP ${existing.status}`);
    const stage = existing.ok ? playProductStage(existing.json) : PlayStage.Missing;
    if (stage === PlayStage.Active) return Outcome.Exists;
    if (stage === PlayStage.Inactive) {
        await activatePlayBuyOption(token, book, request);
        return Outcome.Completed;
    }
    const converted = must(await request(token, 'POST', 'pricing:convertRegionPrices', { price: playUsdPrice() }), 'convert prices');
    if (!playHasUsPrice(converted)) throw new Error('Play answered no US price for the conversion');
    const version = process.env.PLAY_REGIONS_VERSION ?? converted.regionVersion?.version ?? PLAY_DEFAULT_REGIONS_VERSION;
    const query = `allowMissing=true&updateMask=listings,purchaseOptions&regionsVersion.version=${encodeURIComponent(version)}`;
    must(await request(token, 'PATCH', `onetimeproducts/${encodeURIComponent(book.productId)}?${query}`, playProductBody(book, converted)), 'create product');
    await activatePlayBuyOption(token, book, request);
    return existing.ok ? Outcome.Completed : Outcome.Created;
}

/** Pure: activates the one buy option of a Play product. Shape per the batchUpdateStates docs (requests[].activatePurchaseOptionRequest with packageName, productId, purchaseOptionId). */
export function activateBody(book) {
    return { requests: [{ activatePurchaseOptionRequest: { packageName: PACKAGE_NAME, productId: book.productId, purchaseOptionId: PLAY_BUY_OPTION_ID } }] };
}

// ---- Microsoft ----

async function microsoftToken() {
    const body = new URLSearchParams({ grant_type: 'client_credentials', client_id: requireEnv('MS_SUBMISSION_CLIENT_ID'), client_secret: requireEnv('MS_SUBMISSION_CLIENT_SECRET'), resource: MS_RESOURCE });
    const response = await fetch(`https://login.microsoftonline.com/${encodeURIComponent(requireEnv('MS_SUBMISSION_TENANT_ID'))}/oauth2/token`, { method: 'POST', headers: { 'content-type': 'application/x-www-form-urlencoded' }, body, signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS) });
    if (!response.ok) throw new Error(`Microsoft token: HTTP ${response.status}`);
    return (await response.json()).access_token;
}

async function microsoftRequest(token, method, url, body) {
    const response = await fetch(url, { method, headers: { authorization: `Bearer ${token}`, 'content-type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body), signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS) });
    return { status: response.status, ok: response.ok, json: jsonOrNull(await response.text()) };
}

/** Pure: the product ids in one page of the add-on list (`value[].productId`), trimmed and lower-cased so the match with a book's id does not depend on how Partner Center writes it. */
export function addOnProductIds(page) {
    return (page?.value ?? []).map((addOn) => addOn.productId).filter((id) => typeof id === 'string').map(normalProductId);
}

/** Pure: a product id as compared against the add-on list. */
export function normalProductId(id) {
    return id.trim().toLowerCase();
}

/** Pure: the address of the next add-on page. `@nextLink` may be absolute or relative to the API root; the bearer token is only ever sent to Microsoft's own host. */
export function microsoftNextUrl(link) {
    if (typeof link !== 'string' || link === '') return null;
    const url = new URL(link, `${MS_API}/`);
    if (url.origin !== MS_RESOURCE) throw new Error('Microsoft gave a next page on another host; not following it.');
    return url.href;
}

async function microsoftExistingIds(token) {
    const ids = new Set();
    let next = `${MS_API}/inappproducts?top=100`;
    while (next) {
        const page = must(await microsoftRequest(token, 'GET', next), 'list add-ons');
        addOnProductIds(page).forEach((id) => ids.add(id));
        next = microsoftNextUrl(page['@nextLink']);
    }
    return ids;
}

/**
 * Creates one add-on unless the list already holds its product id. A 409 on the create is read once more against a fresh list (review #2: if the list's field did not match the first time, the add-on
 * is still found by its product id); only when it is still absent is the 409 reported as an unsupported Partner Center feature.
 */
export async function createMicrosoftProduct(token, book, existing, storeAppId, { request = microsoftRequest, list = microsoftExistingIds } = {}) {
    const wanted = normalProductId(book.productId);
    if (existing.has(wanted)) return Outcome.Exists;
    const response = await request(token, 'POST', `${MS_API}/inappproducts`, microsoftAddOnBody(book, storeAppId));
    if (response.status === HTTP_CONFLICT) {
        const fresh = await list(token);
        if (fresh.has(wanted)) return Outcome.Exists;
        throw new Error('create add-on: 409, the add-on or the app uses a Partner Center feature the submission API does not support');
    }
    must(response, 'create add-on');
    return Outcome.Created;
}

// ---- running ----

async function openStore(store) {
    if (store === Store.Apple) return { asc: appleClientFromEnv(), appId: APPS.dino.id };
    if (store === Store.Google) return { token: await googleToken() };
    return { token: await microsoftToken(), existing: undefined, storeAppId: requireEnv('MS_STORE_APP_ID') };
}

async function createOne(store, session, book) {
    if (store === Store.Apple) return createAppleProduct(session.asc, book, session.appId);
    if (store === Store.Google) return createPlayProduct(session.token, book);
    session.existing ??= await microsoftExistingIds(session.token);
    return createMicrosoftProduct(session.token, book, session.existing, session.storeAppId);
}

/** Creates the products of one store; a book that fails does not stop the others. Returns `{ book, outcome, message }` per book. */
export async function applyStore(store, books, open = openStore, create = createOne) {
    const session = await open(store);
    const results = [];
    for (const book of books) {
        if (book.problem !== null) {
            results.push({ book, outcome: Outcome.Failed, message: book.problem });
            continue;
        }
        try {
            results.push({ book, outcome: await create(store, session, book), message: '' });
        } catch (error) {
            results.push({ book, outcome: Outcome.Failed, message: error.message });
        }
    }
    return results;
}

function printResults(store, results) {
    console.log(`${STORE_TITLES[store]}:`);
    for (const { book, outcome, message } of results) console.log(`  ${outcome}: ${book.productId}${message ? ` (${message})` : ''}`);
    MANUAL_STEPS[store].forEach((step) => console.log(`  by hand: ${step}`));
}

async function main() {
    const options = parseArgs(process.argv.slice(2));
    const books = paidBooksFrom(readContentEntries(options.contentRoot));
    if (!options.apply) {
        console.log(`DRY RUN (nothing is created; add --apply to create): ${books.length} paid book(s)`);
        options.stores.forEach((store) => console.log(planLines(store, books).join('\n')));
        if (books.some((book) => book.problem !== null)) process.exitCode = 1; // review #9: a scripted pre-check must not pass what --apply would refuse
        return;
    }
    let failed = false;
    for (const store of options.stores) {
        try {
            const results = await applyStore(store, books);
            failed ||= results.some((result) => result.outcome === Outcome.Failed);
            printResults(store, results);
        } catch (error) {
            failed = true;
            console.log(`${STORE_TITLES[store]}: FAILED before any product: ${error.message}`);
        }
    }
    if (failed) process.exitCode = 1;
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    main().catch((error) => {
        console.error(error.message);
        process.exitCode = 1;
    });
}
