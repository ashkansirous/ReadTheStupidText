// Run: node --test worker/store-notifications.test.mjs (Slice 458: the stores' server notifications. Apple payloads are signed with the throwaway chain of apple-jws-fixture.mjs; Google's push token with a test RSA key whose JWK the recorded fetch serves; Play's answers are recorded fixtures.)
import test from "node:test";
import assert from "node:assert/strict";
import { createHash, generateKeyPairSync, sign } from "node:crypto";

import { INTERMEDIATE_DER_BASE64, LEAF_DER_BASE64, LEAF_PRIVATE_KEY_PEM, ROOT_DER_BASE64 } from "./apple-jws-fixture.mjs";
import { base64UrlEncode } from "./entitlements/jwt.ts";
import { isStoreCount, parseEvent, toDataPoint } from "./events.ts";
import { APP_PACKAGE_ID, SUBSCRIPTION_MONTHLY_PRODUCT, SUBSCRIPTION_YEARLY_PRODUCT } from "./products.ts";
import { decideApple, isTrialConversion, subscriptionKindOf } from "./store-notifications/apple.ts";
import { countryOfStorefront } from "./store-notifications/countries.ts";
import { alreadySeen, markSeen } from "./store-notifications/dedup.ts";
import { resetGoogleStoreCaches, verifyPushToken } from "./store-notifications/google.ts";
import { bookIdOfMetaKey, bookResolverFor } from "./store-notifications/books.ts";
import { handleStoreNotification, storeEvent } from "./store-notifications/handler.ts";

const DAY = 86_400_000;
const SIGNED_AT = Date.UTC(2027, 0, 1);
const TEST_ROOT = createHash("sha256").update(Buffer.from(ROOT_DER_BASE64, "base64")).digest("hex");
const APPLE = { roots: [TEST_ROOT] };
const CHAIN = [LEAF_DER_BASE64, INTERMEDIATE_DER_BASE64, ROOT_DER_BASE64];
const BOOK_ID = "0123456789abcdef0123456789abcdef";
const BOOK_PRODUCT = `${APP_PACKAGE_ID}.book.goldilocks`;
const encode = (value) => base64UrlEncode(Buffer.from(JSON.stringify(value)));
const books = async (productId) => (productId === BOOK_PRODUCT ? BOOK_ID : null);

function jws(payload) {
  const input = `${encode({ alg: "ES256", x5c: CHAIN })}.${encode(payload)}`;
  return `${input}.${base64UrlEncode(sign("sha256", Buffer.from(input), { key: LEAF_PRIVATE_KEY_PEM, dsaEncoding: "ieee-p1363" }))}`;
}

const transaction = (extra = {}) => ({
  signedDate: SIGNED_AT, bundleId: APP_PACKAGE_ID, productId: SUBSCRIPTION_MONTHLY_PRODUCT, environment: "Production", storefront: "GBR",
  purchaseDate: SIGNED_AT, originalPurchaseDate: SIGNED_AT, expiresDate: SIGNED_AT + 30 * DAY, transactionId: "2000000123", originalTransactionId: "2000000100", ...extra,
});
const TRIAL = { offerType: 1, offerDiscountType: "FREE_TRIAL", expiresDate: SIGNED_AT + 7 * DAY };

function appleBody(type, subtype, tx, { environment = "Production", uuid = "11111111-2222-3333-4444-555555555555" } = {}) {
  const notification = { notificationType: type, notificationUUID: uuid, signedDate: SIGNED_AT, data: { environment, bundleId: APP_PACKAGE_ID, signedTransactionInfo: jws(tx) } };
  if (subtype !== undefined) notification.subtype = subtype;
  return { signedPayload: jws(notification) };
}

const NOW = new Date(SIGNED_AT + 3_600_000); // an hour after the payloads were signed: inside the freshness window
const countOf = async (body) => (await decideApple(body, books, APPLE, NOW));
const detailOf = (outcome) => `${outcome.count.platform}.${outcome.count.kind}.${outcome.count.offer}`;

