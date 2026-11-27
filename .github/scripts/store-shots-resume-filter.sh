#!/usr/bin/env bash
# Slice 433 (iOS run 45491086879, finding 2): keeps the shots and clips of an earlier run's draft that are still valid on this commit, deletes the rest so the script retakes them.
# Called by store-screenshots.yml's resume step after it downloaded the draft of a resume_run on ANOTHER commit into <dir>.
#
# Usage: store-shots-resume-filter.sh <source-sha> <app> <device> <dir>
# A file is kept only when the app code (src/) AND every flow that produced it are unchanged between <source-sha> and HEAD:
#   a PNG store-<app>-<device>-<n>-<screen>.png      <- the flow whose takeScreenshot names store-<app>-ios-<n>-<screen>
#   an MP4 store-<app>-<device>-video-<clip>.mp4     <- store-shot-ios-video-<clip>.yaml, store-shot-ios-videoprep-<clip>.yaml (when either side has it) and the shelf set-up store-shot-ios-shelfprep.yaml
# A change of this script, the workflow or the pre-flow alone invalidates nothing. Prints one line per file: kept or dropped, with the reason. Exit 0 always (a file it cannot judge is dropped).
set -uo pipefail

SRC_SHA="${1:?source sha}"
APP="${2:?app}"
DEVICE="${3:?device}"
DIR="${4:?folder with the downloaded files}"
FLOW_DIR=".maestro/$APP"
CODE_CHANGED=no

# True when the path is identical in both commits (a path missing on both sides is identical; missing on one side is a change).
unchanged() {
  git diff --quiet "$SRC_SHA" HEAD -- "$@" 2>/dev/null
}

# The flow file (path) that takes the shot of this name at HEAD, or nothing.
flow_of_shot() {
  grep -lE "takeScreenshot: $1\$" "$FLOW_DIR"/store-shot-ios-*.yaml 2>/dev/null | head -n 1
}

# The flows an MP4 depends on, one per line (the clip's flow, its set-up flow, the shelf set-up).
flows_of_clip() {
  echo "$FLOW_DIR/store-shot-ios-video-$1.yaml"
  echo "$FLOW_DIR/store-shot-ios-videoprep-$1.yaml"
  echo "$FLOW_DIR/store-shot-ios-shelfprep.yaml"
}

report() {
  echo "resume: $1 $2 ($3)"
}

drop() {
  rm -f "$1"
  # A clip's .mark goes with its mp4: a mark with no recording, or one from another recording, would misjudge the retake.
  case "$1" in *.mp4) rm -f "${1%.mp4}.mark" ;; esac
  report "dropped" "$(basename "$1")" "$2"
}

judge_png() {
  local file="$1" base name flow
  base="$(basename "$file" .png)"
  name="store-$APP-ios-${base#store-$APP-$DEVICE-}"
  flow="$(flow_of_shot "$name")"
  if [ -z "$flow" ]; then drop "$file" "no flow at this commit takes $name"; return; fi
  if unchanged "$flow"; then report kept "$(basename "$file")" "flow $(basename "$flow") and src/ unchanged"; else drop "$file" "flow $(basename "$flow") changed"; fi
}

judge_mp4() {
  local file="$1" base clip flow changed=""
  base="$(basename "$file" .mp4)"
  clip="${base#store-$APP-$DEVICE-video-}"
  while IFS= read -r flow; do
    unchanged "$flow" || changed="$changed $(basename "$flow")"
  done < <(flows_of_clip "$clip")
  if [ -n "$changed" ]; then drop "$file" "flows changed:$changed"; else report kept "$(basename "$file")" "its flows and src/ unchanged"; fi
}

# The source commit must be in the checkout (the checkout is shallow, so it is fetched here: a depth-1 fetch gives its tree, which is all a diff needs); without it nothing can be judged and every file goes.
git cat-file -e "$SRC_SHA^{commit}" 2>/dev/null || git fetch --no-tags --depth=1 origin "$SRC_SHA" >/dev/null 2>&1 || true
if ! git cat-file -e "$SRC_SHA^{commit}" 2>/dev/null; then
  echo "::warning::resume: commit $SRC_SHA is not available, so no earlier file can be judged; all are dropped"
  rm -f "$DIR"/*.png "$DIR"/*.mp4 "$DIR"/*.mark
  exit 0
fi
unchanged src/ || CODE_CHANGED=yes
for file in "$DIR"/*.png "$DIR"/*.mp4; do
  [ -f "$file" ] || continue
  if [ "$CODE_CHANGED" = yes ]; then drop "$file" "src/ changed since $SRC_SHA"; continue; fi
  case "$file" in
    *.png) judge_png "$file" ;;
    *.mp4) judge_mp4 "$file" ;;
  esac
done
# An orphan mark (its mp4 was never uploaded or was dropped above) is deleted, so it is never uploaded again.
for file in "$DIR"/*.mark; do
  [ -f "$file" ] || continue
  [ -f "${file%.mark}.mp4" ] || { rm -f "$file"; report dropped "$(basename "$file")" "no recording beside it"; }
done
exit 0
