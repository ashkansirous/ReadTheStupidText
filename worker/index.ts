// Serves the landing site in ./site. Static files are answered by the assets
// layer without invoking this Worker; only the paths listed in wrangler.jsonc's
// `assets.run_worker_first` come here, to be mapped onto a file and to get the
// security headers below. `Env` is generated into worker-configuration.d.ts by
// `npm run types`; rerun it after editing wrangler.jsonc.

const PAGE_FOR_PATH: Readonly<Record<string, string>> = {
  "/": "/index.html",
  "/privacy": "/privacy.html",
  "/privacy/": "/privacy.html",
};

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

async function serveAsset(request: Request, env: Env): Promise<Response> {
  const url = new URL(request.url);
  const page = PAGE_FOR_PATH[url.pathname];
  if (page === undefined) {
    return env.ASSETS.fetch(request);
  }
  url.pathname = page;
  return env.ASSETS.fetch(new Request(url, request));
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    return withSecurityHeaders(await serveAsset(request, env));
  },
} satisfies ExportedHandler<Env>;