test("Apple: the mapping table turns type and subtype into a kind", async () => {
  const cases = [
    ["SUBSCRIBED", "INITIAL_BUY", TRIAL, "ios.trial_started.trial"],
    ["SUBSCRIBED", "INITIAL_BUY", {}, "ios.subscribed.paid"],
    ["SUBSCRIBED", "RESUBSCRIBE", {}, "ios.subscribed.paid"],
    ["SUBSCRIBED", "INITIAL_BUY", { offerType: 3 }, "ios.subscribed.code"],
    ["DID_RENEW", undefined, { originalPurchaseDate: SIGNED_AT - 30 * DAY }, "ios.renewed.paid"],
    ["DID_RENEW", "BILLING_RECOVERY", { originalPurchaseDate: SIGNED_AT - 60 * DAY }, "ios.renewed.paid"],
    ["DID_CHANGE_RENEWAL_STATUS", "AUTO_RENEW_DISABLED", {}, "ios.cancelled.paid"],
    ["DID_CHANGE_RENEWAL_STATUS", "AUTO_RENEW_DISABLED", TRIAL, "ios.cancelled.trial"],
    ["EXPIRED", "VOLUNTARY", {}, "ios.expired.paid"],
    ["GRACE_PERIOD_EXPIRED", undefined, {}, "ios.expired.paid"],
    ["DID_FAIL_TO_RENEW", "GRACE_PERIOD", {}, "ios.grace.paid"],
    ["DID_FAIL_TO_RENEW", undefined, {}, "ios.billing_retry.paid"],
    ["REFUND", undefined, {}, "ios.refunded.paid"],
    ["REVOKE", undefined, {}, "ios.refunded.paid"],
    ["OFFER_REDEEMED", "INITIAL_BUY", { offerType: 3 }, "ios.code_redeemed.code"],
  ];
  for (const [type, subtype, extra, expected] of cases) {
    const outcome = await countOf(appleBody(type, subtype, transaction(extra)));
    assert.equal(outcome.type, "count", `${type} ${subtype}`);
    assert.equal(detailOf(outcome), expected, `${type} ${subtype}`);
    assert.equal(outcome.count.source, "sub_monthly");
    assert.equal(outcome.count.country, "GB");
  }
});

test("Apple: the first renewal after a trial is a conversion, an ordinary renewal is not", async () => {
  const afterTrial = transaction({ originalPurchaseDate: SIGNED_AT - 7 * DAY });
  assert.equal(isTrialConversion(afterTrial), true);
  assert.equal(isTrialConversion(transaction({ originalPurchaseDate: SIGNED_AT - 30 * DAY })), false);
  assert.equal(isTrialConversion(transaction()), false);
  assert.equal(detailOf(await countOf(appleBody("DID_RENEW", undefined, afterTrial))), "ios.converted.paid");
});

test("Apple: a yearly plan, a book and an unknown plan name their source", async () => {
  const yearly = await countOf(appleBody("DID_RENEW", undefined, transaction({ productId: SUBSCRIPTION_YEARLY_PRODUCT })));
  assert.equal(yearly.count.source, "sub_yearly");
  const book = await countOf(appleBody("ONE_TIME_CHARGE", undefined, transaction({ productId: BOOK_PRODUCT })));
  assert.deepEqual([detailOf(book), book.count.source, book.count.book], ["ios.book_bought.paid", "shelf", BOOK_ID]);
  const refunded = await countOf(appleBody("REFUND", undefined, transaction({ productId: BOOK_PRODUCT })));
  assert.equal(detailOf(refunded), "ios.book_refunded.paid");
  assert.equal((await countOf(appleBody("ONE_TIME_CHARGE", undefined, transaction({ productId: `${APP_PACKAGE_ID}.book.unknown` })))).type, "ignore");
});

test("Apple: sandbox is a test count, types we do not count and test pings are dropped, a forged payload is refused", async () => {
  assert.equal((await countOf(appleBody("DID_RENEW", undefined, transaction(), { environment: "Sandbox" }))).count.test, true);
  assert.equal((await countOf(appleBody("DID_RENEW", undefined, transaction()))).count.test, false);
  assert.equal((await countOf(appleBody("PRICE_INCREASE", undefined, transaction()))).type, "ignore");
  assert.equal((await countOf({ signedPayload: jws({ notificationType: "TEST", signedDate: SIGNED_AT }) })).type, "ignore");
  assert.equal((await countOf(appleBody("DID_RENEW", undefined, transaction({ bundleId: "other.app" })))).type, "ignore");
  assert.equal((await decideApple({ signedPayload: jws({ notificationType: "DID_RENEW" }) }, books)).type, "refuse"); // the real root is not the test root
  assert.equal((await countOf({ signedPayload: "a.b.c" })).type, "refuse");
  assert.equal((await countOf({})).type, "refuse");
  assert.equal(subscriptionKindOf("EXPIRED", "", {}).kind, "expired");
});

