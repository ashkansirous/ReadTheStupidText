#!/usr/bin/env node
/**
 * Creates one product per paid Dino Reader book at the 0.99 USD price point in the three stores (Slice 346, Part D), through each store's API where one exists.
 * The paid books are read from the repo the way publish-store-books.mjs reads them: every `content/dino/<stem>/book.json` whose `access` is not "free" (default paid), with the
 * product id from `accessOf` (`uk.sirous.dinoreader.book.<stem>` unless book.json names one). Nothing here is saved or logged about a buyer; no secret value is ever printed.
 *
 * DRY RUN IS THE DEFAULT: with no flag the script prints what it would create, makes no network call and needs no credentials. A real run needs --apply.
 *
 * Usage: node store-products.mjs [--apply] [--store apple|google|microsoft]... [--content <folder>] [--book <stem>] [--check-prices]
 *   --book <stem>      only that paid book, and no subscription in any store (Slice 411: a new book never touches the subscription products)
 *   --check-prices     read-only: reads each book's live Microsoft price from the public displaycatalog and fails on anything but USD 0.99 (certification takes hours, so run it the next day)
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
 *   Microsoft: API creates the durable add-on shell, then (Slice 411, proven live 2026-11-18) its submission: price Tier1012 (0.99 USD), the en-us listing and the other fields copied from the newest published book
 *              add-on, commit, and a poll of the commit status (a 429 waits the seconds Microsoft names, up to 10 minutes). A pending submission Partner Center edited (409 on PUT) must be deleted there and re-created.
 *              The Partner Center payout and tax profile must exist. Subscription add-on shells are still shells only (UNVERIFIED shapes).
 *              Microsoft keys: the Entra app's MS_STORE_TENANT_ID / _CLIENT_ID / _CLIENT_SECRET in D:/secrets/dinoreader/secrets.env map to MS_SUBMISSION_* (store-products-local.mjs does it).
 *
 * Slice 384 adds the Dino subscription (a monthly 1.99 GBP and a yearly 14.99 GBP plan, 7-day free trial) to the same run, after the books: Play one subscription with two base plans and trial offers, Apple one group with
 * two subscriptions (price, listing, trial), Microsoft two subscription add-on shells. Its Apple request shapes are checked against Apple's App Store Connect API docs (developer.apple.com/documentation/appstoreconnectapi, read through context7 /websites/developer_apple_appstoreconnectapi, 2026-11-15) and the first --apply answer (the create call has no availableInAllTerritories); the Play shapes are checked against the Google Play Developer API reference (developers.google.com/android-publisher/api-ref/rest/v3, read through context7 /websites/developers_google_android-publisher and the pages themselves, 2026-11-15; no --apply has run yet); the Microsoft shapes are still UNVERIFIED. The dry run lists them and what is left by hand.
 *
 * Run live on 2026-11-13 against all three stores (Play: 11 created and activated; Apple: 11 created; Partner Center: 11 add-on shells created). Request shapes the
 * docs did not settle are still marked UNVERIFIED below. Run --apply once with --store <one store> and read its output before running the others.
 */

