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

async function serveAsset(request: Request, env: Env): Promise<Response> {
  const url = new URL(request.url);
  if (isHiddenDinoPath(url.hostname, url.pathname)) return new Response("Not found", { status: 404 });
  const redirect = canonicalRedirect(url.hostname, url.pathname, url.search);
  if (redirect !== null) return Response.redirect(new URL(redirect, url).toString(), 302);
  const site = resolveSite(url.hostname, url.pathname);
  if (isStoryPath(site.path)) return serveStories(request, url, env, site);
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
  async fetch(request: Request, env: Env): Promise<Response> {
    return withSecurityHeaders(await serveAsset(request, env));
  },
} satisfies ExportedHandler<Env>;