test("storefronts map from alpha-3 to alpha-2, else none", () => {
  assert.deepEqual(["USA", "gbr", "DEU", "NOR", "ZZZ", undefined].map(countryOfStorefront), ["US", "GB", "DE", "NO", "none", "none"]);
});

test("the store rule accepts only the Worker's own well-formed row and refuses an app's", () => {
  const row = (detail, source, book = "none") => ({ name: "store", detail, source, book });
  assert.equal(isStoreCount(row("ios.trial_started.trial", "sub_monthly")), true);
  assert.equal(isStoreCount(row("android.converted.paid", "none")), true);
  assert.equal(isStoreCount(row("ios.book_bought.paid", "shelf", BOOK_ID)), true);
  assert.equal(isStoreCount(row("ios.book_bought.paid", "none")), false, "a book kind needs the shelf source");
  assert.equal(isStoreCount(row("ios.book_bought.trial", "shelf", BOOK_ID)), false);
  assert.equal(isStoreCount(row("ios.renewed.paid", "shelf", BOOK_ID)), false, "a plan kind never names a book");
  assert.equal(isStoreCount(row("windows.renewed.paid", "none")), false);
  assert.equal(isStoreCount(row("ios.bought_it.paid", "none")), false);
  assert.equal(isStoreCount(row("ios.renewed", "none")), false);
  const fromApp = { name: "store", book: "none", language: "und", source: "none", hasRecording: false, version: "1.42.0", platform: "ios", day: "2026-11-27", sampleRate: 1, detail: "ios.renewed.paid" };
  assert.equal(parseEvent(fromApp), null, "an app's batch can never write a store row");
});

test("a store row is a data point with the country in blob10 and the detail in blob9", () => {
  const event = storeEvent({ platform: "ios", kind: "trial_started", offer: "trial", source: "sub_yearly", book: "none", country: "US", test: false }, new Date("2026-11-27T10:00:00Z"));
  const point = toDataPoint(event, { country: "US", device: { osMajor: "", formFactor: "", arch: "", cores: "", ram: "" } });
  assert.equal(point.blobs[0], "store");
  assert.equal(point.blobs[3], "sub_yearly");
  assert.equal(point.blobs[6], "ios");
  assert.equal(point.blobs[7], "2026-11-27");
  assert.equal(point.blobs[8], "ios.trial_started.trial");
  assert.equal(point.blobs[9], "US");
  assert.equal(point.doubles[0], 1);
});

test("dedup drops a second delivery, never remembers a key with no id and counts everything without KV", async () => {
  const kept = new Map();
  const kv = { get: async (key) => kept.get(key) ?? null, put: async (key, value, options) => { kept.set(key, value); assert.ok(options.expirationTtl > 0); } };
  assert.equal(await alreadySeen(kv, "apple:abc"), false);
  assert.equal(await alreadySeen(kv, "apple:abc"), false, "looking does not remember");
  await markSeen(kv, "apple:abc");
  assert.equal(await alreadySeen(kv, "apple:abc"), true);
  await markSeen(kv, "apple:");
  assert.equal(await alreadySeen(kv, "apple:"), false);
  assert.equal(await alreadySeen(undefined, "apple:abc"), false);
});

test("a book id comes from its meta.json key only", () => {
  assert.equal(bookIdOfMetaKey(`dinoreader/stories/${BOOK_ID}/meta.json`), BOOK_ID);
  assert.equal(bookIdOfMetaKey(`dinoreader/stories/${BOOK_ID}/cover.jpg`), null);
  assert.equal(bookIdOfMetaKey("dinoreader/stories/short/meta.json"), null);
});

// ---- the endpoints ----------------------------------------------------------------------------------------------------------

function env(extra = {}) {
  const prod = [];
  const beta = [];
  return { prod, beta, DINO_USAGE: { writeDataPoint: (p) => prod.push(p) }, DINO_USAGE_BETA: { writeDataPoint: (p) => beta.push(p) }, ...extra };
}
const post = (path, body, headers = {}) => new Request(`https://dinoreader.sirous.uk${path}`, { method: "POST", headers, body: JSON.stringify(body) });

