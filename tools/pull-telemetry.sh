#!/usr/bin/env bash
# Copies the headset's MQ3.log (and any capture pictures) to this PC. Usage: tools/pull-telemetry.sh [OUTDIR]
# Authorship: written by Qwen 3 4B (AI) from a written spec, first try; reviewed by Claude (AI).
# Directed and tested by Jon Davis, who did not write it.
set -u

PACKAGE="com.boredoom.chorewars"
DEVICE_DIR="/sdcard/Android/data/$PACKAGE/files"

source "$(dirname "${BASH_SOURCE[0]}")/quest-device.sh"
select_quest || exit 1

OUT="${1:-telemetry-pull/$(date +%Y%m%d-%H%M%S)}"
mkdir -p "$OUT"

if ! adb pull "$DEVICE_DIR/MQ3.log" "$OUT/"; then
    echo "could not pull MQ3.log" >&2
    exit 1
fi

if ! adb pull "$DEVICE_DIR/shots" "$OUT/"; then
    echo "no shots on the device" >&2
fi

echo "pulled to $OUT"
exit 0
