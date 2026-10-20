// The R2 key layout for the Android downloads (Slice 69): the one place both
// the Worker (worker/downloads.ts) and the CI publisher
// (.github/scripts/publish-downloads.mjs) get their keys and file names from.
// Plain JS with a .d.mts beside it so node, tsc and wrangler's bundler all read it.
//   android/<version>/ReadTheStupidText-<version>.apk   the APK
//   manifests/android/<version>.json                    uploaded last

export const APK_PREFIX = "android/";
export const MANIFEST_PREFIX = "manifests/android/";
export const MANIFEST_SUFFIX = ".json";

export function apkFileName(version) {
  return `ReadTheStupidText-${version}.apk`;
}

export function apkKey(version) {
  return `${APK_PREFIX}${version}/${apkFileName(version)}`;
}

export function manifestKey(version) {
  return `${MANIFEST_PREFIX}${version}${MANIFEST_SUFFIX}`;
}