test("POST /api/store/apple writes one production row; a sandbox one goes to the beta dataset only", async () => {
  const e = env();
  const deps = { apple: APPLE, books, now: NOW };
  const taken = await handleStoreNotification(post("/api/store/apple", appleBody("SUBSCRIBED", "INITIAL_BUY", transaction(TRIAL))), e, deps);
  assert.equal(taken.status, 204);
  assert.equal(e.prod.length, 1);
  assert.equal(e.prod[0].blobs[8], "ios.trial_started.trial");
  assert.equal(e.prod[0].blobs[9], "GB");
  const sandbox = await handleStoreNotification(post("/api/store/apple", appleBody("SUBSCRIBED", "INITIAL_BUY", transaction(TRIAL), { environment: "Sandbox" })), e, deps);
  assert.equal(sandbox.status, 204);
  assert.equal(e.prod.length, 1);
  assert.equal(e.beta.length, 1);
});

test("a repeated Apple delivery writes one row", async () => {
  const kept = new Map();
  const e = env({ STORE_DEDUP: { get: async (k) => kept.get(k) ?? null, put: async (k, v) => void kept.set(k, v) } });
  const body = appleBody("DID_RENEW", undefined, transaction());
  for (let i = 0; i < 2; i += 1) assert.equal((await handleStoreNotification(post("/api/store/apple", body), e, { apple: APPLE, books, now: NOW })).status, 204);
  assert.equal(e.prod.length, 1);
});

test("the endpoints answer 405 to a GET, 400 to a body that is not a notification, and 204 (logged, not retried) without the dataset", async () => {
  const e = env();
  assert.equal((await handleStoreNotification(new Request("https://dinoreader.sirous.uk/api/store/apple"), e)).status, 405);
  assert.equal((await handleStoreNotification(post("/api/store/apple", { nope: 1 }), e)).status, 400);
  assert.equal((await handleStoreNotification(new Request("https://dinoreader.sirous.uk/api/store/apple", { method: "POST", body: "not json" }), e)).status, 400);
  const none = await handleStoreNotification(post("/api/store/apple", appleBody("DID_RENEW", undefined, transaction())), {}, { apple: APPLE, books, now: NOW });
  assert.equal(none.status, 204);
});

// ---- Google ------------------------------------------------------------------------------------------------------------------

const RSA = generateKeyPairSync("rsa", { modulusLength: 2048 });
const JWK = { ...RSA.publicKey.export({ format: "jwk" }), kid: "test-key", alg: "RS256", use: "sig" };
const AUDIENCE = "https://dinoreader.sirous.uk/api/store/google";
const PUSH_EMAIL = "rtdn-push@dino.iam.gserviceaccount.com";
const nowS = Math.floor(NOW.getTime() / 1000);
const PLAY_KEY = generateKeyPairSync("rsa", { modulusLength: 2048 }).privateKey.export({ type: "pkcs8", format: "pem" });
const GOOGLE_ENV = { STORE_GOOGLE_AUDIENCE: AUDIENCE, STORE_GOOGLE_PUSH_EMAIL: PUSH_EMAIL, PLAY_SERVICE_ACCOUNT_JSON: JSON.stringify({ client_email: "play@dino.iam.gserviceaccount.com", private_key: PLAY_KEY }) };

function oidc(claims = {}, key = RSA.privateKey) {
  const input = `${encode({ alg: "RS256", kid: "test-key", typ: "JWT" })}.${encode({ iss: "https://accounts.google.com", aud: AUDIENCE, email: PUSH_EMAIL, email_verified: true, exp: nowS + 600, ...claims })}`;
  return `Bearer ${input}.${base64UrlEncode(sign("RSA-SHA256", Buffer.from(input), key))}`;
}

const json = (body, status = 200) => new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

/** A recorded Google: the JWKS, the token endpoint and Play's purchases API. */
function recorded({ subscription, product, status = 200 } = {}) {
  const calls = [];
  const fetcher = async (url) => {
    calls.push(url);
    if (url.includes("/oauth2/v3/certs")) return json({ keys: [JWK] });
    if (url.includes("oauth2.googleapis.com/token")) return json({ access_token: "play-access", expires_in: 3600 });
    if (url.includes("/subscriptionsv2/tokens/")) return json(subscription ?? {}, status);
    if (url.includes("/purchases/products/")) return json(product ?? {}, status);
    throw new Error(`unrecorded request ${url}`);
  };
  return { fetcher, calls };
}

