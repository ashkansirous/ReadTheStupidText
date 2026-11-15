// Run: node --test worker/apple-jws.test.mjs (Slice 390: the StoreKit 2 signed transaction as the iOS purchase proof. The chain is a throwaway one from apple-jws-fixture.mjs, trusted only
// where a test passes its fingerprint in through createAppleChecker; the real Apple root is pinned by APPLE_ROOT_CA_G3_SHA256.)
import test from "node:test";
import assert from "node:assert/strict";
import { createHash, generateKeyPairSync, sign } from "node:crypto";

import { APPLE_ROOT_CA_G3_SHA256, certificateExtensions, verifyAppleSignedPayload } from "./entitlements/apple-jws.ts";
import { appleChecker as realChecker, createAppleChecker, resetAppleTokenCache } from "./entitlements/apple.ts";
import { base64UrlEncode } from "./entitlements/jwt.ts";
import { parseProof } from "./entitlements/proof.ts";
import { CANNOT_VERIFY, CANNOT_VERIFY_SUBSCRIPTION, Verdict, checked } from "./entitlements/types.ts";
import { APP_PACKAGE_ID, SUBSCRIPTION_MONTHLY_PRODUCT } from "./products.ts";
import { INTERMEDIATE_DER_BASE64, LEAF_DER_BASE64, LEAF_PRIVATE_KEY_PEM, ROOT_DER_BASE64 } from "./apple-jws-fixture.mjs";

const PRODUCT = `${APP_PACKAGE_ID}.book.goldilocks`;
const SIGNED_AT = Date.UTC(2027, 0, 1);
const HOUR = 3_600_000;
const TEST_ROOT = createHash("sha256").update(Buffer.from(ROOT_DER_BASE64, "base64")).digest("hex");
const ec = generateKeyPairSync("ec", { namedCurve: "P-256" });
const ENV = { APPLE_IAP_KEY_P8: ec.privateKey.export({ type: "pkcs8", format: "pem" }), APPLE_IAP_KEY_ID: "KEYID", APPLE_IAP_ISSUER_ID: "issuer" };

const appleChecker = createAppleChecker([TEST_ROOT]);
const encode = (value) => base64UrlEncode(Buffer.from(JSON.stringify(value)));
const CHAIN = [LEAF_DER_BASE64, INTERMEDIATE_DER_BASE64, ROOT_DER_BASE64];

function jws(payload, { x5c = CHAIN, alg = "ES256" } = {}) {
  const input = `${encode({ alg, x5c })}.${encode(payload)}`;
  const signature = sign("sha256", Buffer.from(input), { key: LEAF_PRIVATE_KEY_PEM, dsaEncoding: "ieee-p1363" });
  return `${input}.${base64UrlEncode(signature)}`;
}

const transaction = (extra = {}) => ({ signedDate: SIGNED_AT, bundleId: APP_PACKAGE_ID, productId: PRODUCT, environment: "Production", transactionId: "2000000123", originalTransactionId: "2000000100", ...extra });
const json = (body, status = 200) => new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
const unsigned = (payload) => `${encode({})}.${encode(payload)}.sig`;

function recorded(rules) {
  const calls = [];
  const fetcher = async (url, init) => {
    calls.push({ url, init });
    const rule = rules.find(([fragment]) => url.includes(fragment));
    if (!rule) throw new Error(`unrecorded request ${url}`);
    return rule[1].clone();
  };
  return { fetcher, calls };
}

test("the pinned root is Apple Root CA - G3's SHA-256", () => {
  assert.match(APPLE_ROOT_CA_G3_SHA256, /^[0-9a-f]{64}$/);
  assert.equal(APPLE_ROOT_CA_G3_SHA256, "63343abfb89a6a03ebb57e9b3f5fa7be7c4f5c756f3017b3a8c488c3653e9179");
});

test("a payload signed through a chain that ends in a trusted root is believed", async () => {
  const payload = await verifyAppleSignedPayload(jws(transaction()), { roots: [TEST_ROOT] });
  assert.equal(payload?.transactionId, "2000000123");
});

test("the real Apple root is the default: a chain that ends in another root is not believed", async () => {
  assert.equal(await verifyAppleSignedPayload(jws(transaction())), null);
  assert.equal(await verifyAppleSignedPayload(jws(transaction()), { roots: ["0".repeat(64)] }), null);
});