import { createSign } from 'node:crypto';
import { existsSync, readFileSync, readdirSync, realpathSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { APPS, ascToken, createAscClient } from './ios-review-cadence.mjs';
import { accessOf } from './publish-store-books.mjs';

export const Store = { Apple: 'apple', Google: 'google', Microsoft: 'microsoft' };
export const ALL_STORES = [Store.Apple, Store.Google, Store.Microsoft];
export const Outcome = { Created: 'created', Completed: 'completed', Exists: 'exists', Failed: 'failed', RateLimited: 'rate limited, rerun later' };

/** Thrown when a store keeps answering 429 past the run's wait budget; the book ends as `Outcome.RateLimited` (nonzero exit), never reported as done. */
export class RateLimitedError extends Error {}

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
const APPLE_LIST_MAX_PAGES = 50;
const APPLE_RETRY_MAX = 5;
const APPLE_PROGRESS_EVERY = 20;
const APPLE_RETRY_BASE_MS = 1000;
const APPLE_RETRY_MAX_MS = 60000;
const HTTP_TOO_MANY = 429;
const HTTP_SERVER_ERROR = 500;
const APPLE_TERRITORY_MAX_PAGES = 10;
const APPLE_API_ORIGIN = 'https://api.appstoreconnect.apple.com';
const HTTP_NOT_FOUND = 404;
const HTTP_CONFLICT = 409;
const MS_PRICE_ID = 'Tier1012'; // 0.99 USD on the advanced pricing model (Tier1012-Tier1424 are valid there; read from Microsoft's add-on submission docs 2026-11-18)
const MS_LISTING_LOCALE = 'en-us';
const MS_TEMPLATE_FIELDS = ['contentType', 'keywords', 'lifetime', 'visibility', 'privacyPolicyRequired', 'privacyPolicyUrl'];
const MS_STATUS_COMMIT_STARTED = 'CommitStarted';
const MS_STATUS_PUBLISHED = 'Published';
const MS_COMMIT_RUNNING_STATUSES = [MS_STATUS_COMMIT_STARTED, 'PreProcessing'];
/** What a Microsoft submission status means for the run (the one table every caller reads): needs work = PUT then commit; in flight = leave alone; failed or unknown = throw. */
export const MsSubmissionKind = Object.freeze({ NeedsWork: 'needs work', InFlight: 'in flight', Failed: 'failed', Unknown: 'unknown' });
// PreProcessing: Partner Center's status read live 2026-11-18 right after CommitStarted. No status at all (a fresh submission) needs work like PendingCommit.
const MS_KIND_BY_STATUS = new Map([
    ['PendingCommit', MsSubmissionKind.NeedsWork],
    ...[MS_STATUS_COMMIT_STARTED, 'PreProcessing', 'Certification', 'InCertification', 'Release', 'Publishing', 'PendingPublication', MS_STATUS_PUBLISHED].map((status) => [status, MsSubmissionKind.InFlight]),
    ...['CommitFailed', 'Failed', 'CertificationFailed', 'ReleaseFailed', 'Canceled'].map((status) => [status, MsSubmissionKind.Failed]),
]);
const MS_PUBLISH_MODE_IMMEDIATE = 'Immediate';
const MS_ACTION_PURCHASE = 'Purchase';
const MS_VERDICT_PENDING = 'pending';
const MS_VERDICT_DONE = 'done';
const CATALOG_RETRY_WAIT_MS = 3000;
const MS_STATUS_POLL_MS = 5000;
const MS_STATUS_POLL_MAX = 120; // 120 x 5 s = 10 minutes; a commit still running after that is reported, not failed
const MS_RATE_LIMIT_MAX_WAIT_S = 600; // the longest single wait
const MS_RATE_LIMIT_TOTAL_S = 1800; // Partner Center kept answering "try again in ~290 s" for about 15 minutes on 2026-11-18; a run's waits in total may reach 30 minutes
const MS_RATE_LIMIT_DEFAULT_S = 60;
const MS_DISPLAY_CATALOG = 'https://displaycatalog.mp.microsoft.com/v7.0/products';
const MS_PRICE_CURRENCY = 'USD';

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

/** Pure: true when the body has content that does not parse as JSON (an HTML 404 page); an empty body and a real JSON `null` are not. */
export function isNonJsonBody(text) {
    if (!text || text.trim() === '') return false;
    try {
        JSON.parse(text);
        return false;
    } catch {
        return true;
    }
}

/** Pure: parses the command line; an unknown flag or store is an error, so a typo never turns into a real run. */
export function parseArgs(argv) {
    const options = { apply: false, stores: [], contentRoot: DEFAULT_CONTENT_ROOT, book: null, checkPrices: false };
    for (let i = 0; i < argv.length; i++) {
        const flag = argv[i];
        if (flag === '--apply') options.apply = true;
        else if (flag === '--store') options.stores.push(storeNamed(argv[++i]));
        else if (flag === '--content') options.contentRoot = requireValue(flag, argv[++i]);
        else if (flag === '--book') options.book = requireValue(flag, argv[++i]);
        else if (flag === '--check-prices') options.checkPrices = true;
        else throw new Error(`Unknown argument "${flag}". Usage: store-products.mjs [--apply] [--store apple|google|microsoft]... [--content <folder>] [--book <stem>] [--check-prices]`);
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

/** Pure: with `--book <stem>` only that paid book (an unknown or free stem is an error, so a typo never creates nothing quietly); without it all of `books`. */
export function selectBooks(books, stem) {
    if (stem === null) return books;
    const chosen = books.filter((book) => book.stem === stem);
    if (chosen.length === 0) throw new Error(`--book ${stem}: no paid book with that folder name in the content folder (a free book needs no product)`);
    return chosen;
}

/** Pure: the products one store's run handles: the books, then the subscriptions only when no single book was asked for (Decision 4). */
export function itemsForStore(store, books, options) {
    return options.book === null ? [...books, ...subscriptionItemsFor(store)] : books;
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
        'nothing for a book: the run sets the add-on\'s price (Tier1012 = 0.99 USD), en-us listing and commits it; certification takes hours, so read the live price the next day with --check-prices',
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
    if (response.ok && response.nonJson === true) throw nonJsonError(what, response); // a 200 HTML page (a gateway or captive portal) is not a success
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

/** How much of a non-JSON answer an error shows (an HTML 404 of a wrong route, a gateway or captive-portal page all land here). */
const NON_JSON_HEAD_CHARS = 80;

/** Pure: the error for an answer that has content but is not JSON (status and the first characters, so the cause can be told from a wrong route). */
function nonJsonError(what, response) {
    return new Error(`${what}: answer is not JSON (status ${response.status}, first ${NON_JSON_HEAD_CHARS} chars: ${JSON.stringify(response.head ?? '')})`);
}

async function playRequest(token, method, path, body) {
    const response = await fetch(`${PLAY_API}/${PACKAGE_NAME}/${path}`, {
        method,
        headers: { authorization: `Bearer ${token}`, 'content-type': 'application/json' },
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
    });
    const text = await response.text();
    return { status: response.status, ok: response.ok, retryAfter: response.headers.get('retry-after'), json: jsonOrNull(text), empty: text.trim() === '', nonJson: isNonJsonBody(text), head: text.slice(0, NON_JSON_HEAD_CHARS) };
}

/** Pure: where a Play product stands from its GET answer: no buy option (`missing`), a buy option that is not live (`inactive`), or live (`active`). */
export function playProductStage(product) {
    const buy = (product?.purchaseOptions ?? []).find((option) => option?.purchaseOptionId === PLAY_BUY_OPTION_ID);
    if (!buy) return PlayStage.Missing;
    return buy.state === PLAY_STATE_ACTIVE ? PlayStage.Active : PlayStage.Inactive;
}

export const PlayStage = { Missing: 'missing', Inactive: 'inactive', Active: 'active' };
const PLAY_STATE_ACTIVE = 'ACTIVE';
const PLAY_STATE_DRAFT = 'DRAFT';

async function activatePlayBuyOption(token, book, request) {
    must(await request(token, 'POST', `oneTimeProducts/${encodeURIComponent(book.productId)}/purchaseOptions:batchUpdateStates`, activateBody(book)), 'activate the buy option');
}

/** Creates one Play product, or completes a half-created one (created but not activated, or created without its buy option). */
export async function createPlayProduct(token, book, request = playRequest) {
    const existing = await request(token, 'GET', `oneTimeProducts/${encodeURIComponent(book.productId)}`);
    if (!existing.ok && existing.status !== HTTP_NOT_FOUND) throw new Error(`look up product: HTTP ${existing.status}`);
    // Slice 346 E4: Play's own answers are JSON, a 404 included; an HTML page (a wrong route answers Google's HTML 404) must never read as "no such product", or an existing product would be "created" again.
    // An empty body (a 200 or 204 with nothing in it) is an answer too, as before; only a body that has content and is not JSON is the wrong-route trap.
    if (existing.nonJson === true) throw nonJsonError('look up product', existing);
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
    const body = book.kind === 'subscription' ? microsoftSubscriptionBody(book, storeAppId) : microsoftAddOnBody(book, storeAppId);
    const response = await request(token, 'POST', `${MS_API}/inappproducts`, body);
    if (response.status === HTTP_CONFLICT) {
        const fresh = await list(token);
        if (fresh.has(wanted)) return Outcome.Exists;
        throw new Error('create add-on: 409, the add-on or the app uses a Partner Center feature the submission API does not support');
    }
    must(response, 'create add-on');
    return Outcome.Created;
}

// ---- Microsoft book add-on: price, listing, commit (Slice 411) ----
// Proven live on 2026-11-18: the add-on shell takes a submission whose pricing is `Tier1012` (0.99 USD), the template fields of the newest published book add-on and an en-us listing; commit, then poll the status.

/** Pure: the seconds Microsoft names in a 429 answer ("try again in 292 seconds"), else a default; never above the wait cap. */
export function microsoftRateLimitSeconds(json) {
    const named = Number(/(\d+)\s*seconds?/i.exec(JSON.stringify(json ?? ''))?.[1]);
    return Math.min(Number.isFinite(named) && named > 0 ? named : MS_RATE_LIMIT_DEFAULT_S, MS_RATE_LIMIT_MAX_WAIT_S);
}

/** A Microsoft request that, on a 429, waits the seconds Microsoft names and sends it again; gives up once the waits in total (`budget.waited`, shared by every request of a run) would pass 30 minutes (RateLimitedError). */
export function retryingMicrosoft(request, { sleep = realSleep, log = () => {}, budget = { waited: 0 } } = {}) {
    return async (token, method, url, body) => {
        for (;;) {
            const response = await request(token, method, url, body);
            if (response.status !== HTTP_TOO_MANY) return response;
            const seconds = microsoftRateLimitSeconds(response.json);
            if (budget.waited + seconds > MS_RATE_LIMIT_TOTAL_S) throw new RateLimitedError(`Microsoft rate limit: still asked to wait ${seconds} s after ${budget.waited} s of waiting (cap ${MS_RATE_LIMIT_TOTAL_S} s); rerun later`);
            log(`    Microsoft rate limit: waiting ${seconds} s`);
            budget.waited += seconds;
            await sleep(seconds * 1000);
        }
    };
}

/** Pure: the PUT body of a book's submission: what Partner Center made (`pending`) plus the price, the en-us listing and the template's other fields. Read-only fields are dropped. */
export function microsoftSubmissionBody(book, pending, template) {
    const { statusDetails, status, fileUploadUrl, ...kept } = pending; // eslint-disable-line no-unused-vars
    const copied = Object.fromEntries(MS_TEMPLATE_FIELDS.filter((field) => template[field] !== undefined).map((field) => [field, template[field]])); // a field the template lacks keeps the value Partner Center put on the pending submission
    return {
        ...kept, ...copied,
        targetPublishMode: MS_PUBLISH_MODE_IMMEDIATE,
        pricing: { priceId: MS_PRICE_ID, marketSpecificPricings: template.pricing?.marketSpecificPricings ?? {}, sales: [] },
        listings: { [MS_LISTING_LOCALE]: { title: book.title, description: APPLE_DESCRIPTION } },
    };
}

/** Pure: the kind of a submission status (see `MsSubmissionKind`); a missing status needs work, a status not in the table is Unknown. */
export function microsoftSubmissionKind(status) {
    if (status === undefined || status === null) return MsSubmissionKind.NeedsWork;
    return MS_KIND_BY_STATUS.get(status) ?? MsSubmissionKind.Unknown;
}

/** Pure: the Error for a Failed or Unknown submission, naming its status; null for the other kinds. */
function microsoftSubmissionProblem(submission) {
    const kind = microsoftSubmissionKind(submission?.status);
    if (kind === MsSubmissionKind.Unknown) return new Error(`unknown status ${submission.status}: check Partner Center`);
    if (kind !== MsSubmissionKind.Failed) return null;
    return new Error(`the pending submission ${submission.id} ended ${submission.status}: delete it in Partner Center, then rerun`);
}

/**
 * Pure: what a status read during the commit poll means: `done` (Published), `pending` (any other kind that is not an error), or an Error for a failed or unknown status
 * (CommitFailed, CertificationFailed ...; its errors printed).
 */
export function microsoftCommitVerdict(statusAnswer) {
    const status = statusAnswer?.status;
    const kind = microsoftSubmissionKind(status);
    if (kind === MsSubmissionKind.Unknown) throw microsoftSubmissionProblem(statusAnswer);
    if (kind === MsSubmissionKind.Failed) {
        const errors = (statusAnswer?.statusDetails?.errors ?? []).map((error) => error.details ?? error.code ?? JSON.stringify(error)).join('; ');
        throw new Error(`commit failed: ${errors || 'no error given'} (status ${status})`);
    }
    return kind === MsSubmissionKind.InFlight && !MS_COMMIT_RUNNING_STATUSES.includes(status) ? MS_VERDICT_DONE : MS_VERDICT_PENDING; // the commit itself is over once the status is past CommitStarted and PreProcessing
}

/** Pure: the public, credential-free address of an add-on's catalog entry (price readable once it is live). */
export function microsoftCatalogUrl(storeId) {
    return `${MS_DISPLAY_CATALOG}/${encodeURIComponent(storeId)}?market=US&languages=en-us`;
}

/** Pure: the live purchase price in a displaycatalog answer, as `{ currency, price }`, or null when it has none (not live yet). */
export function microsoftLivePrice(catalog) {
    const product = catalog?.Product ?? catalog?.Products?.[0] ?? catalog; // v7.0 answers a top-level `Product` (read live 2026-11-18); the plural form is kept for older answers
    for (const sku of product?.DisplaySkuAvailabilities ?? []) {
        for (const availability of sku.Availabilities ?? []) {
            const price = availability.OrderManagementData?.Price;
            if (availability.Actions?.includes(MS_ACTION_PURCHASE) && price?.ListPrice !== undefined) return { currency: price.CurrencyCode, price: price.ListPrice };
        }
    }
    return null;
}

/** Pure: why a live price is not the book price (USD 0.99), or null when it is. */
export function microsoftPriceProblem(live) {
    if (live === null) return 'no live price yet (not certified or not published)';
    if (live.currency === MS_PRICE_CURRENCY && Number(live.price).toFixed(2) === PRICE_USD) return null;
    return `live price is ${live.currency} ${live.price}, not ${MS_PRICE_CURRENCY} ${PRICE_USD}`;
}

/** Map of normalised product id -> add-on id, in the order Partner Center lists them (the order is not documented and not relied on). */
async function microsoftAddOnMap(token, request = microsoftRequest) {
    const ids = new Map();
    let next = `${MS_API}/inappproducts?top=100`;
    while (next) {
        const page = must(await request(token, 'GET', next), 'list add-ons');
        (page?.value ?? []).filter((addOn) => typeof addOn.productId === 'string').forEach((addOn) => ids.set(normalProductId(addOn.productId), addOn.id));
        next = microsoftNextUrl(page?.['@nextLink']);
    }
    return ids;
}

/** The submission of a published book add-on whose fields every new book copies: the list's order is not documented, so any published one will do (the fields are the same for every book); the last listed is tried first, one GET each. */
export async function microsoftTemplate(token, addOns, request, skipProductId) {
    const bookPrefix = `${PACKAGE_NAME}.book.`;
    const candidates = [...addOns].filter(([productId]) => productId.startsWith(bookPrefix) && productId !== normalProductId(skipProductId)).reverse();
    for (const [, id] of candidates) {
        const full = must(await request(token, 'GET', `${MS_API}/inappproducts/${id}`), 'read template add-on');
        const published = full?.lastPublishedInAppProductSubmission?.id;
        if (published) return must(await request(token, 'GET', `${MS_API}/inappproducts/${id}/submissions/${published}`), 'read template submission');
    }
    throw new Error('no published book add-on to copy the submission fields from');
}

async function microsoftPendingSubmission(token, id, full, request) {
    const path = `${MS_API}/inappproducts/${id}/submissions`;
    if (!full.pendingInAppProductSubmission) return must(await request(token, 'POST', path), 'create submission');
    if (!full.pendingInAppProductSubmission.id) throw new Error('the add-on lists a pending submission with no id: check Partner Center');
    return must(await request(token, 'GET', `${path}/${full.pendingInAppProductSubmission.id}`), 'read pending submission');
}

/** PUTs the submission. A 409 is read against the submission's status, mapped by the same table: in flight answers `{ inFlight }` (the status lags behind a commit); needs work means it was edited in Partner Center; failed or unknown throw. */
async function microsoftPutSubmission(token, id, submission, body, request) {
    const base = `${MS_API}/inappproducts/${id}/submissions/${submission.id}`;
    const response = await request(token, 'PUT', base, body);
    if (response.status !== HTTP_CONFLICT) return must(response, 'put submission');
    const current = must(await request(token, 'GET', `${base}/status`), 'read status after 409');
    const problem = microsoftSubmissionProblem({ id: submission.id, status: current?.status });
    if (problem !== null) throw problem;
    if (microsoftSubmissionKind(current?.status) === MsSubmissionKind.InFlight) return { inFlight: current.status };
    throw new Error('the pending submission was edited in Partner Center (409 on PUT): delete that submission there, then rerun');
}

function stillCommittingResult(status, where) {
    if (status === MS_STATUS_PUBLISHED) return { outcome: Outcome.Exists, message: `already ${status}; ${where}` };
    return { outcome: Outcome.Exists, message: `still committing (${status}), check later with --check-prices or Partner Center; ${where}` };
}

async function microsoftCommitAndWait(token, id, submission, request, { sleep, log }) {
    const base = `${MS_API}/inappproducts/${id}/submissions/${submission.id}`;
    must(await request(token, 'POST', `${base}/commit`), 'commit submission');
    let status = MS_STATUS_COMMIT_STARTED;
    for (let attempt = 0; attempt < MS_STATUS_POLL_MAX; attempt++) {
        await sleep(MS_STATUS_POLL_MS);
        const answer = must(await request(token, 'GET', `${base}/status`), 'read commit status');
        status = answer.status ?? status;
        log(`    submission status: ${answer.status}`);
        if (microsoftCommitVerdict(answer) === MS_VERDICT_DONE) return { status, stillCommitting: false };
    }
    return { status, stillCommitting: true };
}

/**
 * Completes one book add-on shell (Decisions 1-2): published -> Exists; committed and in certification -> Exists; otherwise PUT price, listing and template fields, commit and poll.
 * Returns `{ outcome, message }`; the message carries the Store id and the catalog address for the later price check.
 */
export async function completeMicrosoftBook(token, book, addOnId, { request, template, sleep = realSleep, log = () => {} }) {
    const full = must(await request(token, 'GET', `${MS_API}/inappproducts/${addOnId}`), 'read add-on');
    const where = `Store id ${addOnId}, ${microsoftCatalogUrl(addOnId)}`;
    if (full.lastPublishedInAppProductSubmission) return { outcome: Outcome.Exists, message: where };
    const pending = await microsoftPendingSubmission(token, addOnId, full, request);
    if (!pending?.id) throw new Error('the pending submission has no id: check Partner Center');
    const problem = microsoftSubmissionProblem(pending);
    if (problem !== null) throw problem;
    if (microsoftSubmissionKind(pending.status) === MsSubmissionKind.InFlight) return stillCommittingResult(pending.status, where);
    const put = await microsoftPutSubmission(token, addOnId, pending, microsoftSubmissionBody(book, pending, await template()), request);
    if (put.inFlight !== undefined) return stillCommittingResult(put.inFlight, where);
    const { status, stillCommitting } = await microsoftCommitAndWait(token, addOnId, pending, request, { sleep, log });
    if (stillCommitting) return { outcome: Outcome.Completed, message: `still committing (${status}) after ${(MS_STATUS_POLL_MAX * MS_STATUS_POLL_MS) / 1000} s, check later with --check-prices or Partner Center; ${where}` };
    return { outcome: Outcome.Completed, message: `committed, now ${status}; certification takes hours, read the price later with --check-prices; ${where}` };
}

/** Pure: `fn` that runs once; every later call gets the first call's promise. A rejected run is forgotten (review #4), so one failed read does not fail every later book the same way. */
export function memoisedOnce(fn) {
    let promise;
    return () => {
        promise ??= fn().catch((error) => {
            promise = undefined;
            throw error;
        });
        return promise;
    };
}

/** One book on Microsoft: the shell (as before), then its submission. A shell made in this run reports Created, a finished one Exists, a pending one Completed. */
export async function createMicrosoftBook(token, book, session, options = {}) {
    const { sleep = realSleep, log = console.log } = options;
    session.rateBudget ??= { waited: 0 }; // one 30-minute budget of 429 waits for the whole run
    const request = options.request ?? retryingMicrosoft(microsoftRequest, { log, budget: session.rateBudget });
    session.addOns ??= await microsoftAddOnMap(token, request);
    const shell = await createMicrosoftProduct(token, book, new Set(session.addOns.keys()), session.storeAppId, { request, list: async (t) => new Set((await microsoftAddOnMap(t, request)).keys()) });
    if (shell === Outcome.Created) session.addOns = await microsoftAddOnMap(token, request);
    const addOnId = session.addOns.get(normalProductId(book.productId));
    if (addOnId === undefined) throw new Error('the add-on was created but is not in the add-on list yet; rerun');
    session.template ??= memoisedOnce(() => microsoftTemplate(token, session.addOns, request, book.productId)); // read once per run; the template is a published add-on, never the book in hand
    const done = await completeMicrosoftBook(token, book, addOnId, { request, template: session.template, sleep, log });
    return { outcome: shell === Outcome.Created ? Outcome.Created : done.outcome, message: done.message };
}

/**
 * Reads each book's live Microsoft price from the public catalog and returns `{ book, storeId, problem, unreadable }` per book (problem null = USD 0.99; `unreadable` is set, and `problem` stays null,
 * when the catalog could not be read, so a transient error is never counted as a wrong price). `fetchCatalog(storeId)` and `sleep` are injected for tests.
 */
export async function checkMicrosoftPrices(books, addOns, fetchCatalog = fetchCatalogJson, sleep = realSleep) {
    const results = [];
    for (const book of books) {
        const id = addOns.get(normalProductId(book.productId));
        results.push({ book, storeId: id, ...(await priceProblemOf(id, fetchCatalog, sleep)) });
    }
    return results;
}

/** True for a catalog failure worth one more try: HTTP 429 or a 5xx (`error.status` is set by `fetchCatalogJson`). */
function isTransientCatalogError(error) {
    return error?.status === HTTP_TOO_MANY || error?.status >= HTTP_SERVER_ERROR;
}

/** One catalog read, repeated once after a short wait when the first failed with a 429 or 5xx. */
async function readCatalogWithRetry(storeId, fetchCatalog, sleep) {
    try {
        return await fetchCatalog(storeId);
    } catch (error) {
        if (!isTransientCatalogError(error)) throw error;
        await sleep(CATALOG_RETRY_WAIT_MS);
        return fetchCatalog(storeId);
    }
}

/** One book's `{ problem, unreadable }`: a wrong or missing price is a problem; a catalog that could not be read (after the retry) is `unreadable`, never "no live price yet" and never a wrong price. */
async function priceProblemOf(storeId, fetchCatalog, sleep) {
    if (storeId === undefined) return { problem: 'no add-on in Partner Center', unreadable: null };
    try {
        return { problem: microsoftPriceProblem(microsoftLivePrice(await readCatalogWithRetry(storeId, fetchCatalog, sleep))), unreadable: null };
    } catch (error) {
        return { problem: null, unreadable: `could not read the catalog: ${error.message}` };
    }
}

/** The catalog answer, or null when the catalog has no such product yet (404); any other failure throws (with `status`) so it is not mistaken for "not live". */
async function fetchCatalogJson(storeId) {
    const response = await fetch(microsoftCatalogUrl(storeId), { signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS) });
    if (response.status === HTTP_NOT_FOUND) return null;
    if (!response.ok) throw Object.assign(new Error(`displaycatalog answered HTTP ${response.status}`), { status: response.status });
    return jsonOrNull(await response.text());
}

// ---- Subscription (Slice 384) ----
// One subscription per store with a monthly (1.99 GBP) and a yearly (14.99 GBP) plan and a 7-day free trial for new subscribers; the stores fill in other countries (Play: convertRegionPrices; Apple: equalised price points).
// Play: one subscription with two base plans and a trial offer on each. Apple: one subscription group with two products. Microsoft: two subscription add-on shells (price, period and trial are set in Partner Center by hand).
// Every request shape below follows the stores' docs read 2026-11-15 and is marked UNVERIFIED (Microsoft) or "checked against" its doc page (Apple, Play) until a real --apply has run once; this script's dry run (the default) is all that has run for Play.

export const SUBSCRIPTION_PRODUCT_ID = `${PACKAGE_NAME}.sub`;
export const TRIAL_DAYS = 7;
export const TRIAL_OFFER_ID = 'trial';
export const SUBSCRIPTION_CURRENCY = 'GBP';
const APPLE_SUBSCRIPTION_TERRITORY = 'GBR';
const APPLE_GROUP_NAME = 'Dino Book Reader subscription';
const SUBSCRIPTION_TITLE = 'Dino Book Reader: every book';
const SUBSCRIPTION_DESCRIPTION = 'Opens every book in Dino Book Reader while you subscribe.';
export const SUBSCRIPTION_PLANS = [
    { id: 'monthly', productId: `${SUBSCRIPTION_PRODUCT_ID}.monthly`, playPeriod: 'P1M', applePeriod: 'ONE_MONTH', appleLevel: 2, gbp: { currencyCode: SUBSCRIPTION_CURRENCY, units: '1', nanos: 990000000 }, gbpLabel: '1.99' },
    { id: 'yearly', productId: `${SUBSCRIPTION_PRODUCT_ID}.yearly`, playPeriod: 'P1Y', applePeriod: 'ONE_YEAR', appleLevel: 1, gbp: { currencyCode: SUBSCRIPTION_CURRENCY, units: '14', nanos: 990000000 }, gbpLabel: '14.99' },
];

/** Pure: what a store gets created for the subscription. Play has one subscription holding both plans; Apple and Microsoft one product per plan. */
export function subscriptionItemsFor(store) {
    if (store === Store.Google) return [{ kind: 'subscription', title: SUBSCRIPTION_TITLE, productId: SUBSCRIPTION_PRODUCT_ID, plans: SUBSCRIPTION_PLANS, problem: null }];
    return SUBSCRIPTION_PLANS.map((plan) => ({ kind: 'subscription', title: `${SUBSCRIPTION_TITLE} (${plan.id})`, productId: plan.productId, plan, problem: null }));
}

/** Pure: the dry-run lines for the subscription of one store. */
export function subscriptionPlanLines(store) {
    const lines = [`${STORE_TITLES[store]}: subscription, ${SUBSCRIPTION_PLANS.map((plan) => `${plan.id} ${plan.gbpLabel} ${SUBSCRIPTION_CURRENCY}`).join(' and ')}, ${TRIAL_DAYS}-day free trial`];
    for (const item of subscriptionItemsFor(store)) lines.push(`  would create ${item.productId} ("${item.title}")`);
    if (store === Store.Google) lines.push(`  other regions: converted from the tax-exclusive equivalent of the GB price (convertRegionPrices takes tax-exclusive, answers tax-inclusive); --apply prints GB, US, DE, JP, AU, CA prices per plan before it creates anything`);
    return [...lines, ...SUBSCRIPTION_MANUAL_STEPS[store].map((step) => `  by hand: ${step}`)];
}

export const SUBSCRIPTION_MANUAL_STEPS = {
    [Store.Apple]: [
        'add the review screenshot to each subscription and the subscription group\'s localisation if App Store Connect asks, then submit them for review with an app version (https://appstoreconnect.apple.com/apps/6825053435/distribution/subscriptions)',
    ],
    [Store.Google]: [
        'nothing left once the merchant account is done (Play Console > Monetise with Play > Subscriptions)',
    ],
    [Store.Microsoft]: [
        'for each subscription add-on set the period (1 month, 1 year), the price (1.99 GBP, 14.99 GBP), the 7-day free trial, its listing and submit it in Partner Center (the submission API cannot set a subscription\'s price or trial)',
    ],
};

/** Pure: Play's subscription (POST .../subscriptions?productId=...&regionsVersion.version=...): one en-GB listing and a base plan per plan, priced per region from the converted GBP price. Checked against monetization.subscriptions.create and the Subscription, SubscriptionListing (title, description up to 200 chars, up to four benefits) and BasePlan pages, 2026-11-15; the regionsVersion object is sent as the query parameter regionsVersion.version. */
export function playSubscriptionBody(convertedByPlan) {
    return {
        packageName: PACKAGE_NAME,
        productId: SUBSCRIPTION_PRODUCT_ID,
        listings: [{ languageCode: PLAY_LISTING_LANGUAGE, title: SUBSCRIPTION_TITLE, description: SUBSCRIPTION_DESCRIPTION, benefits: ['Every book opens', 'New books included'] }],
        basePlans: SUBSCRIPTION_PLANS.map((plan) => playBasePlanBody(plan, convertedByPlan[plan.id])),
    };
}

/** Pure: the region price list of a base plan: Play's converted price per region, except the GB one, which is the plan's own GBP price (the conversion's input is tax exclusive and its output tax inclusive, so Play's GB answer would be 1.99 plus VAT). */
export function playPlanRegionalConfigs(plan, converted) {
    const byRegion = new Map(Object.entries(converted?.convertedRegionPrices ?? {}).map(([regionCode, entry]) => [regionCode, entry.price]));
    byRegion.set(PLAY_REGION_GB, plan.gbp);
    return [...byRegion].map(([regionCode, price]) => ({ regionCode, newSubscriberAvailability: true, price }));
}

/** Checked against developers.google.com/android-publisher/api-ref/rest/v3/monetization.subscriptions (BasePlan, AutoRenewingBasePlanType, RegionalBasePlanConfig, OtherRegionsBasePlanConfig), 2026-11-15. */
function playBasePlanBody(plan, converted) {
    const other = converted?.convertedOtherRegionsPrice;
    const body = {
        basePlanId: plan.id,
        autoRenewingBasePlanType: { billingPeriodDuration: plan.playPeriod, resubscribeState: 'RESUBSCRIBE_STATE_ACTIVE' },
        regionalConfigs: playPlanRegionalConfigs(plan, converted),
    };
    if (other?.usdPrice && other?.eurPrice) body.otherRegionsConfig = { usdPrice: other.usdPrice, eurPrice: other.eurPrice, newSubscriberAvailability: true };
    return body;
}

/** Pure: the 7-day free trial offer of one base plan (POST .../basePlans/<plan>/offers?offerId=trial&regionsVersion.version=...): free for new subscribers of this subscription, in every region the plan is sold in. Checked against monetization.subscriptions.basePlans.offers.create and the SubscriptionOffer, SubscriptionOfferPhase (recurrenceCount, duration ISO 8601, one phase config per offer region), RegionalSubscriptionOfferPhaseConfig (free: {}), OtherRegionsSubscriptionOfferPhaseConfig (free), RegionalSubscriptionOfferConfig, OtherRegionsSubscriptionOfferConfig (otherRegionsNewSubscriberAvailability) and AcquisitionTargetingRule (scope.thisSubscription) pages, 2026-11-15. No offerTags are sent: the docs name the field but not the shape of an OfferTag, and the app picks the offer by its id. */
export function playTrialOfferBody(plan, regionCodes, withOtherRegions = true) {
    const phase = { recurrenceCount: 1, duration: `P${TRIAL_DAYS}D`, regionalConfigs: regionCodes.map((regionCode) => ({ regionCode, free: {} })) };
    const body = {
        packageName: PACKAGE_NAME,
        productId: SUBSCRIPTION_PRODUCT_ID,
        basePlanId: plan.id,
        offerId: TRIAL_OFFER_ID,
        phases: [phase],
        regionalConfigs: regionCodes.map((regionCode) => ({ regionCode, newSubscriberAvailability: true })),
        targeting: { acquisitionRule: { scope: { thisSubscription: {} } } },
    };
    // Only a base plan that has an otherRegionsConfig can have an offer with one.
    if (withOtherRegions) {
        phase.otherRegionsConfig = { free: {} };
        body.otherRegionsConfig = { otherRegionsNewSubscriberAvailability: true };
    }
    return body;
}

/** Pure: the held base plan of a subscription Play answered. */
function playHeldPlan(subscription, planId) {
    return (subscription?.basePlans ?? []).find((entry) => entry?.basePlanId === planId);
}

/** Pure: whether a held base plan's GB price is the plan's own (same units and nanos); a plan with no GB price never matches. */
export function playHeldGbPriceMatches(subscription, plan) {
    const price = (playHeldPlan(subscription, plan.id)?.regionalConfigs ?? []).find((config) => config?.regionCode === PLAY_REGION_GB)?.price;
    return price !== undefined && String(price.units ?? '0') === String(plan.gbp.units) && Number(price.nanos ?? 0) === plan.gbp.nanos;
}

/** Pure: where a Play subscription stands: no subscription (`missing`), a base plan or trial not live (`inactive`), or all live (`active`). */
export function playSubscriptionStage(subscription, trials) {
    const plans = subscription?.basePlans ?? [];
    if (SUBSCRIPTION_PLANS.some((plan) => !plans.some((base) => base?.basePlanId === plan.id))) return PlayStage.Missing;
    const live = SUBSCRIPTION_PLANS.every((plan) => plans.find((base) => base.basePlanId === plan.id).state === PLAY_STATE_ACTIVE && trials?.[plan.id] === PLAY_STATE_ACTIVE);
    return live ? PlayStage.Active : PlayStage.Inactive;
}

/** The state of a plan's trial offer from its GET answer: undefined only for a 404 with a JSON body; any other failure throws, never a reason to create (a duplicate would be posted). */
function playOfferStateOf(answer, plan) {
    if (answer.nonJson === true) throw nonJsonError(`look up the ${plan.id} trial offer`, answer);
    if (answer.ok) return answer.json?.state ?? 'UNKNOWN';
    if (answer.status !== HTTP_NOT_FOUND) throw new Error(`look up the ${plan.id} trial offer: HTTP ${answer.status}`);
    return undefined;
}

/** Pure: the regions a held base plan is sold in (its regionalConfigs); throws when it names none, so a trial is never made for GB only. */
export function playHeldRegions(subscription, planId) {
    const codes = (playHeldPlan(subscription, planId)?.regionalConfigs ?? []).map((config) => config?.regionCode).filter((code) => typeof code === 'string');
    if (codes.length === 0) throw new Error(`Play lists no regions for the ${planId} base plan, so the trial offer's regions are unknown`);
    return codes;
}

/** Pure: a Play request that sends a GET again after a 429 or 5xx and a write again only after a 429 (Play did not apply it), up to APPLE_RETRY_MAX times, waiting Retry-After when given. */
export function retryingPlay(request, sleep = realSleep, log = () => {}) {
    return async (token, method, path, body) => {
        let response = await request(token, method, path, body);
        for (let attempt = 0; attempt < APPLE_RETRY_MAX && canRetryPlay(method, response); attempt++) {
            log(`  Play answered ${response.status} to ${method} ${path.split('?')[0]}; trying again (${attempt + 1} of ${APPLE_RETRY_MAX})`);
            await sleep(appleRetryDelayMs(response.retryAfter, attempt));
            response = await request(token, method, path, body);
        }
        return response;
    };
}

function canRetryPlay(method, response) {
    return method === 'GET' ? response.status === HTTP_TOO_MANY || response.status >= HTTP_SERVER_ERROR : response.status === HTTP_TOO_MANY;
}

/** Pure: the regions version a Play write uses: PLAY_REGIONS_VERSION, else the version Play named in a conversion, else the default. */
export function playRegionsVersion(converted) {
    return process.env.PLAY_REGIONS_VERSION ?? converted?.regionVersion?.version ?? PLAY_DEFAULT_REGIONS_VERSION;
}

/**
 * Creates the Play subscription with both base plans and trial offers, or finishes a half-created one (each base plan and offer that is not live is created or activated). It reads what Play holds first
 * (subscriptions.get, then each base plan's trial offer) and posts only what is missing, so a rerun never posts a duplicate. A plan that fails does not stop the other; the failures are listed in one error.
 */
export async function createPlaySubscription(token, item, rawRequest = playRequest, { sleep = realSleep, log = console.log } = {}) {
    const request = retryingPlay(rawRequest, sleep, log);
    const base = `subscriptions/${encodeURIComponent(SUBSCRIPTION_PRODUCT_ID)}`;
    const existing = await request(token, 'GET', base);
    if (!existing.ok && existing.status !== HTTP_NOT_FOUND) throw new Error(`look up subscription: HTTP ${existing.status}`);
    if (existing.nonJson === true) throw nonJsonError('look up subscription', existing);
    const trials = {};
    for (const plan of item.plans) trials[plan.id] = existing.ok ? playOfferStateOf(await request(token, 'GET', `${base}/basePlans/${plan.id}/offers/${TRIAL_OFFER_ID}`), plan) : undefined;
    const stage = existing.ok ? playSubscriptionStage(existing.json, trials) : PlayStage.Missing;
    log(`  ${SUBSCRIPTION_PRODUCT_ID}: Play holds ${existing.ok ? 'the subscription' : 'nothing'} (${stage})`);
    if (stage === PlayStage.Active) return Outcome.Exists;
    if (existing.ok && stage === PlayStage.Missing) throw new Error(`the subscription exists without all its base plans; Play's create call cannot add them, so add the missing base plan(s) in Play Console (${base})`);
    const { regionsQuery, converted } = existing.ok ? await playQueryForHeld(token, existing.json, item, request) : await playCreateSubscription(token, item, request, log);
    const failed = [];
    for (const plan of item.plans) {
        try {
            await finishPlayPlan(token, plan, { held: existing.ok ? existing.json : undefined, offerState: trials[plan.id], converted: converted[plan.id], regionsQuery, request, log });
        } catch (error) {
            failed.push(`${plan.id} (${error.message})`);
        }
    }
    if (failed.length > 0) throw new Error(`${failed.length} of ${item.plans.length} plans failed, rerun to finish only those: ${failed.join('; ')}`);
    return existing.ok ? Outcome.Completed : Outcome.Created;
}

const MICROS = 1e6;
const NANOS_PER_MICRO = 1000;
const PLAY_PREVIEW_REGIONS = ['GB', 'US', 'DE', 'JP', 'AU', 'CA'];

/** Pure: a Money as whole millionths of its unit. */
function moneyMicros(money) {
    return Math.round(Number(money.units ?? 0) * MICROS + Number(money.nanos ?? 0) / NANOS_PER_MICRO);
}

/** Pure: a Money from whole millionths of a unit. */
function moneyFromMicros(currencyCode, micros) {
    return { currencyCode, units: String(Math.floor(micros / MICROS)), nanos: (micros % MICROS) * NANOS_PER_MICRO };
}

/**
 * Pure: the tax-exclusive GB price to convert from, so that the plan's price is what a GB customer pays. Play's convertRegionPrices takes a tax-EXCLUSIVE price and answers tax-INCLUSIVE prices
 * ("price: The intital price to convert other regions from. Tax exclusive." / "ConvertedRegionPrice.price: The converted price tax inclusive."), so converting 1.99 straight would give GB 1.99 plus VAT
 * and every other region a price built on that inflated base. `firstAnswer` is the conversion of the plan's price as is: its GB answer is 1.99 times (1 + VAT), so the exclusive price is 1.99 / (1 + VAT).
 * Returns null when the answer names no GB price.
 */
export function playTaxExclusiveInput(plan, firstAnswer) {
    const gross = firstAnswer?.convertedRegionPrices?.[PLAY_REGION_GB]?.price;
    if (gross === undefined) return null;
    const wanted = moneyMicros(plan.gbp);
    return moneyFromMicros(plan.gbp.currencyCode, Math.round((wanted * wanted) / moneyMicros(gross)));
}

/** Pure: the preview lines for a conversion: the plan's GB price next to the converted price (tax inclusive) of 5 big regions. */
export function playPriceLines(plan, converted) {
    const priceOf = (code) => (code === PLAY_REGION_GB ? plan.gbp : converted?.convertedRegionPrices?.[code]?.price);
    const parts = PLAY_PREVIEW_REGIONS.map((code) => {
        const price = priceOf(code);
        return `${code} ${price === undefined ? 'none' : `${(moneyMicros(price) / MICROS).toFixed(2)} ${price.currencyCode}`}`;
    });
    return [`  ${plan.id} prices (tax inclusive, converted from the tax-exclusive GB price): ${parts.join(', ')}`];
}

/** Converts a plan's GB price the way Play Console does for a typed base price: from its tax-exclusive equivalent, so no region is priced from a VAT-inflated base. */
async function playConvertPlan(request, token, plan, log) {
    const first = must(await request(token, 'POST', 'pricing:convertRegionPrices', { price: plan.gbp }), `convert the ${plan.id} price`);
    const input = playTaxExclusiveInput(plan, first);
    if (input === null) {
        throw new Error(`${plan.id}: Play's conversion named no GB price, so the tax-exclusive base is unknown; nothing was created (converting from ${plan.gbpLabel} as it is would price other regions from a VAT-inflated base)`);
    }
    return must(await request(token, 'POST', 'pricing:convertRegionPrices', { price: input }), `convert the ${plan.id} tax-exclusive price`);
}

/** For a subscription Play already holds: PLAY_REGIONS_VERSION, else the version the held resource names, else one conversion call to read Play's current version (the offer POST needs the version the plans were made with). */
async function playQueryForHeld(token, held, item, request) {
    let version = process.env.PLAY_REGIONS_VERSION ?? held?.regionsVersion?.version;
    if (version === undefined) {
        const answer = must(await request(token, 'POST', 'pricing:convertRegionPrices', { price: item.plans[0].gbp }), 'read the Play regions version');
        version = playRegionsVersion(answer);
    }
    return { regionsQuery: `regionsVersion.version=${encodeURIComponent(version)}`, converted: {} };
}

/** Converts each plan's price, then creates the subscription (both base plans, in DRAFT) in one call. */
async function playCreateSubscription(token, item, request, log) {
    const converted = {};
    for (const plan of item.plans) {
        converted[plan.id] = await playConvertPlan(request, token, plan, log);
        for (const line of playPriceLines(plan, converted[plan.id])) log(line);
    }
    const regionsQuery = `regionsVersion.version=${encodeURIComponent(playRegionsVersion(converted[item.plans[0].id]))}`;
    log(`  ${SUBSCRIPTION_PRODUCT_ID}: creating the subscription with ${item.plans.length} base plans`);
    must(await request(token, 'POST', `subscriptions?productId=${encodeURIComponent(SUBSCRIPTION_PRODUCT_ID)}&${regionsQuery}`, playSubscriptionBody(converted)), 'create the subscription');
    return { regionsQuery, converted };
}

/** Pure: whether a conversion answer names the GB region (the plans are sold from GBP). */
export function playHasGbPrice(converted) {
    return converted?.convertedRegionPrices?.[PLAY_REGION_GB] !== undefined;
}

/** Activates one base plan that is not live, then creates (when missing) and activates its trial offer. */
async function finishPlayPlan(token, plan, { held, offerState, converted, regionsQuery, request, log }) {
    const base = `subscriptions/${encodeURIComponent(SUBSCRIPTION_PRODUCT_ID)}/basePlans/${plan.id}`;
    const planState = playHeldPlan(held, plan.id)?.state;
    if (held && !playHeldGbPriceMatches(held, plan)) throw new Error(`the held ${plan.id} base plan's GB price is not ${plan.gbpLabel} ${SUBSCRIPTION_CURRENCY}; no offer is made for it and it is not activated, fix the price in Play Console (${base})`);
    if (planState !== PLAY_STATE_ACTIVE) {
        must(await request(token, 'POST', `${base}:activate`, {}), `activate the ${plan.id} base plan`);
        log(`  ${plan.productId}: base plan activated`);
    }
    if (offerState === PLAY_STATE_ACTIVE) return;
    if (offerState !== undefined && offerState !== PLAY_STATE_DRAFT) {
        log(`  ${plan.productId}: the trial offer is ${offerState}; left alone (never re-activated by this script), activate it in Play Console if it should be live (${base}/offers/${TRIAL_OFFER_ID})`);
        return;
    }
    if (offerState === undefined) {
        const regions = held ? playHeldRegions(held, plan.id) : playPlanRegionalConfigs(plan, converted).map((config) => config.regionCode);
        const other = converted?.convertedOtherRegionsPrice;
        const withOther = held ? playHeldPlan(held, plan.id)?.otherRegionsConfig !== undefined : Boolean(other?.usdPrice && other?.eurPrice);
        must(await request(token, 'POST', `${base}/offers?offerId=${TRIAL_OFFER_ID}&${regionsQuery}`, playTrialOfferBody(plan, regions, withOther)),`create the ${plan.id} trial offer`);
        log(`  ${plan.productId}: trial offer created for ${regions.length} regions`);
    }
    must(await request(token, 'POST', `${base}/offers/${TRIAL_OFFER_ID}:activate`, {}), `activate the ${plan.id} trial offer`);
    log(`  ${plan.productId}: trial offer activated`);
}

const PLAY_REGION_GB = 'GB';

/** Pure: App Store Connect's subscription group (POST /v1/subscriptionGroups). */
export function appleSubscriptionGroupBody(appId) {
    return { data: { type: 'subscriptionGroups', attributes: { referenceName: APPLE_GROUP_NAME }, relationships: { app: { data: { type: 'apps', id: appId } } } } };
}

/** Pure: the group's en-US display name (POST /v1/subscriptionGroupLocalizations). Checked against Apple's docs (subscription-group-localizations-v1, 2026-11-15). */
export function appleGroupLocalizationBody(groupId) {
    return { data: { type: 'subscriptionGroupLocalizations', attributes: { name: 'Dino Book Reader', locale: APPLE_LOCALE }, relationships: { subscriptionGroup: { data: { type: 'subscriptionGroups', id: groupId } } } } };
}

/** Pure: one auto-renewing subscription of the group (POST /v1/subscriptions; checked against Apple's docs, managing-auto-renewable-subscriptions, 2026-11-15, minus availableInAllTerritories, which the live API rejected on 2026-11-15); the yearly plan is level 1 (the best), the monthly level 2. */
export function appleSubscriptionBody(item, groupId) {
    return {
        data: {
            type: 'subscriptions',
            attributes: { name: `Every book (${item.plan.id})`, productId: item.productId, subscriptionPeriod: item.plan.applePeriod, familySharable: false, groupLevel: item.plan.appleLevel, reviewNote: 'Opens every book while subscribed. Purchases sit behind the grown-ups gate.' },
            relationships: { group: { data: { type: 'subscriptionGroups', id: groupId } } },
        },
    };
}

/** Pure: the en-US name and description of an Apple subscription (POST /v1/subscriptionLocalizations). Checked against Apple's docs (managing-auto-renewable-subscriptions, 2026-11-15; the pre-4.4.1 route, deprecated but available). */
export function appleSubscriptionLocalizationBody(item, subscriptionId) {
    return { data: { type: 'subscriptionLocalizations', attributes: { name: SUBSCRIPTION_TITLE, description: cut(SUBSCRIPTION_DESCRIPTION, APPLE_DESCRIPTION_MAX), locale: APPLE_LOCALE }, relationships: { subscription: { data: { type: 'subscriptions', id: subscriptionId } } } } };
}

/** Pure: the territory availability of a subscription (POST /v1/subscriptionAvailabilities): every territory, as Apple adds new ones. The create call itself has no availableInAllTerritories (Apple answered so on the first --apply). */
export function appleSubscriptionAvailabilityBody(subscriptionId, territoryIds) {
    return {
        data: {
            type: 'subscriptionAvailabilities',
            attributes: { availableInNewTerritories: true },
            relationships: {
                subscription: { data: { type: 'subscriptions', id: subscriptionId } },
                availableTerritories: { data: territoryIds.map((id) => ({ type: 'territories', id })) },
            },
        },
    };
}

/** Pure: the price of one territory (POST /v1/subscriptionPrices): the price point's id (it belongs to one territory), applied now (startDate null). The base territory gets the chosen price point, every other territory its equalised one. */
export function appleSubscriptionPriceBody(subscriptionId, pricePointId) {
    return {
        data: {
            type: 'subscriptionPrices',
            attributes: { startDate: null, preserveCurrentPrice: false },
            relationships: { subscription: { data: { type: 'subscriptions', id: subscriptionId } }, subscriptionPricePoint: { data: { type: 'subscriptionPricePoints', id: pricePointId } } },
        },
    };
}

/** Pure: the 7-day free trial of one territory (POST /v1/subscriptionIntroductoryOffers): duration ONE_WEEK, one period, FREE_TRIAL; an offer is per territory. */
export function appleTrialBody(subscriptionId, territoryId = APPLE_SUBSCRIPTION_TERRITORY) {
    return {
        data: {
            type: 'subscriptionIntroductoryOffers',
            attributes: { duration: 'ONE_WEEK', offerMode: 'FREE_TRIAL', numberOfPeriods: 1 },
            relationships: {
                subscription: { data: { type: 'subscriptions', id: subscriptionId } },
                territory: { data: { type: 'territories', id: territoryId } },
            },
        },
    };
}

/** Creates one Apple subscription (and the group once per run), or completes a half-created one: group, product, listing, price and trial are each checked and only the missing ones are done. */
export async function createAppleSubscription(asc, item, appId, session = {}) {
    session.groupId ??= await ensureAppleGroup(asc, appId);
    const found = must(await asc.get(`/v1/subscriptionGroups/${session.groupId}/subscriptions`), 'look up subscription');
    const existingId = (found.data ?? []).find((entry) => entry?.attributes?.productId === item.productId)?.id;
    const id = existingId ?? must(await asc.post('/v1/subscriptions', appleSubscriptionBody(item, session.groupId)), 'create subscription').data.id;
    const fresh = existingId === undefined;
    let did = fresh;
    const missing = async (path, what) => fresh || !(await appleStepDone(asc, path, what));
    if (await missing(`/v1/subscriptions/${id}/subscriptionLocalizations`, 'the en-US listing')) {
        must(await asc.post('/v1/subscriptionLocalizations', appleSubscriptionLocalizationBody(item, id)), 'add the en-US listing');
        did = true;
    }
    if (await missing(`/v1/subscriptions/${id}/subscriptionAvailability`, 'the availability')) {
        must(await asc.post('/v1/subscriptionAvailabilities', appleSubscriptionAvailabilityBody(id, await appleTerritoryIds(asc))), 'set availability');
        did = true;
    }
    if (await appleSubscriptionPricesAndTrial(asc, item, id, fresh, session)) did = true;
    if (fresh) return Outcome.Created;
    return did ? Outcome.Completed : Outcome.Exists;
}

/**
 * What a subscription's prices or introductory offers already cover: `territories` (the ids its entries name) and `unplaced` (true when an entry names no territory, e.g. one made by hand for all territories).
 * Apple's docs (context7 /websites/developer_apple_appstoreconnectapi, 2026-11-15) show a territory relationship with data only when asked with include=territory, and say nothing of an all-territories entry,
 * so an entry that names none is read as "there is already one here" and nothing of that kind is posted. None on a fresh subscription.
 */
async function appleDoneTerritories(asc, path, what, fresh) {
    if (fresh) return { territories: new Set(), unplaced: false };
    const entries = await appleListAll(asc, `${path}?include=territory&limit=${APPLE_TERRITORY_LIMIT}`, `check ${what}`, { missingIsEmpty: true });
    const named = entries.map(entryTerritory);
    return { territories: new Set(named.filter((territory) => territory !== null)), unplaced: named.includes(null) };
}

/** Pure: the territory id an entry of a list read with include=territory names, or null when it names none. */
export function entryTerritory(entry) {
    const territory = entry?.relationships?.territory?.data?.id;
    return typeof territory === 'string' ? territory : null;
}

/** Pure: the price point per territory for a plan: the base territory's own point, then its equalised points (the list of Apple's equalisation answers). A point that names no territory cannot be placed and is left out. */
export function applePricePlan(basePointId, equalized) {
    const plan = new Map([[APPLE_SUBSCRIPTION_TERRITORY, basePointId]]);
    for (const point of equalized) {
        const territory = entryTerritory(point);
        if (territory !== null && typeof point.id === 'string' && !plan.has(territory)) plan.set(territory, point.id);
    }
    return plan;
}

/** Pure: the plan narrowed to the territories of the availability; `skipped` lists the availability territories the plan has no price point for (they are reported, not posted). */
export function applePlanForAvailability(plan, territoryIds) {
    const skipped = territoryIds.filter((territory) => !plan.has(territory));
    return { plan: new Map(territoryIds.filter((territory) => plan.has(territory)).map((territory) => [territory, plan.get(territory)])), skipped };
}

/** The territory ids Apple actually holds in a subscription's availability, read back (the availability may be partial or older than this run). */
async function appleHeldTerritoryIds(asc, id) {
    const availability = must(await asc.get(`/v1/subscriptions/${id}/subscriptionAvailability`), 'read the availability back').data;
    if (typeof availability?.id !== 'string') throw new Error('read the availability back: Apple gave no availability id');
    const territories = await appleListAll(asc, `/v1/subscriptionAvailabilities/${availability.id}/availableTerritories?limit=${APPLE_TERRITORY_LIMIT}`, 'read the available territories');
    return territories.map((territory) => territory.id);
}

/** Every entry of a paged App Store Connect list, following every `links.next`; a list longer than the safety cap is an error, never a silent cut. */
async function appleListAll(asc, path, what, { missingIsEmpty = false } = {}) {
    const all = [];
    for (let page = 0; path !== null && page < APPLE_LIST_MAX_PAGES; page++) {
        const response = await asc.get(path);
        if (missingIsEmpty && page === 0 && response.status === HTTP_NOT_FOUND) return [];
        const json = must(response, what);
        all.push(...(json.data ?? []));
        path = applePagePath(json.links?.next);
    }
    if (path !== null) throw new Error(`${what}: more than ${APPLE_LIST_MAX_PAGES} pages, so the list would be incomplete`);
    return all;
}

/** Pure: the delay before a retry, in ms: Apple's Retry-After (seconds) when given, else doubling from one second; capped. */
export function appleRetryDelayMs(retryAfter, attempt) {
    const seconds = Number(retryAfter);
    const given = retryAfter !== null && retryAfter !== undefined && retryAfter !== '' && Number.isFinite(seconds) && seconds >= 0;
    return Math.min(given ? seconds * 1000 : APPLE_RETRY_BASE_MS * 2 ** attempt, APPLE_RETRY_MAX_MS);
}

const realSleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/** Whether a response may be sent again: a GET after a 429 or 5xx; a POST only after a 429 that names a Retry-After (Apple did not accept it). A POST that got a 5xx may have been applied, so it is never repeated. */
function canRetry(isPost, response) {
    if (isPost) return response.status === HTTP_TOO_MANY && response.retryAfter !== null && response.retryAfter !== undefined && response.retryAfter !== '';
    return response.status === HTTP_TOO_MANY || response.status >= HTTP_SERVER_ERROR;
}

/** An App Store Connect client whose GETs retry a 429 or 5xx, and whose POSTs retry only a 429 with Retry-After (honouring it), a fixed number of times. */
export function retryingAsc(asc, sleep = realSleep) {
    const again = (call, isPost) => async (...args) => {
        let response = await call(...args);
        for (let attempt = 0; attempt < APPLE_RETRY_MAX && canRetry(isPost, response); attempt++) {
            await sleep(appleRetryDelayMs(response.retryAfter, attempt));
            response = await call(...args);
        }
        return response;
    };
    return { get: again((...args) => asc.get(...args), false), post: again((...args) => asc.post(...args), true) };
}

/** Sets the price (GBR point plus equalised points for every other territory) and the 7-day free trial, in each territory that lacks them. True when anything was posted. */
async function appleSubscriptionPricesAndTrial(plainAsc, item, id, fresh, session) {
    const asc = retryingAsc(plainAsc, session.sleep);
    session.notes ??= [];
    const priced = await appleDoneTerritories(asc, `/v1/subscriptions/${id}/prices`, 'the prices', fresh);
    const trialled = await appleDoneTerritories(asc, `/v1/subscriptions/${id}/introductoryOffers`, 'the free trial', fresh);
    if (priced.unplaced) session.notes.push(`${item.productId}: a price entry names no territory (made by hand?), so no price was posted; check it in App Store Connect`);
    if (trialled.unplaced) session.notes.push(`${item.productId}: a free-trial entry names no territory (made by hand?), so no trial was posted; check it in App Store Connect`);
    const points = await appleListAll(asc, `/v1/subscriptions/${id}/pricePoints?filter[territory]=${APPLE_SUBSCRIPTION_TERRITORY}&limit=${APPLE_PRICE_POINT_LIMIT}`, 'read the price points');
    const pointId = findApplePricePoint(points, item.plan.gbpLabel);
    if (pointId === null) throw new Error(`no ${item.plan.gbpLabel} ${SUBSCRIPTION_CURRENCY} price point for ${item.productId}`);
    const equalized = await appleListAll(asc, `/v1/subscriptionPricePoints/${pointId}/equalizations?include=territory&limit=${APPLE_PRICE_POINT_LIMIT}`, 'read the equalised prices');
    const { plan, skipped } = applePlanForAvailability(applePricePlan(pointId, equalized), await appleHeldTerritoryIds(asc, id));
    if (skipped.length > 0) session.notes.push(`${item.productId}: ${skipped.length} territory(ies) have no equalised price point and were skipped: ${skipped.join(', ')}`);
    return appleSetTerritories(asc, item.productId, id, plan, { priced, trialled }, session);
}

/** Posts the missing price and trial of each territory; a territory whose POST failed is recorded and the loop goes on, then the run fails naming them (a rerun reads what is done first and does only those). True when anything was posted. */
async function appleSetTerritories(asc, productId, id, plan, { priced, trialled }, session) {
    const log = session.log ?? console.log;
    let did = false;
    let posted = 0;
    let position = 0;
    const failed = [];
    for (const [territory, territoryPointId] of plan) {
        try {
            if (!priced.unplaced && !priced.territories.has(territory)) {
                must(await asc.post('/v1/subscriptionPrices', appleSubscriptionPriceBody(id, territoryPointId)), `set the ${territory} price`);
                did = true;
            }
            if (!trialled.unplaced && !trialled.territories.has(territory)) {
                must(await asc.post('/v1/subscriptionIntroductoryOffers', appleTrialBody(id, territory)), `add the ${territory} free trial`);
                did = true;
            }
            posted++;
        } catch (error) {
            failed.push(`${territory} (${error.message})`);
        }
        if (++position % APPLE_PROGRESS_EVERY === 0) log(`  ${productId}: ${position} of ${plan.size} territories handled (${posted} ok, ${failed.length} failed)`);
    }
    if (failed.length > 0) throw new Error(`${failed.length} of ${plan.size} territories failed, rerun to finish only those: ${failed.join('; ')}`);
    return did;
}

/** The group of the app, created with its en-US display name when missing; a group the failed first run left without a display name gets it now. */
async function ensureAppleGroup(asc, appId) {
    const found = must(await asc.get(`/v1/apps/${appId}/subscriptionGroups`), 'look up subscription group');
    const existing = (found.data ?? []).find((group) => group?.attributes?.referenceName === APPLE_GROUP_NAME)?.id;
    const groupId = existing ?? must(await asc.post('/v1/subscriptionGroups', appleSubscriptionGroupBody(appId)), 'create subscription group').data.id;
    if (existing === undefined || (await appleGroupListingMissing(asc, groupId))) {
        must(await asc.post('/v1/subscriptionGroupLocalizations', appleGroupLocalizationBody(groupId)), 'add the group listing');
    }
    return groupId;
}

/** True only when the group's localization list was read successfully and is empty; a 404 or any other failure stops the step (posting again on a guess would hit a 409). */
async function appleGroupListingMissing(asc, groupId) {
    const response = await asc.get(`/v1/subscriptionGroups/${groupId}/subscriptionGroupLocalizations`);
    if (!response.ok) throw new Error(`check the group listing: could not read it (HTTP ${response.status}), so the group's display name was not posted; rerun when App Store Connect answers`);
    const data = response.json?.data;
    if (!Array.isArray(data)) throw new Error('check the group listing: Apple gave no list, so the group\'s display name was not posted');
    return data.length === 0;
}

/** Pure: Partner Center's create-add-on body for a subscription shell. UNVERIFIED: that the submission API accepts productType Subscription; if it answers 409 or an error, create the add-on by hand. */
export function microsoftSubscriptionBody(item, storeAppId) {
    return { applicationIds: [storeAppId], productId: item.productId, productType: 'Subscription' };
}

// ---- running ----

async function openStore(store) {
    if (store === Store.Apple) return { asc: appleClientFromEnv(), appId: APPS.dino.id, notes: [] };
    if (store === Store.Google) return { token: await googleToken() };
    return { token: await microsoftToken(), existing: undefined, storeAppId: requireEnv('MS_STORE_APP_ID') };
}

async function createOne(store, session, book) {
    if (store === Store.Apple && book.kind === 'subscription') return createAppleSubscription(session.asc, book, session.appId, session);
    if (store === Store.Google && book.kind === 'subscription') return createPlaySubscription(session.token, book);
    if (store === Store.Apple) return createAppleProduct(session.asc, book, session.appId);
    if (store === Store.Google) return createPlayProduct(session.token, book);
    if (book.kind !== 'subscription') return createMicrosoftBook(session.token, book, session);
    session.existing ??= await microsoftExistingIds(session.token);
    return createMicrosoftProduct(session.token, book, session.existing, session.storeAppId);
}

/** Creates the products of one store; a book that fails does not stop the others. Returns `{ book, outcome, message }` per book. */
export async function applyStore(store, books, open = openStore, create = createOne) {
    const session = await open(store);
    const results = [];
    let rateLimited = null; // once the 429 budget is spent every later book of the store would only be refused again, so it ends the same way without a call
    for (const book of books) {
        if (book.problem !== null) {
            results.push({ book, outcome: Outcome.Failed, message: book.problem });
            continue;
        }
        if (rateLimited !== null) {
            results.push({ book, outcome: Outcome.RateLimited, message: rateLimited });
            continue;
        }
        try {
            results.push({ book, ...resultOf(await create(store, session, book)) });
        } catch (error) {
            if (error instanceof RateLimitedError) rateLimited = error.message;
            results.push({ book, outcome: error instanceof RateLimitedError ? Outcome.RateLimited : Outcome.Failed, message: error.message });
        }
    }
    results.notes = session?.notes ?? []; // things a run left undone without failing (skipped territories ...), printed in the end report
    return results;
}

/** Pure: a create step answers an outcome string, or `{ outcome, message }` when it has something to print (the Microsoft Store id). */
export function resultOf(answer) {
    return typeof answer === 'string' ? { outcome: answer, message: '' } : { outcome: answer.outcome, message: answer.message ?? '' };
}

function printResults(store, results, withSubscription) {
    console.log(`${STORE_TITLES[store]}:`);
    for (const { book, outcome, message } of results) console.log(`  ${outcome}: ${book.productId}${message ? ` (${message})` : ''}`);
    (results.notes ?? []).forEach((note) => console.log(`  NOT DONE: ${note}`));
    MANUAL_STEPS[store].forEach((step) => console.log(`  by hand: ${step}`));
    if (withSubscription) SUBSCRIPTION_MANUAL_STEPS[store].forEach((step) => console.log(`  by hand (subscription): ${step}`));
}

function priceLabel(problem, unreadable) {
    if (unreadable !== null) return 'COULD NOT READ';
    return problem === null ? 'ok' : 'PROBLEM';
}

/** `--check-prices`: read-only. Lists the add-ons, reads each book's public catalog price and prints one line per book; sets the exit code on any price that is not USD 0.99. */
async function runPriceCheck(books) {
    const addOns = await microsoftAddOnMap(await microsoftToken(), retryingMicrosoft(microsoftRequest, { log: console.log }));
    const results = await checkMicrosoftPrices(books, addOns);
    for (const { book, storeId, problem, unreadable } of results) console.log(`  ${priceLabel(problem, unreadable)}: ${book.productId} (${storeId ?? 'no add-on'})${problem === null && unreadable === null ? ` USD ${PRICE_USD}` : `: ${problem ?? unreadable}`}`);
    if (results.some((result) => result.problem !== null || result.unreadable !== null)) process.exitCode = 1; // an unreadable catalog is not a wrong price, but the check did not pass either
}

/** True when this module is the script node was started with; real paths are compared so a drive-letter case, slashes or a junction do not make `main` silently skip. */
export function isEntryScript(moduleUrl, argvPath = process.argv[1]) {
    if (!argvPath) return false;
    try {
        return realpathSync.native(argvPath) === realpathSync.native(fileURLToPath(moduleUrl));
    } catch {
        // no-fallback: a path that cannot be resolved is simply not this module
        return false;
    }
}

async function main() {
    const options = parseArgs(process.argv.slice(2));
    const books = selectBooks(paidBooksFrom(readContentEntries(options.contentRoot)), options.book);
    if (options.checkPrices) return runPriceCheck(books);
    if (!options.apply) {
        console.log(`DRY RUN (nothing is created; add --apply to create): ${books.length} paid book(s)`);
        options.stores.forEach((store) => console.log([...planLines(store, books), ...(options.book === null ? subscriptionPlanLines(store) : [])].join('\n')));
        if (books.some((book) => book.problem !== null)) process.exitCode = 1; // review #9: a scripted pre-check must not pass what --apply would refuse
        return;
    }
    let failed = false;
    for (const store of options.stores) {
        try {
            const results = await applyStore(store, itemsForStore(store, books, options)); // Slice 384: the subscription after the books, through the same one-failure-never-stops-the-rest loop (Slice 411: none with --book)
            failed ||= results.some((result) => result.outcome === Outcome.Failed || result.outcome === Outcome.RateLimited);
            printResults(store, results, options.book === null);
        } catch (error) {
            failed = true;
            console.log(`${STORE_TITLES[store]}: FAILED before any product: ${error.message}`);
        }
    }
    if (failed) process.exitCode = 1;
}

if (isEntryScript(import.meta.url)) {
    main().catch((error) => {
        console.error(error.message);
        process.exitCode = 1;
    });
}
