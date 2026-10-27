// Shared constants and the R2 key layout of the Dino story store (Slice 92): the one place the
// Worker (downloads.ts, stories.ts, appconfig.ts, events.ts) and worker/check-configs.mjs get them
// from. Plain JS with a .d.mts beside it so node, tsc and wrangler's bundler all read it.
// (The APK/manifest keys went when /download became a redirect to #platforms.)
//   dinoreader/stories/<id>/meta.json                   a free story (Slice 92), uploaded last
//   dinoreader/stories/<id>/cover.jpg
//   dinoreader/stories/<id>/story-v<n>.dino             the .dino package, version n

export const Product = {
  ReadTheStupidText: "readthestupidtext",
  DinoReader: "dinoreader",
};

// The two sites' hosts and the Dino Reader path prefix on *.workers.dev (Slice 74).
// Shared by the Worker (downloads.ts) and worker/check-configs.mjs.
export const Host = {
  ReadTheStupidText: "readthestupidtext.sirous.uk",
  DinoReader: "dinoreader.sirous.uk",
};

export const DINO_PATH_PREFIX = "/dino";

// Slice 92: the free Dino story store, uploaded by hand. A folder counts only when its
// meta.json and a package are both there (meta.json goes up last, like an APK manifest).
export const STORIES_PREFIX = "dinoreader/stories/";
export const STORY_META_FILE = "meta.json";
export const STORY_COVER_FILE = "cover.jpg";
export const STORY_PACKAGE_PREFIX = "story-v";
export const STORY_PACKAGE_SUFFIX = ".dino";

/** A story id is the package id: a GUID in "N" form (32 hex digits), the same shape the app accepts. */
export const STORY_ID_PATTERN = /^[0-9a-f]{32}$/;

export function storyFolder(id) {
  return `${STORIES_PREFIX}${id}/`;
}

export function storyMetaKey(id) {
  return `${storyFolder(id)}${STORY_META_FILE}`;
}

export function storyCoverKey(id) {
  return `${storyFolder(id)}${STORY_COVER_FILE}`;
}

export function storyPackageFileName(version) {
  return `${STORY_PACKAGE_PREFIX}${version}${STORY_PACKAGE_SUFFIX}`;
}

export function storyPackageKey(id, version) {
  return `${storyFolder(id)}${storyPackageFileName(version)}`;
}

// Shared by downloads.ts and stories.ts (stories.ts can import values only from this file; see its header).
export const JSON_CONTENT_TYPE = "application/json; charset=utf-8";
export const NO_CACHE = "no-cache";
export const VERSIONED_CACHE = "public, max-age=86400";
export const LISTING_CACHE_MS = 60_000;
export const LIST_PAGE_SIZE = 1000;
export const MAX_LIST_PAGES = 20;
