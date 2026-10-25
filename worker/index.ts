// Serves the landing sites in ./site: Read The Stupid Text at the root of
// `site/`, Dino Reader (Slice 74) in `site/dino/`. The Host header picks the
// site (resolveSite, in downloads.ts so node can test it); an unknown host gets
// the Read The Stupid Text site. On *.workers.dev (the pre-cut-over check) the
// Dino site is reached under a `/dino/` prefix. The free story store API
// (/api/stories, worker/stories.ts, Slice 92) and its remote config (/api/app-config,
// worker/appconfig.ts, Slice 132) are Dino Reader's only.
// Static files are answered by the assets layer without invoking this Worker;
// only the paths listed in wrangler.jsonc's `assets.run_worker_first` come
// here, to be mapped onto a file and to get the security headers below. `Env`
// is generated into worker-configuration.d.ts by `npm run types`; rerun it
// after editing wrangler.jsonc.

import { DINO_PATH_PREFIX, Product, assetPageFor, canonicalRedirect, handleDownload, isDownloadPath, isHiddenDinoPath, resolveSite, serveObject } from "./downloads";
import type { ResolvedSite } from "./downloads";
import { handleAppConfig, isAppConfigPath } from "./appconfig";
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

function serveAppConfig(request: Request, env: Env, site: ResolvedSite): Promise<Response> | Response {
  if (site.product !== Product.DinoReader) return new Response("Not found", { status: 404 });
  return handleAppConfig(request, env);
}

async function serveAsset(request: Request, env: Env): Promise<Response> {
  const url = new URL(request.url);
  if (isHiddenDinoPath(url.hostname, url.pathname)) return new Response("Not found", { status: 404 });
  const redirect = canonicalRedirect(url.hostname, url.pathname, url.search);
  if (redirect !== null) return Response.redirect(new URL(redirect, url).toString(), 302);
  const site = resolveSite(url.hostname, url.pathname);
  if (isStoryPath(site.path)) return serveStories(request, url, env, site);
  if (isAppConfigPath(site.path)) return serveAppConfig(request, env, site);
  if (isDownloadPath(site.path)) {
    return handleDownload(withPath(request, url, site.path), env, site.product, site.home);
  }
  const page = assetPageFor(site);
  return env.ASSETS.fetch(page === null ? request : withPath(request, url, page));
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    return withSecurityHeaders(await serveAsset(request, env));
  },
} satisfies ExportedHandler<Env>;
