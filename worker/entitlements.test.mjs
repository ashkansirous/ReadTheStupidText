// Run: node --test worker/entitlements.test.mjs (Slice 346: paid books. Proofs, the three store checkers against recorded answers, the gate, and the story routes behind it.)
import test from "node:test";
import assert from "node:assert/strict";
import { generateKeyPairSync } from "node:crypto";

import { appleChecker, resetAppleTokenCache } from "./entitlements/apple.ts";
import { GateAnswer, MAX_CACHED, NOT_OWNED_CACHE_MS, createPaidGate, resetOwnedCache } from "./entitlements/gate.ts";
import { googleChecker, resetGoogleTokenCache } from "./entitlements/google.ts";
import { microsoftChecker, ownedFromItems, resetMicrosoftTokenCache, serviceTicket } from "./entitlements/microsoft.ts";
import { base64UrlEncode } from "./entitlements/jwt.ts";
import { parseProof } from "./entitlements/proof.ts";
import { CANNOT_VERIFY, Verdict, checked } from "./entitlements/types.ts";
import { APP_PACKAGE_ID, productsUnlocking, unlocksBook } from "./products.ts";
import { serveObject } from "./downloads.ts";
import { handleStories, parseMeta, toListItem } from "./stories.ts";

const BOOK = "7d0e5a4e000040008000000000000001";
const FREE_BOOK = "7d0e5a4e000040008000000000000002";
const PRODUCT = `${APP_PACKAGE_ID}.book.goldilocks`;
const OTHER = `${APP_PACKAGE_ID}.book.red-riding-hood`;
const BUNDLE = `${APP_PACKAGE_ID}.bundle.classics`;

const rsa = generateKeyPairSync("rsa", { modulusLength: 2048 });
const ec = generateKeyPairSync("ec", { namedCurve: "P-256" });
const RSA_PEM = rsa.privateKey.export({ type: "pkcs8", format: "pem" });
const EC_PEM = ec.privateKey.export({ type: "pkcs8", format: "pem" });
const ENV = {
  PLAY_SERVICE_ACCOUNT_JSON: JSON.stringify({ client_email: "svc@x.iam", private_key: RSA_PEM }),
  APPLE_IAP_KEY_P8: EC_PEM,
  APPLE_IAP_KEY_ID: "KEYID",
  APPLE_IAP_ISSUER_ID: "issuer",
  MS_STORE_TENANT_ID: "tenant",
  MS_STORE_CLIENT_ID: "client",
  MS_STORE_CLIENT_SECRET: "secret",
};

const json = (body, status = 200) => new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
const fresh = () => {
  resetGoogleTokenCache();
  resetAppleTokenCache();
  resetMicrosoftTokenCache();
  resetOwnedCache();
};

// A recorded store: each rule is [url fragment, response].
function recorded(rules) {
  const calls = [];
  const fetcher = async (url, init) => {
    calls.push({ url, init });
    const rule = rules.find(([fragment]) => url.includes(fragment));
    if (!rule) throw new Error(`unrecorded request ${url}`);
    const answer = typeof rule[1] === "function" ? rule[1](url, init) : rule[1];
    return answer instanceof Response ? answer.clone() : answer;
  };
  return { fetcher, calls };
}

const signedTransaction = (payload) => `${base64UrlEncode(Buffer.from("{}"))}.${base64UrlEncode(Buffer.from(JSON.stringify(payload)))}.sig`;

test("a proof is store, product and token; anything else is refused", () => {
  assert.deepEqual(parseProof(`android:${PRODUCT}:abc.def:ghi`), { store: "android", productId: PRODUCT, token: "abc.def:ghi" });
  assert.deepEqual(parseProof("windows::KEYKEY"), { store: "windows", productId: "", token: "KEYKEY" });
  for (const bad of ["", "android", "android:x", `android::tok`, `amazon:${PRODUCT}:tok`, `ios:${PRODUCT}:`, `ios:bad id!:tok`, `android:${PRODUCT}:${"a".repeat(9000)}`]) {
    assert.equal(parseProof(bad), null, bad);
  }
});