const pubsub = (notification, messageId = "m-1") => ({ message: { data: Buffer.from(JSON.stringify({ version: "1.0", packageName: APP_PACKAGE_ID, eventTimeMillis: "1", ...notification })).toString("base64"), messageId }, subscription: "projects/p/subscriptions/s" });
const subNote = (type, subscriptionId = SUBSCRIPTION_MONTHLY_PRODUCT) => pubsub({ subscriptionNotification: { version: "1.0", notificationType: type, purchaseToken: "TOKEN-SECRET", subscriptionId } });
const playSub = (extra = {}, item = {}) => ({ regionCode: "NO", subscriptionState: "SUBSCRIPTION_STATE_ACTIVE", latestOrderId: "GPA.1111-2222-3333-44444", lineItems: [{ productId: SUBSCRIPTION_MONTHLY_PRODUCT, offerDetails: { basePlanId: "monthly" }, ...item }], ...extra });

async function push(note, play, headers = { Authorization: oidc() }, e = env(GOOGLE_ENV)) {
  const { fetcher } = recorded(play);
  resetGoogleStoreCaches();
  const response = await handleStoreNotification(post("/api/store/google", note, headers), e, { fetcher, books, now: NOW });
  return { response, e };
}

test("Google: the push token must be Google's, for our audience and the push account, and unexpired", async () => {
  const { fetcher } = recorded();
  const check = async (header, e = GOOGLE_ENV) => { resetGoogleStoreCaches(); return verifyPushToken(header, e, fetcher, nowS); };
  assert.equal(await check(oidc()), true);
  assert.equal(await check(null), false);
  assert.equal(await check("Bearer a.b.c"), false);
  assert.equal(await check(oidc({ aud: "https://elsewhere" })), false);
  assert.equal(await check(oidc({ email: "someone@else.com" })), false);
  assert.equal(await check(oidc({ email_verified: false })), false);
  assert.equal(await check(oidc({ iss: "https://evil.example" })), false);
  assert.equal(await check(oidc({ exp: nowS - 3600 })), false);
  assert.equal(await check(oidc({}, generateKeyPairSync("rsa", { modulusLength: 2048 }).privateKey)), false, "signed with another key");
  assert.equal(await check(oidc(), {}), false, "no audience or email configured: fails closed");
});

test("Google: a push without a valid token is 401 and writes nothing", async () => {
  const none = await push(subNote(4), { subscription: playSub() }, {});
  assert.equal(none.response.status, 401);
  assert.equal(none.e.prod.length, 0);
});

test("Google: the purchase read decides trial or paid, plan, region and test", async () => {
  const trial = await push(subNote(4), { subscription: playSub({}, { offerPhase: { freeTrial: {} } }) });
  assert.equal(trial.response.status, 204);
  assert.equal(trial.e.prod[0].blobs[8], "android.trial_started.trial");
  assert.equal(trial.e.prod[0].blobs[3], "sub_monthly");
  assert.equal(trial.e.prod[0].blobs[9], "NO");
  const paid = await push(subNote(4), { subscription: playSub({}, { offerPhase: { basePrice: {} }, offerDetails: { basePlanId: "yearly" } }) });
  assert.equal(paid.e.prod[0].blobs[8], "android.subscribed.paid");
  assert.equal(paid.e.prod[0].blobs[3], "sub_yearly");
  const test = await push(subNote(4), { subscription: playSub({ testPurchase: {} }, { offerPhase: { freeTrial: {} } }) });
  assert.equal(test.e.prod.length, 0);
  assert.equal(test.e.beta.length, 1);
});

test("Google: renewal kinds, conversion after a trial, and the other notification types", async () => {
  const detail = async (type, play) => { const { e } = await push(subNote(type), { subscription: play }); return e.prod[0]?.blobs[8] ?? null; };
  assert.equal(await detail(2, playSub({ latestOrderId: "GPA.1111-2222-3333-44444..3" })), "android.renewed.paid");
  assert.equal(await detail(2, playSub({ latestOrderId: "GPA.1111-2222-3333-44444..0" }, { offerDetails: { basePlanId: "monthly", offerId: "freetrial" } })), "android.converted.paid");
  assert.equal(await detail(2, playSub({ latestOrderId: "GPA.1111-2222-3333-44444..0" })), "android.renewed.paid", "no offer, so no trial to convert");
  assert.equal(await detail(3, playSub()), "android.cancelled.paid");
  assert.equal(await detail(5, playSub()), "android.billing_retry.paid");
  assert.equal(await detail(6, playSub()), "android.grace.paid");
  assert.equal(await detail(12, playSub()), "android.refunded.paid");
  assert.equal(await detail(13, playSub()), "android.expired.paid");
  assert.equal(await detail(9, playSub()), null, "deferred is not counted");
});

