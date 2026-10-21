// Pure helpers shared by check-configs.mjs and its tests.

// Removes // and /* */ comments and trailing commas, leaving string contents alone.
export function stripJsonc(text) {
  let out = "";
  let i = 0;
  while (i < text.length) {
    const c = text[i];
    const next = text[i + 1];
    if (c === '"') {
      const start = i++;
      while (i < text.length && text[i] !== '"') i += text[i] === "\\" ? 2 : 1;
      out += text.slice(start, ++i);
    } else if (c === "/" && next === "/") {
      while (i < text.length && text[i] !== "\n") i++;
    } else if (c === "/" && next === "*") {
      const end = text.indexOf("*/", i + 2);
      i = end === -1 ? text.length : end + 2;
    } else {
      out += c;
      i++;
    }
  }
  return out.replace(/,(\s*[}\]])/g, "$1");
}

export const runFirst = (config) => config.assets?.run_worker_first ?? [];

// Only trailing-* globs are modelled (text before * is a literal prefix); mid-path
// globs, a bare "*" and "!" exclusion rules are not, so wrangler deploy is the
// backstop for those.
const isCoveredBy = (glob, path) => glob !== path && glob.endsWith("*") && path.startsWith(glob.slice(0, -1));

// Wrangler refuses a run_worker_first entry that a glob entry already covers
// ("rule '/download/*' makes it redundant").
export function redundantPaths(config) {
  const entries = runFirst(config).filter((entry) => !entry.startsWith("!"));
  return entries.filter((path) => entries.some((glob) => isCoveredBy(glob, path)));
}