test("a product unlocks a list of books: a per-book product is one, a bundle row is more", () => {
  assert.deepEqual(productsUnlocking(BOOK, PRODUCT, {}), [PRODUCT]);
  assert.deepEqual(productsUnlocking(BOOK, PRODUCT, { [BUNDLE]: [BOOK, FREE_BOOK] }), [PRODUCT, BUNDLE]);
  assert.deepEqual(productsUnlocking(BOOK, null, {}), []);
  assert.equal(unlocksBook([BUNDLE], BOOK, PRODUCT, { [BUNDLE]: [BOOK, FREE_BOOK] }), true);
  assert.equal(unlocksBook([BUNDLE], "ffffffffffffffffffffffffffffffff", PRODUCT, { [BUNDLE]: [BOOK] }), false);
  assert.equal(unlocksBook([OTHER], BOOK, PRODUCT, {}), false);
});

test("Google: a purchased token is owned, a cancelled or unknown one is not, an error is cannot-verify", async () => {
  fresh();
  const proof = parseProof(`android:${PRODUCT}:tok123`);
  const token = ["oauth2.googleapis.com", json({ access_token: "at" })];
  const owned = recorded([token, ["/tokens/tok123", json({ purchaseState: 0 })]]);
  assert.deepEqual(await googleChecker.check(proof, ENV, owned.fetcher), checked([PRODUCT]));
  const get = owned.calls.find((call) => call.url.includes("/tokens/"));
  assert.ok(get.url.includes(`/applications/${APP_PACKAGE_ID}/purchases/products/${encodeURIComponent(PRODUCT)}/`));
  assert.equal(get.init.headers.Authorization, "Bearer at");
  const cancelled = recorded([token, ["/tokens/", json({ purchaseState: 1 })]]);
  assert.deepEqual(await googleChecker.check(proof, ENV, cancelled.fetcher), checked([]));
  const unknown = recorded([token, ["/tokens/", json({ error: {} }, 404)]]);
  assert.deepEqual(await googleChecker.check(proof, ENV, unknown.fetcher), checked([]));
  const broken = recorded([token, ["/tokens/", json({}, 503)]]);
  assert.deepEqual(await googleChecker.check(proof, ENV, broken.fetcher), CANNOT_VERIFY);
  const noToken = recorded([["oauth2.googleapis.com", json({}, 401)]]);
  fresh();
  assert.deepEqual(await googleChecker.check(proof, ENV, noToken.fetcher), CANNOT_VERIFY);
});

test("Apple: a transaction for this app and product is owned; revoked, other product or other app is not; sandbox is tried on 404", async () => {
  fresh();
  const proof = parseProof(`ios:${PRODUCT}:2000000123`);
  const good = signedTransaction({ bundleId: APP_PACKAGE_ID, productId: PRODUCT, environment: "Production" });
  const production = recorded([["api.storekit.apple.com", json({ signedTransactionInfo: good })]]);
  assert.deepEqual(await appleChecker.check(proof, ENV, production.fetcher), checked([PRODUCT]));
  assert.ok(production.calls[0].url.endsWith("/inApps/v1/transactions/2000000123"));
  assert.match(production.calls[0].init.headers.Authorization, /^Bearer [\w-]+\.[\w-]+\.[\w-]+$/);
  for (const payload of [{ bundleId: APP_PACKAGE_ID, productId: PRODUCT, environment: "Production", revocationDate: 1 }, { bundleId: APP_PACKAGE_ID, productId: OTHER, environment: "Production" }, { bundleId: "other.app", productId: PRODUCT, environment: "Production" }, { bundleId: APP_PACKAGE_ID, productId: PRODUCT }]) {
    const bad = recorded([["api.storekit", json({ signedTransactionInfo: signedTransaction(payload) })]]);
    assert.deepEqual(await appleChecker.check(proof, ENV, bad.fetcher), checked([]), JSON.stringify(payload));
  }
  const down = recorded([["api.storekit", json({}, 500)]]);
  assert.deepEqual(await appleChecker.check(proof, ENV, down.fetcher), CANNOT_VERIFY);
});

