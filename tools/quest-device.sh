#!/usr/bin/env bash
# Picks the Quest 3 out of whatever adb can see and points adb at it (ANDROID_SERIAL), so other
# attached devices (a phone, say) are left alone. Source this file, then call select_quest.
# A Quest is a device in the "device" state whose model is Quest_* or whose product is eureka.
# The same headset can appear twice (USB and Wi-Fi); the USB entry wins. Two different headsets is an error.
# Written by: Claude (AI). Passenger: Jon Davis (ideas man; directed and tested, did not write this code).

select_quest() {
    local line serial usb="" wifi="" count=0
    while IFS= read -r line; do
        [[ "$line" =~ [[:space:]]device[[:space:]] ]] || continue
        [[ "$line" == *"model:Quest"* || "$line" == *"product:eureka"* ]] || continue
        serial="${line%%[[:space:]]*}"
        count=$((count + 1))
        if [[ "$serial" == *:* ]]; then wifi="$serial"; else usb="$serial"; fi
    done < <(adb devices -l | tail -n +2)

    if [ "$count" -eq 0 ]; then
        echo "no Quest found (adb sees: $(adb devices | tail -n +2 | grep -c .) other device(s))" >&2
        return 1
    fi
    if [ "$count" -gt 2 ] || { [ "$count" -eq 2 ] && { [ -z "$usb" ] || [ -z "$wifi" ]; }; }; then
        echo "more than one Quest attached; unplug the extra headset" >&2
        return 1
    fi
    export ANDROID_SERIAL="${usb:-$wifi}"
    echo "using Quest $ANDROID_SERIAL" >&2
}