test("Google: a book purchase and its cancellation name the book; an unknown sku, a test ping and a foreign package write nothing", async () => {
  const one = (type, sku = BOOK_PRODUCT) => pubsub({ oneTimeProductNotification: { version: "1.0", notificationType: type, purchaseToken: "T", sku } });
  const bought = await push(one(1), { product: { regionCode: "US", purchaseType: 1 } });
  assert.deepEqual([bought.e.prod[0].blobs[8], bought.e.prod[0].blobs[1], bought.e.prod[0].blobs[3]], ["android.book_bought.paid", BOOK_ID, "shelf"]);
  assert.equal((await push(one(2), { product: { regionCode: "US" } })).e.prod[0].blobs[8], "android.book_refunded.paid");
  assert.equal((await push(one(1), { product: { regionCode: "US", purchaseType: 0 } })).e.beta.length, 1, "a license tester's purchase is a test");
  assert.equal((await push(one(1, `${APP_PACKAGE_ID}.book.nope`), { product: {} })).e.prod.length, 0);
  const ping = await push(pubsub({ testNotification: { version: "1.0" } }), {});
  assert.equal(ping.response.status, 204);
  assert.equal(ping.e.prod.length, 0);
  const foreign = await push({ message: { data: Buffer.from(JSON.stringify({ packageName: "other.app", testNotification: {} })).toString("base64"), messageId: "x" } }, {});
  assert.equal(foreign.response.status, 400);
});

test("Google: a voided subscription is a refund, Play being down is a 503 so Pub/Sub redelivers, and a gone purchase is dropped", async () => {
  const voided = pubsub({ voidedPurchaseNotification: { purchaseToken: "T", orderId: "GPA.1", productType: 1, refundType: 1 } });
  assert.equal((await push(voided, { subscription: playSub() })).e.prod[0].blobs[8], "android.refunded.paid");
  assert.equal((await push(pubsub({ voidedPurchaseNotification: { purchaseToken: "T", orderId: "GPA.2", productType: 2 } }), {})).e.prod.length, 0);
  const down = await push(subNote(4), { subscription: {}, status: 500 });
  assert.equal(down.response.status, 503);
  const gone = await push(subNote(4), { subscription: {}, status: 410 });
  assert.equal(gone.response.status, 204);
  assert.equal(gone.e.prod.length, 0);
});

test("no token, transaction id or order id ever reaches a row", async () => {
  const { e } = await push(subNote(4), { subscription: playSub({}, { offerPhase: { freeTrial: {} } }) });
  assert.doesNotMatch(JSON.stringify(e.prod), /TOKEN-SECRET|GPA\./);
  const apple = env();
  await handleStoreNotification(post("/api/store/apple", appleBody("DID_RENEW", undefined, transaction())), apple, { apple: APPLE, books, now: NOW });
  assert.doesNotMatch(JSON.stringify(apple.prod), /2000000123|2000000100|11111111-2222/);
});

test("the Dino-only run_worker_first entries exist in wrangler.jsonc", async () => {
  const { readFileSync } = await import("node:fs");
  const text = readFileSync(new URL("../wrangler.jsonc", import.meta.url), "utf8");
  assert.match(text, /"\/api\/store\/\*"/);
  assert.match(text, /"\/dino\/api\/store\/\*"/);
});

// ---- Pass 1 review fixes ----------------------------------------------------------------------------------------------------

test("a row that could not be written (dataset unbound) is a 204 and is not remembered, so a later delivery with the dataset bound still counts", async () => {
  const kept = new Map();
  const STORE_DEDUP = { get: async (k) => kept.get(k) ?? null, put: async (k, v) => void kept.set(k, v) };
  const body = appleBody("DID_RENEW", undefined, transaction());
  const deps = { apple: APPLE, books, now: NOW };
  assert.equal((await handleStoreNotification(post("/api/store/apple", body), { STORE_DEDUP }, deps)).status, 204);
  const sandboxOnly = appleBody("DID_RENEW", undefined, transaction(), { environment: "Sandbox" });
  assert.equal((await handleStoreNotification(post("/api/store/apple", sandboxOnly), { STORE_DEDUP, DINO_USAGE: { writeDataPoint: () => {} } }, deps)).status, 204); // no beta binding
  assert.equal(kept.size, 0);
  const e = env({ STORE_DEDUP });
  assert.equal((await handleStoreNotification(post("/api/store/apple", body), e, deps)).status, 204);
  assert.equal(e.prod.length, 1);
});