test("Apple: in production a sandbox transaction never counts and the sandbox host is not asked; a staging Worker that allows it does", async () => {
  fresh();
  const proof = parseProof(`ios:${PRODUCT}:2000000123`);
  const sandboxTransaction = signedTransaction({ bundleId: APP_PACKAGE_ID, productId: PRODUCT, environment: "Sandbox" });
  const production = recorded([["api.storekit.apple.com", json({}, 404)], ["api.storekit-sandbox.apple.com", json({ signedTransactionInfo: sandboxTransaction })]]);
  assert.deepEqual(await appleChecker.check(proof, ENV, production.fetcher), checked([]));
  assert.equal(production.calls.some((call) => call.url.includes("sandbox")), false);
  const relabelled = recorded([["api.storekit.apple.com", json({ signedTransactionInfo: sandboxTransaction })]]);
  assert.deepEqual(await appleChecker.check(proof, ENV, relabelled.fetcher), checked([]));
  const staging = { ...ENV, APPLE_IAP_ALLOW_SANDBOX: "true" };
  const allowed = recorded([["api.storekit.apple.com", json({}, 404)], ["api.storekit-sandbox.apple.com", json({ signedTransactionInfo: sandboxTransaction })]]);
  assert.deepEqual(await appleChecker.check(proof, staging, allowed.fetcher), checked([PRODUCT]));
});

test("Microsoft: the customer's active durable add-ons are the owned list", async () => {
  fresh();
  const proof = parseProof("windows::STOREIDKEY");
  const items = [
    { productType: "Durable", status: "Active", inAppOfferToken: PRODUCT },
    { productType: "Durable", status: "Revoked", inAppOfferToken: OTHER },
    { productType: "Application", status: "Active", inAppOfferToken: "app" },
    { productType: "Durable", status: "Active" },
  ];
  assert.deepEqual(ownedFromItems(items), [PRODUCT]);
  const store = recorded([["login.microsoftonline.com", json({ access_token: "ms" })], ["collections.mp.microsoft.com", json({ items })]]);
  assert.deepEqual(await microsoftChecker.check(proof, ENV, store.fetcher), checked([PRODUCT]));
  const query = JSON.parse(store.calls[1].init.body);
  assert.equal(query.beneficiaries[0].identityValue, "STOREIDKEY");
  assert.equal(query.beneficiaries[0].identityType, "b2b");
  const refused = recorded([["login.microsoftonline.com", json({ access_token: "ms" })], ["collections", json({}, 500)]]);
  fresh();
  assert.deepEqual(await microsoftChecker.check(proof, ENV, refused.fetcher), CANNOT_VERIFY);
});

test("Microsoft: the ticket handed to the app and the token that queries are two audiences, cached apart (check7 #1)", async () => {
  fresh();
  const proof = parseProof("windows::STOREIDKEY");
  const store = recorded([["login.microsoftonline.com", (_url, init) => json({ access_token: `tok-${new URLSearchParams(init.body).get("resource")}` })], ["collections.mp.microsoft.com", json({ items: [] })]]);
  const ticket = await serviceTicket(ENV, store.fetcher);
  assert.equal(ticket, "tok-https://onestore.microsoft.com/b2b/keys/create/collections");
  await microsoftChecker.check(proof, ENV, store.fetcher);
  const query = store.calls.find((call) => call.url.includes("collections.mp"));
  assert.equal(query.init.headers.Authorization, "Bearer tok-https://onestore.microsoft.com");
  await serviceTicket(ENV, store.fetcher);
  assert.equal(store.calls.filter((call) => call.url.includes("login.microsoftonline")).length, 2); // one token per audience, each reused
});

test("Microsoft: a rotated client secret under the same client id does not reuse the cached token (check8 #8)", async () => {
  fresh();
  const store = recorded([["login.microsoftonline.com", (_url, init) => json({ access_token: `tok-${new URLSearchParams(init.body).get("client_secret")}` })]]);
  assert.equal(await serviceTicket(ENV, store.fetcher), "tok-secret");
  assert.equal(await serviceTicket({ ...ENV, MS_STORE_CLIENT_SECRET: "rotated" }, store.fetcher), "tok-rotated");
  assert.equal(await serviceTicket({ ...ENV, MS_STORE_CLIENT_SECRET: "rotated" }, store.fetcher), "tok-rotated");
  assert.equal(store.calls.length, 2);
});

test("missing store secrets are cannot-verify for every store, never owned, and ask nobody", async () => {
  fresh();
  const never = recorded([]);
  for (const text of [`android:${PRODUCT}:t`, `ios:${PRODUCT}:t`, "windows::t"]) {
    const proof = parseProof(text);
    const checker = { android: googleChecker, ios: appleChecker, windows: microsoftChecker }[proof.store];
    assert.equal((await checker.check(proof, {}, never.fetcher)).verdict, Verdict.CannotVerify, text);
  }
  assert.equal(never.calls.length, 0);
});

