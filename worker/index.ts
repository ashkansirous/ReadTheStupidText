// Serves the landing sites in ./site: Read The Stupid Text at the root of
// `site/`, Dino Reader (Slice 74) in `site/dino/`. The Host header picks the
// site (resolveSite, in downloads.ts so node can test it); an unknown host gets
// the Read The Stupid Text site. On *.workers.dev (the pre-cut-over check) the
// Dino site is reached under a `/dino/` prefix. The free story store API
// (/api/stories, worker/stories.ts, Slice 92) and its remote config (/api/app-config,
// worker/appconfig.ts, Slice 132) and its anonymous usage counts (/api/events, worker/events.ts, Slice 133) are Dino Reader's;
// Read The Stupid Text answers those two as well since Slice 137 (its own config object and dataset).
// Static files are answered by the assets layer without invoking this Worker;
// only the paths listed in wrangler.jsonc's `assets.run_worker_first` come
// here, to be mapped onto a file and to get the security headers below. `Env`
// is generated into worker-configuration.d.ts by `npm run types`; rerun it
// after editing wrangler.jsonc.

import { DINO_PATH_PREFIX, Product, assetPageFor, canonicalRedirect, handleDownload, isDownloadPath, isHiddenDinoPath, resolveSite, serveObject } from "./downloads";
import type { ResolvedSite } from "./downloads";
import { APP_CONFIG_KEY, RTST_APP_CONFIG_KEY, handleAppConfig, isAppConfigPath, parseAppConfig } from "./appconfig";
import { handleCrash, isCrashPath } from "./crashes";
import type { CrashesEnv } from "./crashes";
import { handleEvents, handleRtstEvents, isEventsPath } from "./events";
import type { EventsEnv, RtstEventsEnv } from "./events";
import { handleStories, isStoryPath } from "./stories";
import { handleSonos, isSonosPath } from "./sonos";
import { handleAlexa, isAlexaPath } from "./alexa";
import type { AlexaEnv } from "./alexa";
import { handleStats, isStatsApiPath, isStatsPath } from "./stats";
import { runRollup } from "./stats-rollup";
import type { StatsEnv } from "./stats-rollup";

const SECURITY_HEADERS: Readonly<Record<string, string>> = {
  "X-Content-Type-Options": "nosniff",
  "Referrer-Policy": "strict-origin-when-cross-origin",
  "X-Frame-Options": "DENY",
};

function withSecurityHeaders(response: Response): Response {
  const secured = new Response(response.body, response);
  for (const [name, value] of Object.entries(SECURITY_HEADERS)) {
    secured.headers.set(name, value);
  }
  return secured;
}

function withPath(request: Request, url: URL, pathname: string): Request {
  if (url.pathname === pathname) return request;
  const target = new URL(url);
  target.pathname = pathname;
  return new Request(target, request);
}

function serveStories(request: Request, url: URL, env: Env, site: ResolvedSite): Promise<Response> | Response {
  if (site.product !== Product.DinoReader) return new Response("Not found", { status: 404 });
  const pathPrefix = site.home === "/" ? "" : DINO_PATH_PREFIX;
  return handleStories(withPath(request, url, site.path), env, pathPrefix, serveObject);
}

// Slice 283: Dino Reader's anonymous Sonos music service (worker/sonos.ts).
// Slice 289: each play is counted in `dino_usage` under the apps' own telemetry switch (declared further down: function declarations are hoisted).
function serveSonos(request: Request, url: URL, env: Env & EventsEnv, site: ResolvedSite, ctx: ExecutionContext): Promise<Response> | Response {
  if (site.product !== Product.DinoReader) return new Response("Not found", { status: 404 });
  const pathPrefix = site.home === "/" ? "" : DINO_PATH_PREFIX;
  // ctx.waitUntil is called as a method (destructuring it throws "Illegal invocation"); the play is counted after the answer is sent.
  const usage = { dataset: env.DINO_USAGE, telemetryEnabled: telemetryCheck(env, APP_CONFIG_KEY), defer: (work: Promise<unknown>) => ctx.waitUntil(work) };
  return handleSonos(withPath(request, url, site.path), env, pathPrefix, serveObject, usage);
}

// Slice 282: Dino Reader's anonymous Alexa kid skill (worker/alexa.ts); each segment played is counted like a Sonos play.
function serveAlexa(request: Request, url: URL, env: Env & EventsEnv & AlexaEnv, site: ResolvedSite, ctx: ExecutionContext): Promise<Response> | Response {
  if (site.product !== Product.DinoReader) return new Response("Not found", { status: 404 });
  const pathPrefix = site.home === "/" ? "" : DINO_PATH_PREFIX;
  const usage = { dataset: env.DINO_USAGE, telemetryEnabled: telemetryCheck(env, APP_CONFIG_KEY), defer: (work: Promise<unknown>) => ctx.waitUntil(work) };
  return handleAlexa(withPath(request, url, site.path), env, pathPrefix, serveObject, usage);
}

// Each product has its own config object in the downloads bucket (Slice 137 added Read The Stupid Text's).
function configKeyFor(site: ResolvedSite): string {
  return site.product === Product.DinoReader ? APP_CONFIG_KEY : RTST_APP_CONFIG_KEY;
}