test("Apple: a payload signed more than seven days ago, or in the future, is dropped with 204 and counts nothing", async () => {
  const body = appleBody("DID_RENEW", undefined, transaction());
  for (const now of [new Date(SIGNED_AT + 7 * 24 * 3_600_000 + 60_000), new Date(SIGNED_AT - 3_600_000)]) {
    const e = env();
    assert.equal((await handleStoreNotification(post("/api/store/apple", body), e, { apple: APPLE, books, now })).status, 204);
    assert.equal(e.prod.length, 0);
  }
});

const metaKey = (id) => `dinoreader/stories/${id}/meta.json`;
const NEW_ID = "fedcba9876543210fedcba9876543210";
function fakeBucket(files, { failList = false } = {}) {
  return {
    list: async () => { if (failList) throw new Error("r2 down"); return { objects: Object.keys(files).map((key) => ({ key })), truncated: false }; },
    get: async (key) => ({ json: async () => { const v = files[key]; if (v === "not json") throw new SyntaxError("bad"); return v; } }),
  };
}

test("books: a missing product refetches the catalogue once, and one bad meta.json skips only that book", async () => {
  const files = { [metaKey(BOOK_ID)]: { productId: BOOK_PRODUCT }, [metaKey("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]: "not json" };
  const resolve = bookResolverFor(fakeBucket(files));
  assert.equal(await resolve(BOOK_PRODUCT), BOOK_ID, "a bad neighbour does not blank the catalogue");
  files[metaKey(NEW_ID)] = { productId: `${APP_PACKAGE_ID}.book.newest` };
  assert.equal(await resolve(`${APP_PACKAGE_ID}.book.newest`), NEW_ID, "a book published after the load is found by the refetch");
  assert.equal(await resolve(`${APP_PACKAGE_ID}.book.nope`), null);
});

test("books: a bucket that cannot be read is a 503 so the store redelivers, not a dropped purchase", async () => {
  const resolve = bookResolverFor(fakeBucket({}, { failList: true }));
  const e = env();
  const body = appleBody("ONE_TIME_CHARGE", undefined, transaction({ productId: BOOK_PRODUCT }));
  assert.equal((await handleStoreNotification(post("/api/store/apple", body), e, { apple: APPLE, books: resolve, now: NOW })).status, 503);
  assert.equal(e.prod.length, 0);
});

test("Google: a token signed by a key published after the cache was filled is accepted after one refetch, at most once a minute", async () => {
  const rotated = generateKeyPairSync("rsa", { modulusLength: 2048 });
  const NEW_JWK = { ...rotated.publicKey.export({ format: "jwk" }), kid: "rotated-key", alg: "RS256", use: "sig" };
  let published = [JWK];
  let fetches = 0;
  const fetcher = async () => { fetches += 1; return json({ keys: published }); };
  const withKid = (kid, key) => {
    const input = `${encode({ alg: "RS256", kid, typ: "JWT" })}.${encode({ iss: "https://accounts.google.com", aud: AUDIENCE, email: PUSH_EMAIL, email_verified: true, exp: nowS + 600 })}`;
    return `Bearer ${input}.${base64UrlEncode(sign("RSA-SHA256", Buffer.from(input), key))}`;
  };
  resetGoogleStoreCaches();
  const realNow = Date.now;
  let clock = realNow();
  Date.now = () => clock;
  try {
    assert.equal(await verifyPushToken(oidc(), GOOGLE_ENV, fetcher, nowS), true);
    published = [JWK, NEW_JWK];
    clock += 30 * 60_000;
    assert.equal(await verifyPushToken(withKid("rotated-key", rotated.privateKey), GOOGLE_ENV, fetcher, nowS), true);
    const before = fetches;
    assert.equal(await verifyPushToken(withKid("bogus", rotated.privateKey), GOOGLE_ENV, fetcher, nowS), false);
    assert.equal(fetches, before, "inside the cooldown a bogus kid costs no fetch");
    clock += 61_000;
    assert.equal(await verifyPushToken(withKid("bogus", rotated.privateKey), GOOGLE_ENV, fetcher, nowS), false);
    assert.equal(await verifyPushToken(withKid("bogus", rotated.privateKey), GOOGLE_ENV, fetcher, nowS), false);
    assert.equal(fetches - before, 1, "after the cooldown a bogus kid costs one refetch, not one per request");
  } finally {
    Date.now = realNow;
  }
});

test("Google: a 401 or 403 from Play is a configuration fault answered 204; 5xx and 429 stay retryable", async () => {
  for (const status of [401, 403]) assert.equal((await push(subNote(4), { subscription: {}, status })).response.status, 204);
  for (const status of [429, 500, 503]) assert.equal((await push(subNote(4), { subscription: {}, status })).response.status, 503);
});

test("Google: a trial push retried after the trial ended is still a trial start; an intro-price renewal is not a conversion; recovered and restarted count nothing", async () => {
  const detail = async (type, play) => (await push(subNote(type), { subscription: play })).e.prod[0]?.blobs[8] ?? null;
  const afterTrial = playSub({ latestOrderId: "GPA.1111-2222-3333-44444..0" }, { offerDetails: { basePlanId: "monthly", offerId: "freetrial" }, offerPhase: { basePrice: {} } });
  assert.equal(await detail(4, afterTrial), "android.trial_started.trial");
  assert.equal(await detail(2, afterTrial), "android.converted.paid");
  const intro = playSub({ latestOrderId: "GPA.1111-2222-3333-44444..0" }, { offerDetails: { basePlanId: "monthly", offerId: "intro" }, offerPhase: { introductoryPrice: {} } });
  assert.equal(await detail(2, intro), "android.renewed.paid");
  assert.equal(await detail(4, intro), "android.subscribed.paid");
  assert.equal(await detail(1, playSub()), null);
  assert.equal(await detail(7, playSub()), null);
});

test("the Dino run_worker_first entries are also in wrangler.workers-dev.jsonc", async () => {
  const { readFileSync } = await import("node:fs");
  const text = readFileSync(new URL("../wrangler.workers-dev.jsonc", import.meta.url), "utf8");
  assert.match(text, /"\/api\/store\/\*"/);
  assert.match(text, /"\/dino\/api\/store\/\*"/);
});

// ---- Pass 2 review fixes ----------------------------------------------------------------------------------------------------

test("Apple: only an exact Production on both the notification and the transaction is production; Xcode, LocalTesting or a missing field are tests", async () => {
  for (const environment of ["Xcode", "LocalTesting", "", "production"]) {
    assert.equal((await countOf(appleBody("DID_RENEW", undefined, transaction(), { environment }))).count.test, true, `outer ${environment}`);
    assert.equal((await countOf(appleBody("DID_RENEW", undefined, transaction({ environment })))).count.test, true, `transaction ${environment}`);
  }
  const noEnvironment = transaction();
  delete noEnvironment.environment;
  assert.equal((await countOf(appleBody("DID_RENEW", undefined, noEnvironment))).count.test, true);
});

test("a dedup store that throws never turns a written row into an error", async () => {
  const warn = console.warn;
  console.warn = () => {};
  try {
    const body = appleBody("DID_RENEW", undefined, transaction());
    const deps = { apple: APPLE, books, now: NOW };
    const failingPut = env({ STORE_DEDUP: { get: async () => null, put: async () => { throw new Error("kv down"); } } });
    assert.equal((await handleStoreNotification(post("/api/store/apple", body), failingPut, deps)).status, 204);
    assert.equal(failingPut.prod.length, 1);
    const failingGet = env({ STORE_DEDUP: { get: async () => { throw new Error("kv down"); }, put: async () => {} } });
    assert.equal((await handleStoreNotification(post("/api/store/apple", body), failingGet, deps)).status, 204);
    assert.equal(failingGet.prod.length, 1);
  } finally {
    console.warn = warn;
  }
});

test("Apple: a plan change from monthly to yearly, or a renewal under an offer, is not a trial conversion", async () => {
  const upgrade = transaction({ productId: SUBSCRIPTION_YEARLY_PRODUCT, originalPurchaseDate: SIGNED_AT - 60 * DAY, expiresDate: SIGNED_AT + 365 * DAY });
  assert.equal(isTrialConversion(upgrade), false);
  assert.equal(detailOf(await countOf(appleBody("DID_RENEW", undefined, upgrade))), "ios.renewed.paid");
  assert.equal(isTrialConversion(transaction({ originalPurchaseDate: SIGNED_AT - 7 * DAY, offerType: 2 })), false);
  const yearlyAfterTrial = transaction({ productId: SUBSCRIPTION_YEARLY_PRODUCT, originalPurchaseDate: SIGNED_AT - 7 * DAY, expiresDate: SIGNED_AT + 365 * DAY });
  assert.equal(isTrialConversion(yearlyAfterTrial), true);
});