function gateWith({ enabled = true, owned, env = ENV, verdict } = {}) {
  const calls = { checks: 0, enabled: 0 };
  const checker = { async check(proof) { calls.checks += 1; return verdict ?? checked(owned ?? [proof.productId]); } };
  const checkers = { android: checker, ios: checker, windows: checker };
  const gate = createPaidGate({ enabled: async () => { calls.enabled += 1; return enabled; }, env, fetcher: async () => json({}), checkers });
  return { gate, calls };
}

test("gate: switch off means no proof is needed and no store is asked", async () => {
  fresh();
  const { gate, calls } = gateWith({ enabled: false });
  assert.equal(await gate(null, { id: BOOK, productId: PRODUCT }), GateAnswer.Off);
  assert.equal(calls.checks, 0);
});

test("gate: a switch that cannot be read counts as off", async () => {
  fresh();
  const gate = createPaidGate({ enabled: async () => { throw new Error("r2"); }, env: ENV, fetcher: async () => json({}) });
  assert.equal(await gate(null, { id: BOOK, productId: PRODUCT }), GateAnswer.Off);
});

test("gate: no proof, a malformed proof, another book's product and a store that cannot answer", async () => {
  fresh();
  const book = { id: BOOK, productId: PRODUCT };
  assert.equal(await gateWith().gate(null, book), GateAnswer.NoProof);
  assert.equal(await gateWith().gate("  ", book), GateAnswer.NoProof);
  assert.equal(await gateWith().gate("garbage", book), GateAnswer.BadProof);
  assert.equal(await gateWith({ owned: [OTHER] }).gate(`android:${OTHER}:t`, book), GateAnswer.BadProof);
  assert.equal(await gateWith({ verdict: CANNOT_VERIFY }).gate(`android:${PRODUCT}:t`, book), GateAnswer.CannotVerify);
  assert.equal(await gateWith({ owned: [PRODUCT] }).gate(`android:${PRODUCT}:t`, book), GateAnswer.Allowed);
});

// Captures console.warn lines while `run` runs.
async function warnings(run) {
  const lines = [];
  const original = console.warn;
  console.warn = (...args) => lines.push(args.join(" "));
  try {
    await run();
  } finally {
    console.warn = original;
  }
  return lines;
}

const SECRET_MARKERS = ["STOREIDKEY", "secret", "Bearer", "ms-token", "customer-123", "1.2.3.4", "AADSTS7000215"];
const assertNothingSecret = (lines) => {
  const text = lines.join("\n");
  for (const marker of SECRET_MARKERS) assert.ok(!text.includes(marker), `log leaked ${marker}: ${text}`);
};

test("Microsoft logs: a 400 and a 500 log their codes, a 200 logs the active items, and the answers are unchanged", async () => {
  fresh();
  const proof = parseProof("windows::STOREIDKEY");
  const refusal = (status) => json({ code: "BadRequest", message: "customer-123 is bad", innererror: { code: "InvalidBeneficiary", message: "customer-123" } }, status);
  const bad = recorded([["login.microsoftonline.com", json({ access_token: "ms-token" })], ["collections", refusal(400)]]);
  let answer;
  let lines = await warnings(async () => { answer = await microsoftChecker.check(proof, ENV, bad.fetcher); });
  assert.deepEqual(answer, checked([]));
  assert.ok(lines.some((line) => line.includes("status=400") && line.includes("code=BadRequest") && line.includes("innererror=InvalidBeneficiary")));
  assertNothingSecret(lines);
  fresh();
  const down = recorded([["login.microsoftonline.com", json({ access_token: "ms-token" })], ["collections", refusal(503)]]);
  lines = await warnings(async () => { answer = await microsoftChecker.check(proof, ENV, down.fetcher); });
  assert.deepEqual(answer, CANNOT_VERIFY);
  assert.ok(lines.some((line) => line.includes("status=503")));
  fresh();
  const items = [{ productType: "Durable", status: "Active", inAppOfferToken: PRODUCT, customerId: "customer-123" }, { productType: "Durable", status: "Revoked", inAppOfferToken: OTHER }];
  const good = recorded([["login.microsoftonline.com", json({ access_token: "ms-token" })], ["collections", json({ items })]]);
  lines = await warnings(async () => { answer = await microsoftChecker.check(proof, ENV, good.fetcher); });
  assert.deepEqual(answer, checked([PRODUCT]));
  assert.ok(lines.some((line) => line.includes("items=2") && line.includes(`Durable/Active/${PRODUCT}`) && !line.includes(OTHER)));
  assertNothingSecret(lines);
});

