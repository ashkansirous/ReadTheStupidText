// The R2 key layout for the Android downloads (Slice 69, Dino Reader added in
// Slice 74): the one place both the Worker (worker/downloads.ts) and the CI
// publisher (.github/scripts/publish-downloads.mjs) get their keys and file
// names from. Plain JS with a .d.mts beside it so node, tsc and wrangler's
// bundler all read it. One bucket, one key prefix per product:
//   Read The Stupid Text
//     android/<version>/ReadTheStupidText-<version>.apk   the APK
//     manifests/android/<version>.json                    uploaded last
//   Dino Reader
//     dinoreader/android/<version>/DinoReader-<version>.apk
//     dinoreader/manifests/android/<version>.json         uploaded last
//     dinoreader/stories/<id>/meta.json                   a free story (Slice 92), uploaded last
//     dinoreader/stories/<id>/cover.jpg
//     dinoreader/stories/<id>/story-v<n>.dino             the .dino package, version n

export const APK_PREFIX = "android/";
export const MANIFEST_PREFIX = "manifests/android/";
export const MANIFEST_SUFFIX = ".json";

export const Product = {
  ReadTheStupidText: "readthestupidtext",
  DinoReader: "dinoreader",
};

const PRODUCT_LAYOUT = {
  [Product.ReadTheStupidText]: { keyPrefix: "", fileStem: "ReadTheStupidText" },
  [Product.DinoReader]: { keyPrefix: "dinoreader/", fileStem: "DinoReader" },
};

function layoutFor(product) {
  const layout = PRODUCT_LAYOUT[product];
  if (!layout) throw new Error(`Unknown product "${product}".`);
  return layout;
}

export function apkFileName(version, product = Product.ReadTheStupidText) {
  return `${layoutFor(product).fileStem}-${version}.apk`;
}

export function apkKey(version, product = Product.ReadTheStupidText) {
  return `${layoutFor(product).keyPrefix}${APK_PREFIX}${version}/${apkFileName(version, product)}`;
}

/** The listing prefix of a product's manifests. */
export function manifestPrefix(product = Product.ReadTheStupidText) {
  return `${layoutFor(product).keyPrefix}${MANIFEST_PREFIX}`;
}

export function manifestKey(version, product = Product.ReadTheStupidText) {
  return `${manifestPrefix(product)}${version}${MANIFEST_SUFFIX}`;
}

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