test("a tampered payload, a swapped signature or the wrong algorithm is not believed", async () => {
  const good = jws(transaction()).split(".");
  const tampered = [good[0], encode(transaction({ productId: "other" })), good[2]].join(".");
  assert.equal(await verifyAppleSignedPayload(tampered, { roots: [TEST_ROOT] }), null);
  const other = jws(transaction({ productId: "other" })).split(".");
  assert.equal(await verifyAppleSignedPayload([good[0], good[1], other[2]].join("."), { roots: [TEST_ROOT] }), null);
  assert.equal(await verifyAppleSignedPayload(jws(transaction(), { alg: "none" }), { roots: [TEST_ROOT] }), null);
  for (const bad of ["", "a.b", "a.b.c.d", unsigned(transaction())]) assert.equal(await verifyAppleSignedPayload(bad, { roots: [TEST_ROOT] }), null, bad);
});

test("a broken chain is not believed: a missing intermediate, a leaf only, certificates out of order", async () => {
  for (const x5c of [[LEAF_DER_BASE64, ROOT_DER_BASE64], [LEAF_DER_BASE64], [ROOT_DER_BASE64, INTERMEDIATE_DER_BASE64, LEAF_DER_BASE64], [INTERMEDIATE_DER_BASE64, LEAF_DER_BASE64, ROOT_DER_BASE64], "x", [1, 2, 3]]) {
    assert.equal(await verifyAppleSignedPayload(jws(transaction(), { x5c }), { roots: [TEST_ROOT] }), null, JSON.stringify(x5c).slice(0, 40));
  }
});

test("the certificates must be valid when Apple signed, by the payload's own signedDate", async () => {
  const roots = { roots: [TEST_ROOT] };
  assert.equal(await verifyAppleSignedPayload(jws(transaction({ signedDate: Date.UTC(2020, 0, 1) })), roots), null); // before the chain existed
  assert.equal(await verifyAppleSignedPayload(jws(transaction({ signedDate: Date.UTC(2130, 0, 1) })), roots), null); // after it expired
  const noDate = transaction();
  delete noDate.signedDate;
  assert.ok(await verifyAppleSignedPayload(jws(noDate), { ...roots, now: SIGNED_AT })); // no signedDate: the clock decides
  assert.equal(await verifyAppleSignedPayload(jws(noDate), { ...roots, now: Date.UTC(2020, 0, 1) }), null);
});

test("Apple checker: a verified JWS is checked by its transaction id; the answer from Apple still decides", async () => {
  resetAppleTokenCache();
  try {
    const proof = parseProof(`ios:${PRODUCT}:${jws(transaction())}`);
    const apple = recorded([["api.storekit.apple.com", json({ signedTransactionInfo: unsigned({ bundleId: APP_PACKAGE_ID, productId: PRODUCT, environment: "Production" }) })]]);
    assert.deepEqual(await appleChecker.check(proof, ENV, apple.fetcher), { ...checked([PRODUCT]), environment: "Production" });
    assert.ok(apple.calls[0].url.endsWith("/inApps/v1/transactions/2000000123"));
    const revoked = recorded([["api.storekit.apple.com", json({ signedTransactionInfo: unsigned({ bundleId: APP_PACKAGE_ID, productId: PRODUCT, environment: "Production", revocationDate: 1 }) })]]);
    assert.deepEqual((await appleChecker.check(proof, ENV, revoked.fetcher)).owned, []);
    const down = recorded([["api.storekit.apple.com", json({}, 500)]]);
    assert.deepEqual(await appleChecker.check(proof, ENV, down.fetcher), CANNOT_VERIFY);
  } finally {
    resetAppleTokenCache();
  }
});