test("Microsoft logs: a failed token mint logs status and error only, a throw logs its name only", async () => {
  fresh();
  const proof = parseProof("windows::STOREIDKEY");
  const denied = recorded([["login.microsoftonline.com", json({ error: "invalid_client", error_description: "AADSTS7000215 secret client tenant 1.2.3.4" }, 401)]]);
  let answer;
  let lines = await warnings(async () => { answer = await microsoftChecker.check(proof, ENV, denied.fetcher); });
  assert.deepEqual(answer, CANNOT_VERIFY);
  assert.ok(lines.some((line) => line.includes("status=401") && line.includes("error=invalid_client")));
  assertNothingSecret(lines);
  fresh();
  lines = await warnings(async () => { answer = await microsoftChecker.check(proof, ENV, async () => { throw new Error("connect secret 1.2.3.4"); }); });
  assert.deepEqual(answer, CANNOT_VERIFY);
  assert.ok(lines.some((line) => line.includes("check threw Error")));
  assertNothingSecret(lines);
  fresh();
  lines = await warnings(async () => { assert.equal(await serviceTicket(ENV, async () => { throw new Error("secret"); }), null); });
  assert.ok(lines.some((line) => line.includes("ticket mint threw Error")));
  assertNothingSecret(lines);
});

test("gate logs: one line per real store check, naming store and product, never the proof; no line for Off, no/bad proof, cache hits or the limiter", async () => {
  fresh();
  const book = { id: BOOK, productId: PRODUCT };
  const answers = [];
  const lines = await warnings(async () => {
    answers.push(await gateWith().gate(null, book));
    answers.push(await gateWith().gate("garbage", book));
    answers.push(await gateWith({ owned: [OTHER] }).gate(`windows:${OTHER}:STOREIDKEY`, book));
    answers.push(await gateWith({ verdict: CANNOT_VERIFY }).gate(`android:${PRODUCT}:STOREIDKEY2`, book));
    answers.push(await gateWith().gate(`ios:${PRODUCT}:STOREIDKEY3`, book, "1.2.3.4"));
  });
  assert.deepEqual(answers, [GateAnswer.NoProof, GateAnswer.BadProof, GateAnswer.BadProof, GateAnswer.CannotVerify, GateAnswer.Allowed]);
  assert.deepEqual(lines.map((line) => line.split(" ").slice(0, 3).join(" ")), [
    "paid-gate answer=bad-proof store=windows", "paid-gate answer=cannot-verify store=android", "paid-gate answer=allowed store=ios",
  ]);
  assert.ok(lines.every((line) => line.endsWith(`product=${PRODUCT}`)));
  assert.ok(!lines.join("\n").includes("STOREIDKEY") && !lines.join("\n").includes("1.2.3.4"));
  assert.deepEqual(await warnings(async () => { await gateWith({ enabled: false }).gate(null, book); }), []);
  const same = gateWith();
  const burst = await warnings(async () => { for (let i = 0; i < 4; i += 1) await same.gate(`ios:${PRODUCT}:STOREIDKEY4`, book); });
  assert.equal(burst.length, 1, "a cached answer is not logged again");
  const limited = createPaidGate({ enabled: async () => true, env: ENV, fetcher: async () => json({}), limit: async () => false, checkers: { android: { check: async () => checked([]) }, ios: { check: async () => checked([]) }, windows: { check: async () => checked([]) } } });
  assert.deepEqual(await warnings(async () => { assert.equal(await limited(`ios:${PRODUCT}:STOREIDKEY5`, book, "k"), GateAnswer.RateLimited); }), []);
});

