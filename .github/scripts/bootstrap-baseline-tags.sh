#!/usr/bin/env bash
# Slice 130: make the per-product version tags self-bootstrapping. For each tag prefix given
# (e.g. rtst-v dino-v) with no <prefix><x.y.z> tag yet, tag the commit of the newest legacy shared
# v<x.y.z> tag reachable from HEAD as <prefix><x.y.z> and push it, so the product's first
# per-product version continues after the last shared one and never goes backwards.
# Fails only when a prefix has no tag and no legacy v<x.y.z> tag exists either.
# Usage: bootstrap-baseline-tags.sh <prefix>...   (run inside a full clone with tags)
set -euo pipefail

SEMVER_TAIL='[0-9]+\.[0-9]+\.[0-9]+'
REMOTE="${BASELINE_REMOTE:-origin}"

has_product_tag() {
  local tags
  tags=$(git tag --list "${1}[0-9]*")
  grep -Eq "^${1}${SEMVER_TAIL}\$" <<<"$tags"
}

newest_legacy_tag() {
  git tag --merged HEAD --list 'v[0-9]*' --sort=-v:refname | grep -E "^v${SEMVER_TAIL}\$" | head -n 1 || true
}

bootstrap_prefix() {
  local prefix="$1" legacy tag
  if has_product_tag "$prefix"; then
    echo "${prefix}: a baseline tag already exists."
    return 0
  fi
  legacy=$(newest_legacy_tag)
  if [ -z "$legacy" ]; then
    echo "::error::No ${prefix}<x.y.z> tag and no legacy v<x.y.z> tag to continue from."
    return 1
  fi
  tag="${prefix}${legacy#v}"
  git tag "$tag" "${legacy}^{commit}"
  git push "$REMOTE" "refs/tags/${tag}"
  echo "${prefix}: created baseline ${tag} at ${legacy}."
}

if [ "$#" -eq 0 ]; then
  echo "usage: bootstrap-baseline-tags.sh <prefix>..." >&2
  exit 2
fi
for prefix in "$@"; do
  bootstrap_prefix "$prefix"
done