test("Apple checker: a JWS that is forged, for another app or product, or a sandbox one owns nothing and Apple is not asked", async () => {
  resetAppleTokenCache();
  try {
    const never = recorded([["api.storekit", json({})]]);
    const forged = jws(transaction()).split(".");
    const unverified = [
      [forged[0], encode(transaction({ transactionId: "2999999999" })), forged[2]].join("."),
      jws(transaction({ environment: "Production" }), { x5c: [LEAF_DER_BASE64, INTERMEDIATE_DER_BASE64, INTERMEDIATE_DER_BASE64] }), // not three distinct certificates ending in the root
      jws(transaction({ environment: "Sandbox" })), // verified, but a Worker without the sandbox switch cannot say: not cached as "not owned"
    ];
    for (const token of unverified) assert.deepEqual(await appleChecker.check(parseProof(`ios:${PRODUCT}:${token}`), ENV, never.fetcher), CANNOT_VERIFY, token.slice(0, 20)); // a failed verification is never cached as "not owned"
    const cases = [
      jws(transaction({ bundleId: "other.app" })),
      jws(transaction({ productId: `${APP_PACKAGE_ID}.book.red-riding-hood` })),
      jws(transaction({ transactionId: "not-a-number" })),
      jws(transaction({ environment: "Xcode" })),
      jws(transaction({ productId: "other.app.book.goldilocks" }), { x5c: CHAIN }), // a product that is not one of ours, whatever the proof says
    ];
    for (const token of cases) assert.deepEqual(await appleChecker.check(parseProof(`ios:${PRODUCT}:${token}`), ENV, never.fetcher), checked([]), token.slice(0, 20));
    assert.equal(never.calls.length, 0);
    // the real root: a test-chain JWS is not Apple's
    assert.deepEqual(await realChecker.check(parseProof(`ios:${PRODUCT}:${jws(transaction())}`), ENV, never.fetcher), CANNOT_VERIFY);
    assert.equal(never.calls.length, 0);
  } finally {
    resetAppleTokenCache();
  }
});

test("Apple checker: the production Worker (no sandbox switch) refuses a verified Sandbox JWS; a Worker that allows sandbox asks the sandbox host and records the environment", async () => {
  resetAppleTokenCache();
  const sandbox = { bundleId: APP_PACKAGE_ID, productId: PRODUCT, environment: "Sandbox" };
  const proof = parseProof(`ios:${PRODUCT}:${jws(transaction({ environment: "Sandbox" }))}`);
  const refused = recorded([["api.storekit", json({})]]);
  assert.deepEqual(await appleChecker.check(proof, ENV, refused.fetcher), CANNOT_VERIFY);
  assert.equal(refused.calls.length, 0);
  const apple = recorded([["api.storekit-sandbox.apple.com", json({ signedTransactionInfo: unsigned(sandbox) })]]);
  const result = await appleChecker.check(proof, { ...ENV, APPLE_IAP_ALLOW_SANDBOX: "true" }, apple.fetcher);
  assert.deepEqual(result, { ...checked([PRODUCT]), environment: "Sandbox" });
  assert.equal(apple.calls.length, 1);
  assert.ok(apple.calls[0].url.startsWith("https://api.storekit-sandbox.apple.com/inApps/v1/transactions/2000000123"));
  const production = await appleChecker.check(parseProof(`ios:${PRODUCT}:${jws(transaction())}`), ENV, recorded([["api.storekit.apple.com", json({ signedTransactionInfo: unsigned({ ...sandbox, environment: "Production" }) })]]).fetcher);
  assert.equal(production.environment, "Production");
  const plainSandboxId = recorded([["api.storekit.apple.com", json({}, 404)]]); // a plain transaction id has no verified environment: sandbox stays refused without the switch
  assert.deepEqual(await appleChecker.check(parseProof(`ios:${PRODUCT}:2000000123`), ENV, plainSandboxId.fetcher), checked([]));
  assert.equal(plainSandboxId.calls.length, 1);
});

test("a marker OID that is not an extension of the certificate does not count (the check reads the extensions, not the bytes)", () => {
  const leaf = Buffer.from(LEAF_DER_BASE64, "base64");
  const LEAF_OID = Buffer.from("2a864886f76364060b01", "hex");
  assert.ok(certificateExtensions(leaf).includes(LEAF_OID.toString("hex")));
  const forged = Buffer.from(leaf);
  const at = forged.indexOf(LEAF_OID);
  forged[at + LEAF_OID.length - 1] = 0x02; // the extension now names another OID ...
  const subject = forged.indexOf(Buffer.from("Test Receipt Signing")); // ... while the marker bytes sit in a field its holder controls
  LEAF_OID.copy(forged, subject);
  assert.ok(forged.toString("hex").includes(LEAF_OID.toString("hex")));
  assert.ok(!certificateExtensions(forged).includes(LEAF_OID.toString("hex")));
});