test("Microsoft logs: id-like codes and foreign offer tokens are redacted", async () => {
  fresh();
  const proof = parseProof("windows::STOREIDKEY");
  const guid = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";
  const refusal = json({ code: guid, innererror: { code: "deadbeefcafebabe0123" } }, 400);
  const bad = recorded([["login.microsoftonline.com", json({ access_token: "ms-token" })], ["collections", refusal]]);
  let lines = await warnings(async () => { await microsoftChecker.check(proof, ENV, bad.fetcher); });
  assert.ok(lines.some((line) => line.includes("code=redacted") && line.includes("innererror=redacted")));
  assert.ok(!lines.join("\n").includes(guid));
  fresh();
  const denied = recorded([["login.microsoftonline.com", json({ error: guid }, 401)]]);
  lines = await warnings(async () => { await microsoftChecker.check(proof, ENV, denied.fetcher); });
  assert.ok(lines.some((line) => line.includes("error=redacted")));
  fresh();
  const items = [{ productType: "Durable", status: "Active", inAppOfferToken: PRODUCT }, { productType: "Durable", status: "Active", inAppOfferToken: "com.other.app.product" }, { productType: "Durable", status: "Active", inAppOfferToken: guid }];
  const good = recorded([["login.microsoftonline.com", json({ access_token: "ms-token" })], ["collections", json({ items })]]);
  lines = await warnings(async () => { await microsoftChecker.check(proof, ENV, good.fetcher); });
  const line = lines.find((entry) => entry.includes("items=3"));
  assert.ok(line.includes(`Durable/Active/${PRODUCT}`) && line.includes("Durable/Active/other,Durable/Active/other"));
  assert.ok(!line.includes("com.other.app") && !line.includes(guid));
});

test("gate: a good answer is remembered per proof for a request burst", async () => {
  fresh();
  const { gate, calls } = gateWith();
  const book = { id: BOOK, productId: PRODUCT };
  await gate(`android:${PRODUCT}:burst`, book);
  await gate(`android:${PRODUCT}:burst`, book);
  assert.equal(calls.checks, 1);
  await gate(`android:${PRODUCT}:other`, book);
  assert.equal(calls.checks, 2);
});

test("gate: an 'owns nothing' answer is remembered briefly, so a repeated bad proof asks the store once", async (t) => {
  fresh();
  const { gate, calls } = gateWith({ owned: [] });
  const book = { id: BOOK, productId: PRODUCT };
  assert.equal(await gate(`android:${PRODUCT}:nope`, book), GateAnswer.BadProof);
  assert.equal(await gate(`android:${PRODUCT}:nope`, book), GateAnswer.BadProof);
  assert.equal(calls.checks, 1);
  t.mock.timers.enable({ apis: ["Date"], now: Date.now() + NOT_OWNED_CACHE_MS + 1 });
  assert.equal(await gate(`android:${PRODUCT}:nope`, book), GateAnswer.BadProof);
  assert.equal(calls.checks, 2);
});

test("gate: the cache stays bounded", async () => {
  fresh();
  const { gate, calls } = gateWith({ owned: [] });
  const book = { id: BOOK, productId: PRODUCT };
  for (let index = 0; index < MAX_CACHED + 5; index += 1) await gate(`android:${PRODUCT}:t${index}`, book);
  await gate(`android:${PRODUCT}:t0`, book); // evicted, so asked again
  assert.equal(calls.checks, MAX_CACHED + 6);
});

test("gate: a bundle product unlocks every book on its list", async () => {
  fresh();
  const { gate } = gateWith({ owned: [BUNDLE] });
  // BUNDLES is empty in this build, so the product is checked through unlocksBook directly; the gate path with a real row is the same call.
  assert.equal(unlocksBook([BUNDLE], BOOK, PRODUCT, { [BUNDLE]: [BOOK, FREE_BOOK] }), true);
  assert.equal(await gate(`android:${BUNDLE}:t`, { id: BOOK, productId: PRODUCT }), GateAnswer.BadProof);
});

// The story routes behind the gate.
const FOLDER = (id) => `dinoreader/stories/${id}/`;
function storeBucket() {
  const metas = {
    [BOOK]: { schemaVersion: 1, title: "Goldilocks", language: "en", version: 1, access: "paid", productId: PRODUCT, audio: { en: { version: 1, bytes: 3, listenSeconds: 5 } } },
    [FREE_BOOK]: { schemaVersion: 1, title: "Lion", language: "en", version: 1, access: "free", productId: `${APP_PACKAGE_ID}.book.lion` },
  };
  const objects = new Map();
  for (const [id, meta] of Object.entries(metas)) {
    objects.set(`${FOLDER(id)}meta.json`, Buffer.from(JSON.stringify(meta)));
    objects.set(`${FOLDER(id)}story-v1.dino`, Buffer.from("pkg"));
    objects.set(`${FOLDER(id)}cover.jpg`, Buffer.from("jpg"));
    objects.set(`${FOLDER(id)}audio-en-v1.zip`, Buffer.from("zip"));
    objects.set(`${FOLDER(id)}listen-en-v1.m4a`, Buffer.from("m4a"));
  }
  return {
    async list({ prefix }) { return { objects: [...objects.keys()].filter((key) => key.startsWith(prefix)).map((key) => ({ key })), truncated: false }; },
    async head(key) { const body = objects.get(key); return body ? { size: body.length, httpEtag: '"e"' } : null; },
    async get(key) {
      const body = objects.get(key);
      return body ? { size: body.length, httpEtag: '"e"', body: new Response(body).body, json: async () => JSON.parse(body.toString()) } : null;
    },
  };
}

