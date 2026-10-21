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
