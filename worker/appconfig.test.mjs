// Run: node --test worker/appconfig.test.mjs (node 22.18+/24 strip the types of appconfig.ts).
import test from "node:test";
import assert from "node:assert/strict";

import { APP_CONFIG_KEY, DEFAULT_CONFIG, handleAppConfig, isAppConfigPath, parseAppConfig } from "./appconfig.ts";
import { resolveSite, Product } from "./downloads.ts";

function bucketWith(text) {
  return {
    async get(key) {
      if (key !== APP_CONFIG_KEY || text === null) return null;
      return { json: async () => JSON.parse(text) };
    },
  };
}

const get = (method = "GET") => new Request("https://dinoreader.sirous.uk/api/app-config", { method });

test("a valid config is served with a five minute cache", async () => {
  const body = {
    android: { minVersion: "1.2.0", latestVersion: "1.3.1" },
    ios: { minVersion: "1.0.0" },
    telemetry: { enabled: true, sampleRate: 0.25 },
  };
  const response = await handleAppConfig(get(), { DOWNLOADS: bucketWith(JSON.stringify(body)) });
  assert.equal(response.status, 200);
  assert.equal(response.headers.get("Cache-Control"), "public, max-age=300");
  const json = await response.json();
  assert.deepEqual(json.android, { minVersion: "1.2.0", latestVersion: "1.3.1" });
  assert.deepEqual(json.ios, { minVersion: "1.0.0", latestVersion: "0.0.0" });
  assert.deepEqual(json.windows, DEFAULT_CONFIG.windows);
  assert.deepEqual(json.telemetry, { enabled: true, sampleRate: 0.25 });
});

test("a missing file gives the safe defaults", async () => {
  const response = await handleAppConfig(get(), { DOWNLOADS: bucketWith(null) });
  assert.equal(response.status, 200);
  assert.deepEqual(await response.json(), DEFAULT_CONFIG);
});

test("malformed JSON or a non-object body is a 503, so clients keep their last good copy", async () => {
  for (const body of ["{not json", "[]", "null"]) {
    const response = await handleAppConfig(get(), { DOWNLOADS: bucketWith(body) });
    assert.equal(response.status, 503);
    assert.equal(response.headers.get("Cache-Control"), "no-cache");
  }
});

test("bad fields fall back one by one", () => {
  const config = parseAppConfig({
    android: { minVersion: "latest", latestVersion: "2.0.0" },
    windows: "nope",
    telemetry: { enabled: true, sampleRate: 3 },
  });
  assert.deepEqual(config.android, { minVersion: "0.0.0", latestVersion: "2.0.0" });
  assert.deepEqual(config.windows, DEFAULT_CONFIG.windows);
  assert.deepEqual(config.telemetry, { enabled: false, sampleRate: 0 });
  assert.deepEqual(parseAppConfig([]), DEFAULT_CONFIG);
  assert.equal(parseAppConfig({ telemetry: { enabled: "true", sampleRate: 1 } }).telemetry.enabled, false);
});

test("a failed R2 read is a 503 and the HEAD reply has no body", async () => {
  const broken = { async get() { throw new Error("r2 down"); } };
  const failed = await handleAppConfig(get(), { DOWNLOADS: broken });
  assert.equal(failed.status, 503);
  assert.equal(failed.headers.get("Cache-Control"), "no-cache");
  assert.equal((await handleAppConfig(get(), {})).status, 503);
  const head = await handleAppConfig(get("HEAD"), { DOWNLOADS: bucketWith(null) });
  assert.equal(head.status, 200);
  assert.equal(await head.text(), "");
});

test("other methods are refused", async () => {
  const response = await handleAppConfig(get("POST"), { DOWNLOADS: bucketWith(null) });
  assert.equal(response.status, 405);
});

test("the path routes for the Dino host and its workers.dev prefix only", () => {
  assert.equal(isAppConfigPath("/api/app-config"), true);
  assert.equal(isAppConfigPath("/api/app-config/x"), false);
  const dino = resolveSite("dinoreader.sirous.uk", "/api/app-config");
  assert.equal(dino.product, Product.DinoReader);
  assert.equal(isAppConfigPath(dino.path), true);
  const dev = resolveSite("x.workers.dev", "/dino/api/app-config");
  assert.equal(dev.product, Product.DinoReader);
  assert.equal(isAppConfigPath(dev.path), true);
  assert.equal(resolveSite("readthestupidtext.sirous.uk", "/api/app-config").product, Product.ReadTheStupidText);
});