const callStories = (path, gate, headers = {}) =>
  handleStories(new Request(`https://x.test${path}`, { headers }), { DOWNLOADS: storeBucket() }, "", serveObject, gate);
const PAID_FILES = [`/api/stories/${BOOK}/download?v=1`, `/api/stories/${BOOK}/audio/en?v=1`, `/api/stories/${BOOK}/listen/en?v=1`];

test("routes: with the switch off every file is free and needs no proof", async () => {
  fresh();
  const { gate } = gateWith({ enabled: false });
  for (const path of PAID_FILES) assert.equal((await callStories(path, gate)).status, 200, path);
});

// Off or unreadable: exactly origin/main's answer and cache header, the same as with no gate at all.
const MAIN_CACHE = { [PAID_FILES[0]]: "public, max-age=86400", [PAID_FILES[1]]: "public, max-age=86400", [PAID_FILES[2]]: "public, max-age=86400" };
const unreadable = () => createPaidGate({ enabled: async () => { throw new Error("r2"); }, env: ENV, fetcher: async () => json({}) });

test("routes: switch off or unreadable, paid books' files answer byte for byte as without a gate (no 503, no no-store)", async () => {
  fresh();
  for (const gate of [gateWith({ enabled: false }).gate, unreadable()]) {
    for (const path of [...PAID_FILES, `/api/stories/${BOOK}/download`]) {
      const plain = await callStories(path, undefined);
      const gated = await callStories(path, gate);
      assert.equal(gated.status, plain.status, path);
      assert.equal(gated.headers.get("Cache-Control"), plain.headers.get("Cache-Control"), path);
      assert.deepEqual(Buffer.from(await gated.arrayBuffer()), Buffer.from(await plain.arrayBuffer()), path);
    }
    assert.equal((await callStories(PAID_FILES[0], gate)).headers.get("Cache-Control"), MAIN_CACHE[PAID_FILES[0]]);
  }
});

test("routes: a paid book's package, audio pack and listening track are 402 without proof, 403 with a bad one, 200 with a good one", async () => {
  fresh();
  const { gate } = gateWith();
  for (const path of PAID_FILES) {
    assert.equal((await callStories(path, gate)).status, 402, path);
    assert.equal((await callStories(path, gate, { "X-Purchase-Proof": "nonsense" })).status, 403, path);
    assert.equal((await callStories(path, gate, { "X-Purchase-Proof": `android:${OTHER}:t` })).status, 403, path);
  }
  fresh();
  for (const path of PAID_FILES) {
    const response = await callStories(path, gateWith().gate, { "X-Purchase-Proof": `android:${PRODUCT}:t` });
    assert.equal(response.status, 200, path);
  }
});

test("routes: a store that cannot be asked is 503 with no body (Slice 357)", async () => {
  fresh();
  const stuck = gateWith({ verdict: CANNOT_VERIFY }).gate;
  const response = await callStories(PAID_FILES[0], stuck, { "X-Purchase-Proof": `android:${PRODUCT}:t` });
  assert.equal(response.status, 503);
  assert.equal(await response.text(), "");
});

test("routes: a free book, the cover and the list never need proof, and the list says what is paid", async () => {
  fresh();
  const { gate } = gateWith();
  assert.equal((await callStories(`/api/stories/${FREE_BOOK}/download?v=1`, gate)).status, 200);
  assert.equal((await callStories(`/api/stories/${BOOK}/cover?v=1`, gate)).status, 200);
  const list = await (await callStories("/api/stories", gate)).json();
  const paid = list.stories.find((story) => story.id === BOOK);
  const free = list.stories.find((story) => story.id === FREE_BOOK);
  assert.equal(paid.access, "paid");
  assert.equal(paid.productId, PRODUCT);
  assert.deepEqual(paid.unlockedBy, [PRODUCT]);
  assert.equal(free.access, "free");
  assert.deepEqual(free.unlockedBy, []);
});