test("a chain must hold exactly three certificates; the product must be one of ours", async () => {
  const roots = { roots: [TEST_ROOT] };
  for (const x5c of [[LEAF_DER_BASE64, INTERMEDIATE_DER_BASE64, ROOT_DER_BASE64, ROOT_DER_BASE64], [LEAF_DER_BASE64, INTERMEDIATE_DER_BASE64]]) {
    assert.equal(await verifyAppleSignedPayload(jws(transaction(), { x5c }), roots), null, String(x5c.length));
  }
  assert.ok(await verifyAppleSignedPayload(jws(transaction()), roots));
  const strange = "other.app.book.goldilocks";
  const never = recorded([["api.storekit", json({})]]);
  assert.deepEqual(await appleChecker.check(parseProof(`ios:${strange}:${jws(transaction({ productId: strange }))}`), ENV, never.fetcher), checked([]));
  assert.equal(never.calls.length, 0);
});

test("a certificate's dates allow 60 seconds of clock skew", async () => {
  const roots = { roots: [TEST_ROOT] };
  const notBefore = Date.UTC(2026, 10, 15, 11, 40, 12); // the fixture chain's notBefore
  assert.ok(await verifyAppleSignedPayload(jws(transaction({ signedDate: notBefore - 30_000 })), roots));
  assert.equal(await verifyAppleSignedPayload(jws(transaction({ signedDate: notBefore - 120_000 })), roots), null);
});

test("Apple checker: a staging Worker that allows sandbox accepts a verified Sandbox JWS", async () => {
  resetAppleTokenCache();
  try {
    const apple = recorded([["api.storekit-sandbox.apple.com", json({ signedTransactionInfo: unsigned({ bundleId: APP_PACKAGE_ID, productId: PRODUCT, environment: "Sandbox" }) })], ["api.storekit.apple.com", json({}, 404)]]);
    const proof = parseProof(`ios:${PRODUCT}:${jws(transaction({ environment: "Sandbox" }))}`);
    assert.deepEqual((await appleChecker.check(proof, { ...ENV, APPLE_IAP_ALLOW_SANDBOX: "true" }, apple.fetcher)).owned, [PRODUCT]);
  } finally {
    resetAppleTokenCache();
  }
});

test("Apple checker: a subscription JWS asks the statuses by its original transaction id; a bad one is cannot-verify, never expired", async () => {
  resetAppleTokenCache();
  try {
    const plan = { bundleId: APP_PACKAGE_ID, productId: SUBSCRIPTION_MONTHLY_PRODUCT, environment: "Production", expiresDate: Date.now() + HOUR };
    const statuses = json({ data: [{ lastTransactions: [{ status: 1, signedTransactionInfo: unsigned(plan) }] }] });
    const token = jws(transaction({ productId: SUBSCRIPTION_MONTHLY_PRODUCT }));
    const proof = parseProof(`ios:${SUBSCRIPTION_MONTHLY_PRODUCT}:${token}`);
    const apple = recorded([["api.storekit.apple.com", statuses]]);
    assert.deepEqual((await appleChecker.check(proof, ENV, apple.fetcher)).owned, [SUBSCRIPTION_MONTHLY_PRODUCT]);
    assert.ok(apple.calls[0].url.endsWith("/inApps/v1/subscriptions/2000000100"));
    const status = await appleChecker.subscription(proof, ENV, recorded([["api.storekit.apple.com", statuses]]).fetcher);
    assert.equal(status.verdict, Verdict.Checked);
    assert.equal(status.status.state, "active");
    const forged = parseProof(`ios:${SUBSCRIPTION_MONTHLY_PRODUCT}:${[...token.split(".").slice(0, 2), "AAAA"].join(".")}`);
    assert.deepEqual(await appleChecker.subscription(forged, ENV, apple.fetcher), CANNOT_VERIFY_SUBSCRIPTION);
    assert.deepEqual(await appleChecker.check(forged, ENV, apple.fetcher), CANNOT_VERIFY);
  } finally {
    resetAppleTokenCache();
  }
});

test("Apple checker: a plain transaction id from a build before StoreKit 2 still goes straight to Apple", async () => {
  resetAppleTokenCache();
  const proof = parseProof(`ios:${PRODUCT}:2000000123`);
  const apple = recorded([["api.storekit.apple.com", json({ signedTransactionInfo: unsigned({ bundleId: APP_PACKAGE_ID, productId: PRODUCT, environment: "Production" }) })]]);
  assert.deepEqual(await appleChecker.check(proof, ENV, apple.fetcher), checked([PRODUCT]));
  assert.ok(apple.calls[0].url.endsWith("/inApps/v1/transactions/2000000123"));
});
