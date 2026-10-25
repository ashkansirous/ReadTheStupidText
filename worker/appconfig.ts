// Dino Reader's remote switchboard (Slice 132, Decision 138). The owner edits one R2 object,
// `dinoreader/config.json`, with `wrangler r2 object put`; this answers
//   GET /api/app-config    the checked config, read fresh from R2 on every request
// Per platform (android, ios, windows) `minVersion` forces an update and `latestVersion` only shows a quiet
// line; `telemetry` holds the usage-count switch and sample rate (Slices 133-134). A missing
// file, or any field in it that fails its check, falls back to the safe default (no forced update, telemetry
// off), so a typo in the file can never lock anybody out; an unparseable file answers 503 so phones keep their
// last good copy. Nothing about the caller is read or stored. Only
// Dino Reader's host and its /dino/ prefix on workers.dev route here (index.ts). Like stories.ts this file
// imports values only from download-keys.mjs so worker/appconfig.test.mjs can run it under node's type
// stripping: keep it free of enums and parameter properties.

import { JSON_CONTENT_TYPE, NO_CACHE } from "./download-keys.mjs";

export const APP_CONFIG_PATH = "/api/app-config";
export const APP_CONFIG_KEY = "dinoreader/config.json";

/** The response format; the app reads the fields it knows and ignores the rest. */
export const APP_CONFIG_SCHEMA_VERSION = 1;

// Only a hint for browsers and proxies: a Worker response is not stored by the Cloudflare edge cache.
const CONFIG_CACHE_CONTROL = "public, max-age=300";
const ALLOWED_METHODS = "GET, HEAD";
const NO_VERSION = "0.0.0";
const VERSION_PATTERN = /^\d{1,6}(\.\d{1,6}){1,3}$/;
const ErrorCode = { Unavailable: "config-unavailable" } as const;

export const PLATFORMS = ["android", "ios", "windows"] as const;

export interface PlatformVersions {
  /** The app refuses to run below this; "0.0.0" forces nothing. */
  readonly minVersion: string;
  /** The newest release; the app only mentions it quietly. */
  readonly latestVersion: string;
}

export interface TelemetryConfig {
  readonly enabled: boolean;
  /** The share of installs that count, 0 to 1. */
  readonly sampleRate: number;
}

export interface AppConfig {
  readonly schemaVersion: number;
  readonly android: PlatformVersions;
  readonly ios: PlatformVersions;
  readonly windows: PlatformVersions;
  readonly telemetry: TelemetryConfig;
}

export const DEFAULT_CONFIG: AppConfig = {
  schemaVersion: APP_CONFIG_SCHEMA_VERSION,
  android: { minVersion: NO_VERSION, latestVersion: NO_VERSION },
  ios: { minVersion: NO_VERSION, latestVersion: NO_VERSION },
  windows: { minVersion: NO_VERSION, latestVersion: NO_VERSION },
  telemetry: { enabled: false, sampleRate: 0 },
};

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function versionOr(value: unknown, fallback: string): string {
  return typeof value === "string" && VERSION_PATTERN.test(value) ? value : fallback;
}

function platformOf(value: unknown): PlatformVersions {
  const raw = isRecord(value) ? value : {};
  return { minVersion: versionOr(raw["minVersion"], NO_VERSION), latestVersion: versionOr(raw["latestVersion"], NO_VERSION) };
}

function telemetryOf(value: unknown): TelemetryConfig {
  const raw = isRecord(value) ? value : {};
  const rate = raw["sampleRate"];
  const validRate = typeof rate === "number" && Number.isFinite(rate) && rate >= 0 && rate <= 1;
  // Switched on only by a literal true with a usable rate; anything else is off.
  return raw["enabled"] === true && validRate ? { enabled: true, sampleRate: rate } : DEFAULT_CONFIG.telemetry;
}

/** A checked config from parsed JSON; every missing or malformed part takes its default. */
export function parseAppConfig(parsed: unknown): AppConfig {
  const raw = isRecord(parsed) ? parsed : {};
  return {
    schemaVersion: APP_CONFIG_SCHEMA_VERSION,
    android: platformOf(raw["android"]),
    ios: platformOf(raw["ios"]),
    windows: platformOf(raw["windows"]),
    telemetry: telemetryOf(raw["telemetry"]),
  };
}

function jsonResponse(body: unknown, status: number, cacheControl: string): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": JSON_CONTENT_TYPE, "Cache-Control": cacheControl } });
}

// A missing object means defaults (the file is not seeded yet). Unparseable JSON, a body that is not an object, or a failed
// R2 read throws and becomes a 503, which the app answers with its last good copy (Slice 132 review).
async function readConfig(bucket: R2Bucket): Promise<AppConfig> {
  const object = await bucket.get(APP_CONFIG_KEY);
  if (!object) return DEFAULT_CONFIG;
  const parsed: unknown = await object.json();
  if (!isRecord(parsed)) throw new Error("config.json is not a JSON object");
  return parseAppConfig(parsed);
}

/** True for the one path this module answers (after the `/dino` prefix is stripped). */
export function isAppConfigPath(pathname: string): boolean {
  return pathname === APP_CONFIG_PATH;
}

/** Answers GET and HEAD /api/app-config. */
export async function handleAppConfig(request: Request, env: { DOWNLOADS?: R2Bucket }): Promise<Response> {
  if (request.method !== "GET" && request.method !== "HEAD") {
    return new Response(null, { status: 405, headers: { Allow: ALLOWED_METHODS } });
  }
  if (!env.DOWNLOADS) return jsonResponse({ code: ErrorCode.Unavailable }, 503, NO_CACHE);
  let response: Response;
  try {
    response = jsonResponse(await readConfig(env.DOWNLOADS), 200, CONFIG_CACHE_CONTROL);
  } catch {
    response = jsonResponse({ code: ErrorCode.Unavailable }, 503, NO_CACHE);
  }
  return request.method === "HEAD" ? new Response(null, { status: response.status, headers: response.headers }) : response;
}