test("a meta without access is free (an old listing stays open), a wrong value is paid, a bad product id is dropped", () => {
  assert.equal(parseMeta({ schemaVersion: 1, title: "t", language: "en" }).access, "free");
  assert.equal(parseMeta({ schemaVersion: 1, title: "t", language: "en", access: null }).access, "free");
  assert.equal(parseMeta({ schemaVersion: 1, title: "t", language: "en", access: "paid" }).access, "paid");
  assert.equal(parseMeta({ schemaVersion: 1, title: "t", language: "en", access: "FREE" }).access, "paid");
  assert.equal(parseMeta({ schemaVersion: 1, title: "t", language: "en", access: "free" }).access, "free");
  assert.equal(parseMeta({ schemaVersion: 1, title: "t", language: "en", productId: "no spaces!" }).productId, null);
  const item = toListItem({ id: BOOK, meta: parseMeta({ schemaVersion: 1, title: "t", language: "en", access: "free" }), version: 1, hasCover: false }, "");
  assert.equal(item.access, "free");
});

test("routes: a paid book's files are private, no-store; a free book's keep their unchanged public rule", async () => {
  fresh();
  const proof = { "X-Purchase-Proof": `android:${PRODUCT}:t` };
  for (const path of PAID_FILES) {
    const response = await callStories(path, gateWith().gate, proof);
    assert.equal(response.headers.get("Cache-Control"), "private, no-store", path);
  }
  const free = await callStories(`/api/stories/${FREE_BOOK}/download?v=1`, gateWith().gate);
  assert.equal(free.headers.get("Cache-Control"), "public, max-age=86400"); // the unchanged rule for a versioned free file
  const unversioned = await callStories(`/api/stories/${BOOK}/download`, gateWith().gate, proof);
  assert.equal(unversioned.headers.get("Cache-Control"), "private, no-store");
});

test("gate: a client over its limit never reaches a store and is never owned; cached proofs do not count", async () => {
  fresh();
  const asked = [];
  const checker = { async check(proof) { asked.push(proof.token); return checked([proof.productId]); } };
  const checkers = { android: checker, ios: checker, windows: checker };
  let allowed = 1;
  const limit = async () => { allowed -= 1; return allowed >= 0; };
  const gate = createPaidGate({ enabled: async () => true, env: ENV, fetcher: async () => json({}), checkers, limit });
  const book = { id: BOOK, productId: PRODUCT };
  assert.equal(await gate(`android:${PRODUCT}:one`, book, "1.2.3.4"), GateAnswer.Allowed);
  assert.equal(await gate(`android:${PRODUCT}:one`, book, "1.2.3.4"), GateAnswer.Allowed); // remembered: no store call, no limit used
  assert.equal(await gate(`android:${PRODUCT}:two`, book, "1.2.3.4"), GateAnswer.RateLimited);
  assert.deepEqual(asked, ["one"]);
  const response = await callStories(PAID_FILES[0], gate, { "X-Purchase-Proof": `android:${PRODUCT}:three`, "CF-Connecting-IP": "1.2.3.4" });
  assert.equal(response.status, 429);
  assert.equal(await response.text(), ""); // Slice 357: status only
});

test("routes: the Windows ticket is served only when a ticket source has one, never cached, and 429 over the limit", async () => {
  const call = (ticket) => handleStories(new Request("https://x.test/api/stories/windows-ticket", { headers: { "CF-Connecting-IP": "1.2.3.4" } }), { DOWNLOADS: storeBucket() }, "", serveObject, undefined, ticket);
  const served = await call(async () => ({ ticket: "entra-token" }));
  assert.equal(served.status, 200);
  assert.equal(served.headers.get("Cache-Control"), "no-cache");
  assert.deepEqual(await served.json(), { ticket: "entra-token" });
  assert.equal((await call(async () => ({ none: true }))).status, 404);
  assert.equal((await call(async () => ({ limited: true }))).status, 429);
  assert.equal((await call(async () => { throw new Error("microsoft down"); })).status, 404);
  assert.equal((await call(undefined)).status, 404);
  let key = "";
  await call(async (clientKey) => { key = clientKey; return { none: true }; });
  assert.equal(key, "1.2.3.4");
});
