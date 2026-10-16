#!/usr/bin/env bash
# Asks the running app for a picture of what the player sees and copies it here. Usage: tools/capture.sh [LABEL] [TIMEOUT_SECONDS]
# Authorship: first draft by Qwen 3 4B (AI) after one fix round; stale-picture handling added by Claude (AI).
# Directed and tested by Jon Davis, who did not write it.
set -u

PACKAGE="com.boredoom.chorewars"
DEVICE_DIR="/sdcard/Android/data/$PACKAGE/files"

source "$(dirname "${BASH_SOURCE[0]}")/quest-device.sh"
select_quest || exit 1

LABEL="${1:-shot}"
TIMEOUT="${2:-60}"

LABEL=$(printf '%s' "$LABEL" | tr -cd 'A-Za-z0-9-')

# An older picture with the same label may already be there; only a name that was not there before counts.
BEFORE=$(adb shell ls $DEVICE_DIR/shots 2>/dev/null | tr -d '\r')

adb shell "echo $LABEL > $DEVICE_DIR/capture.request"

for ((i=1; i<=TIMEOUT; i++)); do
    NAME=$(adb shell ls $DEVICE_DIR/shots 2>/dev/null | tr -d '\r' | grep -F -- "$LABEL" | grep '\.png$' | grep -vxF -- "$BEFORE" | head -1)
    if [ -n "$NAME" ]; then
        adb pull $DEVICE_DIR/shots/$NAME .
        echo "saved ./$NAME"
        exit 0
    fi
    sleep 1
done

echo "timed out waiting for the capture (is the headset on?)" >&2
exit 1