function serveAppConfig(request: Request, env: Env, site: ResolvedSite): Promise<Response> | Response {
  return handleAppConfig(request, env, configKeyFor(site));
}

// Slice 133: a missing config means telemetry is off, so counts are dropped. An R2 read error is not "off": it throws, handleEvents
// answers 503 (the app retries later) and nothing is cached. The answer is kept briefly per isolate so a flood of requests does not
// turn into a flood of R2 reads.
const TELEMETRY_CACHE_MS = 60_000;
const telemetryCache = new Map<string, { readonly enabled: boolean; readonly at: number }>();

// A switch of the config object, read through the same short cache (Slice 227 adds the crash-report switch next to the telemetry one).
type ConfigSwitch = (config: ReturnType<typeof parseAppConfig>) => boolean;

async function readConfigFlag(env: Env, key: string, pick: ConfigSwitch): Promise<boolean> {
  const object = await env.DOWNLOADS.get(key);
  return object ? pick(parseAppConfig(await object.json())) : false;
}

function configCheck(env: Env, key: string, name: string, pick: ConfigSwitch): () => Promise<boolean> {
  const cacheKey = `${key}#${name}`;
  return async () => {
    const now = Date.now();
    const cached = telemetryCache.get(cacheKey);
    if (cached !== undefined && now - cached.at < TELEMETRY_CACHE_MS) return cached.enabled;
    const enabled = await readConfigFlag(env, key, pick);
    telemetryCache.set(cacheKey, { enabled, at: now });
    return enabled;
  };
}

function telemetryCheck(env: Env, key: string): () => Promise<boolean> {
  return configCheck(env, key, "telemetry", (config) => config.telemetry.enabled);
}

// Slice 227: crash and error reports from the phone apps, their own switch (`crashReports.enabled`, default off), bucket folder and dataset (APP_CRASHES, dataset
// app_crashes). The app is the product whose host the report arrived on.
function serveCrash(request: Request, env: Env & CrashesEnv, site: ResolvedSite): Promise<Response> {
  const check = configCheck(env, configKeyFor(site), "crashReports", (config) => config.crashReports.enabled);
  return handleCrash(request, env, check, site.product === Product.DinoReader ? "dino" : "rtst");
}

// Slice 137: Read The Stupid Text's phone counts take the same road with their own config object and dataset (RTST_USAGE, dataset
// rtst_usage).
function serveEvents(request: Request, env: Env & EventsEnv & RtstEventsEnv, site: ResolvedSite): Promise<Response> | Response {
  const check = telemetryCheck(env, configKeyFor(site));
  return site.product === Product.DinoReader ? handleEvents(request, env, check) : handleRtstEvents(request, env, check);
}

// Slice 287: the public Dino usage dashboard: the page (site/dino/stats.html, served by the page mapping) and its read-only data from D1 (worker/stats.ts).
// Returns null for the page path on the Dino site so the asset layer serves it.
function serveStats(request: Request, url: URL, env: StatsEnv, site: ResolvedSite): Promise<Response> | Response | null {
  if (site.product !== Product.DinoReader) return new Response("Not found", { status: 404 });
  return isStatsApiPath(site.path) ? handleStats(withPath(request, url, site.path), env) : null;
}

async function serveAsset(request: Request, env: Env & StatsEnv, ctx: ExecutionContext): Promise<Response> {
  const url = new URL(request.url);
  if (isHiddenDinoPath(url.hostname, url.pathname)) return new Response("Not found", { status: 404 });
  const redirect = canonicalRedirect(url.hostname, url.pathname, url.search);
  if (redirect !== null) return Response.redirect(new URL(redirect, url).toString(), 302);
  const site = resolveSite(url.hostname, url.pathname);
  if (isStoryPath(site.path)) return serveStories(request, url, env, site);
  if (isSonosPath(site.path)) return serveSonos(request, url, env, site, ctx);
  if (isAlexaPath(site.path)) return serveAlexa(request, url, env, site, ctx);
  if (isStatsPath(site.path)) {
    const answer = serveStats(request, url, env, site);
    if (answer !== null) return answer;
  }
  if (isAppConfigPath(site.path)) return serveAppConfig(request, env, site);
  if (isEventsPath(site.path)) return serveEvents(request, env, site);
  if (isCrashPath(site.path)) return serveCrash(request, env, site);
  if (isDownloadPath(site.path)) {
    return handleDownload(withPath(request, url, site.path), site.home);
  }
  const page = assetPageFor(site);
  return env.ASSETS.fetch(page === null ? request : withPath(request, url, page));
}

export default {
  async fetch(request: Request, env: Env & StatsEnv, ctx: ExecutionContext): Promise<Response> {
    return withSecurityHeaders(await serveAsset(request, env, ctx));
  },
  // Slice 287: the hourly cron (wrangler "triggers") rolls Analytics Engine into D1; it logs and returns when the STATS binding or token is absent.
  async scheduled(_controller: ScheduledController, env: Env & StatsEnv, ctx: ExecutionContext): Promise<void> {
    ctx.waitUntil(runRollup(env));
  },
} satisfies ExportedHandler<Env & StatsEnv>;
