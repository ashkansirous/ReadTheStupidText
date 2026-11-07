// Run: node --test tools/crashes/pull-store.test.mjs
// The recorded response is the sample of https://learn.microsoft.com/en-us/windows/uwp/monetize/get-error-reporting-data (trimmed to the fields the script reads).
import test from "node:test";
import assert from "node:assert/strict";

import { parseArgs, tokenRequest, apiDate, failureHitsUrl, nextUrl, parseRows, collectRows, groupRows, formatGroups, requireEnv, storeIds, dateRange } from "./pull-store.mjs";

const hang = "APPLICATION_HANG_Microsoft.Contoso Demo!CEServices.InternalLiveTileUpdaterRuntime_dfffffff_twinapi.appcore.dll!WaitCoalesced";
const recorded = {
  Value: [
    { date: "2022-07-21", applicationId: "9NBLGGGZ5QDR", failureName: "APPLICATION_HANG_BlockedOn_FileIO", eventType: "hang", market: "US", deviceType: "PC", packageVersion: "2.5.2.34894", deviceCount: 6.0, eventCount: 1.05263157894737 },
    { date: "2022-07-22", applicationId: "9NBLGGGZ5QDR", failureName: hang, eventType: "hang", market: "US", deviceType: "PC", packageVersion: "2.5.2.34894", deviceCount: 6.0, eventCount: 8.94736842105263 },
    { date: "2022-07-23", applicationId: "9NBLGGGZ5QDR", failureName: hang, eventType: "hang", market: "DE", deviceType: "PC", packageVersion: "2.5.3.1", deviceCount: 2.0, eventCount: 3.0 },
  ],
  "@nextLink": "failurehits?applicationId=9NBLGGGZ5QDR&skip=10",
  TotalCount: 443,
};

test("the token request is the Entra client-credentials call for the Store resource", () => {
  const { url, body } = tokenRequest({ AZURE_AD_TENANT_ID: "tenant", AZURE_AD_APPLICATION_CLIENT_ID: "id", AZURE_AD_APPLICATION_SECRET: "s e/c" });
  assert.equal(url, "https://login.microsoftonline.com/tenant/oauth2/token");
  const form = new URLSearchParams(body);
  assert.equal(form.get("grant_type"), "client_credentials");
  assert.equal(form.get("client_secret"), "s e/c");
  assert.equal(form.get("resource"), "https://manage.devcenter.microsoft.com");
});

test("the failurehits URL carries the Store id and mm/dd/yyyy dates", () => {
  assert.equal(apiDate("2026-11-07"), "11/07/2026");
  const url = new URL(failureHitsUrl("9NGT1BN1H92V", "2026-10-09", "2026-11-07"));
  assert.equal(url.pathname, "/v1.0/my/analytics/failurehits");
  assert.equal(url.searchParams.get("applicationId"), "9NGT1BN1H92V");
  assert.equal(url.searchParams.get("startDate"), "10/09/2026");
  assert.equal(url.searchParams.get("endDate"), "11/07/2026");
});

test("a relative nextLink is resolved and the last page has none", () => {
  assert.equal(nextUrl(recorded), "https://manage.devcenter.microsoft.com/v1.0/my/analytics/failurehits?applicationId=9NBLGGGZ5QDR&skip=10");
  assert.equal(nextUrl({ Value: [] }), undefined);
  assert.throws(() => parseRows({ error: "x" }), /no Value array/);
});

test("collectRows follows the nextLink to the last page", async () => {
  const seen = [];
  const rows = await collectRows("first", async (url) => (seen.push(url), url === "first" ? { ...recorded, TotalCount: 4 } : { Value: [recorded.Value[0]], TotalCount: 4 }));
  assert.equal(rows.length, 4);
  assert.equal(seen.length, 2);
});

test("rows group by event type and failure, biggest first, counts rounded, newest date kept", () => {
  const groups = groupRows(recorded.Value);
  assert.equal(groups.length, 2);
  assert.equal(groups[0].key, `hang | ${hang}`);
  assert.equal(Math.round(groups[0].count), 12);
  assert.equal(groups[0].newest, "2022-07-23");
  assert.deepEqual(Object.keys(groups[0].versions).sort(), ["2.5.2.34894", "2.5.3.1"]);
});

