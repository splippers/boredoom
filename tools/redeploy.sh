#!/usr/bin/env bash
# Installs the APK on the headset and launches it. Usage: tools/redeploy.sh [APK] (default Builds/BoreDOOM-Quest.apk)
# Authorship: written by Qwen 3 4B (AI) from a written spec, first try; reviewed by Claude (AI).
# Directed and tested by Jon Davis, who did not write it.
set -u

PACKAGE="com.boredoom.chorewars"
DEVICE_DIR="/sdcard/Android/data/$PACKAGE/files"

APK="${1:-Builds/BoreDOOM-Quest.apk}"

if [ ! -f "$APK" ]; then
    echo "apk not found: $APK" >&2
    exit 1
fi

source "$(dirname "${BASH_SOURCE[0]}")/quest-device.sh"
select_quest || exit 1

adb install -r "$APK"
if [ $? -ne  0 ]; then
    exit 1
fi

adb shell monkey -p $PACKAGE -c android.intent.category.LAUNCHER 1

echo "installed and launched"
exit 0