test("the printout has pull-crashes's shape, and an empty answer says none", () => {
  const text = formatGroups("rtst", recorded.Value, 15);
  assert.match(text, /=== rtst: 3 failure row\(s\) ===/);
  assert.match(text, /^12 x hang \| APPLICATION_HANG_Microsoft/m);
  assert.match(text, /versions 2\.5\.2\.34894:9 2\.5\.3\.1:3 \| markets US:9 DE:3/);
  assert.match(formatGroups("dino", [], 15), /none \(no failure rows/);
});

test("missing credentials and Store ids are named, RTST has no default id", () => {
  assert.throws(() => requireEnv({ A: "1" }, ["A", "B"]), /B/);
  assert.throws(() => storeIds({}, "rtst"), /STORE_PRODUCT_ID/);
  assert.throws(() => storeIds({}, "both"), /STORE_PRODUCT_ID/);
  assert.deepEqual(storeIds({ STORE_PRODUCT_ID: "R1" }, "rtst").ids, { rtst: "R1" });
  assert.throws(() => storeIds({}, "dino"), /DINOREADER_STORE_APP_ID/);
  assert.deepEqual(storeIds({ DINOREADER_STORE_APP_ID: "D1" }, "dino").ids, { dino: "D1" });
  assert.deepEqual(storeIds({ DINOREADER_STORE_APP_ID: "D1", STORE_PRODUCT_ID: "R1" }, "both").ids, { dino: "D1", rtst: "R1" });
});

test("--app both skips Dino with a note when its id is unset", () => {
  const { ids, notes } = storeIds({ STORE_PRODUCT_ID: "R1" }, "both");
  assert.deepEqual(ids, { rtst: "R1" });
  assert.match(notes[0], /dino skipped/);
});

test("parseArgs defaults and validates app, top and dates", () => {
  assert.deepEqual(parseArgs([]), { app: "both", top: 15 });
  assert.deepEqual(parseArgs(["--app", "dino", "--top", "5", "--from", "2026-10-01", "--to", "2026-11-01"]), { app: "dino", top: 5, from: "2026-10-01", to: "2026-11-01" });
  assert.throws(() => parseArgs(["--app", "rtsst"]), /--app must be/);
  assert.throws(() => parseArgs(["--top", "abc"]), /--top/);
  assert.throws(() => parseArgs(["--top", "0"]), /--top/);
  assert.throws(() => parseArgs(["--from", "2026/11/01"]), /--from/);
  assert.throws(() => parseArgs(["--to", "2026-13-45"]), /--to/);
  assert.throws(() => parseArgs(["--bogus", "1"]), /Unknown option/);
  assert.throws(() => parseArgs(["--app"]), /Missing value/);
});

test("nextUrl resolves absolute, root-relative and bare relative links", () => {
  const base = "https://manage.devcenter.microsoft.com";
  assert.equal(nextUrl({ "@nextLink": "/v1.0/my/analytics/failurehits?skip=1" }), `${base}/v1.0/my/analytics/failurehits?skip=1`);
  assert.equal(nextUrl({ "@nextLink": `${base}/v1.0/my/analytics/failurehits?skip=2` }), `${base}/v1.0/my/analytics/failurehits?skip=2`);
});

test("a repeated link, an empty page with a link, or fewer rows than TotalCount is incomplete, never a clean result", async () => {
  let calls = 0;
  await assert.rejects(collectRows("first", async () => (calls++, { Value: [recorded.Value[0]], "@nextLink": "failurehits?skip=1" })), (error) => /repeated/.test(error.message) && error.rows.length === 2);
  assert.equal(calls, 2);
  await assert.rejects(collectRows("a", async (url) => (url === "a" ? { Value: [recorded.Value[0]], "@nextLink": "b" } : { Value: [], "@nextLink": "c" })), (error) => /empty page/.test(error.message) && error.rows.length === 1);
  await assert.rejects(collectRows("a", async () => ({ Value: [recorded.Value[0]], TotalCount: 5 })), (error) => /1 of 5/.test(error.message) && error.rows.length === 1);
  assert.equal((await collectRows("a", async () => ({ Value: [recorded.Value[0]], TotalCount: 1 }))).length, 1);
});

test("a failed page carries the rows gathered so far", async () => {
  const fetchPage = async (url) => {
    if (url === "first") return { Value: [recorded.Value[0]], "@nextLink": "second" };
    throw new Error("failurehits HTTP 429");
  };
  await assert.rejects(collectRows("first", fetchPage), (error) => error.rows.length === 1 && /429/.test(error.message));
});

test("a nextLink to another host is never followed, so the token stays on the API host", async () => {
  assert.throws(() => nextUrl({ "@nextLink": "https://evil.example/steal" }), /another host/);
  assert.throws(() => nextUrl({ "@nextLink": "//evil.example/steal" }), /another host/);
  const hosts = [];
  const fetchPage = async (url) => (hosts.push(url), { Value: [recorded.Value[0]], "@nextLink": "https://evil.example/steal" });
  await assert.rejects(collectRows("https://manage.devcenter.microsoft.com/v1.0/my/analytics/failurehits", fetchPage), (error) => /another host/.test(error.message) && error.rows.length === 1);
  assert.equal(hosts.length, 1);
});

test("an empty STORE_PRODUCT_ID is as good as unset", () => {
  assert.throws(() => storeIds({ STORE_PRODUCT_ID: "" }, "rtst"), /STORE_PRODUCT_ID/);
});

test("dateRange rejects a reversed range and defaults to the last 30 days", () => {
  assert.throws(() => dateRange({ from: "2026-11-05", to: "2026-11-01" }), /must not be after/);
  assert.throws(() => dateRange({ from: "2026-12-01" }, "2026-11-07"), /must not be after/);
  assert.deepEqual(dateRange({}, "2026-11-07"), { from: "2026-10-09", to: "2026-11-07" });
});

test("rows without a date still group, newest reads n/a", () => {
  const text = formatGroups("rtst", [{ ...recorded.Value[0], date: undefined }], 15);
  assert.match(text, /newest n\/a/);
  assert.match(text, /device-days/);
});
