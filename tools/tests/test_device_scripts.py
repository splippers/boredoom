#!/usr/bin/env python3
"""Tests for tools/redeploy.sh, capture.sh and pull-telemetry.sh against a fake adb: they must find the Quest among
whatever is attached and never send a command to anything else. Run: python3 tools/tests/test_device_scripts.py

Written by: Claude (AI). Passenger: Jon Davis (ideas man; directed and tested, did not write this code).
"""
import os
import subprocess
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
MOCK = os.path.join(HERE, "mockbin")
PKG = "com.boredoom.chorewars"

QUEST = "2G0YC1ZFB408XG|device|usb:1-2 product:eureka model:Quest_3 device:eureka"
QUEST_WIFI = "192.168.1.230:5555|device|product:eureka model:Quest_3 device:eureka"
OTHER_QUEST = "1WMHH000000000|device|usb:1-3 product:eureka model:Quest_3 device:eureka"
PHONE = "R58RC2233SA|device|usb:1-5.1.4.2.2 product:a12snseea model:SM_A127F device:a12s"
PHONE_UNAUTH = "R58RC2233SA|unauthorized|usb:1-5.1.4.2.2 transport_id:11"


def run(script, args=(), devices=(), extra_env=None, files=()):
    work = tempfile.mkdtemp(prefix="devtest_")
    for f in files:
        os.makedirs(os.path.dirname(os.path.join(work, f)) or work, exist_ok=True)
        open(os.path.join(work, f), "w").write("apk")
    log = os.path.join(work, "adb.log")
    open(log, "w").close()
    env = dict(os.environ, PATH=MOCK + ":" + os.environ["PATH"], MOCK_DEVICES=";".join(devices), MOCK_LOG=log,
               MOCK_STATE=os.path.join(work, "state"), **(extra_env or {}))
    env.pop("ANDROID_SERIAL", None)
    p = subprocess.run(["bash", os.path.join(TOOLS, script)] + list(args), cwd=work, env=env, capture_output=True, text=True, timeout=40)
    calls = [l.strip() for l in open(log) if " adb " in l]
    return dict(rc=p.returncode, out=p.stdout, err=p.stderr, calls=calls, work=work)


def acted(calls):
    """Calls that do something to a device, as opposed to listing them."""
    return [c for c in calls if " adb devices" not in c]


class DeviceSelection(unittest.TestCase):
    SCRIPTS = [("redeploy.sh", ["x.apk"], ["x.apk"]), ("pull-telemetry.sh", ["out"], []), ("capture.sh", ["menu", "3"], [])]

    def each(self, devices, check, env=None):
        for script, args, files in self.SCRIPTS:
            with self.subTest(script=script):
                check(run(script, args, devices, env, files))

    def test_no_device_does_nothing(self):
        def check(r):
            self.assertNotEqual(r["rc"], 0)
            self.assertEqual(acted(r["calls"]), [])
            self.assertIn("no Quest found", r["err"])
        self.each([], check)

    def test_only_a_phone_does_nothing(self):
        def check(r):
            self.assertNotEqual(r["rc"], 0)
            self.assertEqual(acted(r["calls"]), [])
        self.each([PHONE], check)

    def test_unauthorised_phone_is_not_a_quest(self):
        def check(r):
            self.assertNotEqual(r["rc"], 0)
            self.assertEqual(acted(r["calls"]), [])
        self.each([PHONE_UNAUTH], check)

    def test_quest_alone_is_targeted_by_its_serial(self):
        def check(r):
            self.assertEqual(r["rc"], 0, r["err"])
            self.assertTrue(acted(r["calls"]))
            self.assertTrue(all(c.startswith("[2G0YC1ZFB408XG]") for c in acted(r["calls"])), r["calls"])
        self.each([QUEST], check, {"MOCK_SHOT_AFTER": "2", "MOCK_LABEL": "menu"})

    def test_phone_beside_the_quest_is_never_targeted(self):
        def check(r):
            self.assertEqual(r["rc"], 0, r["err"])
            self.assertTrue(acted(r["calls"]))
            self.assertFalse([c for c in r["calls"] if "R58RC2233SA" in c and " adb devices" not in c], r["calls"])
            self.assertTrue(all(c.startswith("[2G0YC1ZFB408XG]") for c in acted(r["calls"])), r["calls"])
        for order in ([QUEST, PHONE], [PHONE, QUEST], [PHONE_UNAUTH, QUEST]):
            self.each(order, check, {"MOCK_SHOT_AFTER": "2", "MOCK_LABEL": "menu"})

    def test_same_headset_on_usb_and_wifi_uses_usb(self):
        def check(r):
            self.assertEqual(r["rc"], 0, r["err"])
            self.assertTrue(all(c.startswith("[2G0YC1ZFB408XG]") for c in acted(r["calls"])), r["calls"])
        self.each([QUEST_WIFI, QUEST], check, {"MOCK_SHOT_AFTER": "2", "MOCK_LABEL": "menu"})

    def test_wifi_alone_works(self):
        def check(r):
            self.assertEqual(r["rc"], 0, r["err"])
            self.assertTrue(all(c.startswith("[192.168.1.230:5555]") for c in acted(r["calls"])), r["calls"])
        self.each([QUEST_WIFI, PHONE], check, {"MOCK_SHOT_AFTER": "2", "MOCK_LABEL": "menu"})

    def test_two_different_headsets_refuses(self):
        def check(r):
            self.assertNotEqual(r["rc"], 0)
            self.assertEqual(acted(r["calls"]), [])
            self.assertIn("more than one Quest", r["err"])
        self.each([QUEST, OTHER_QUEST], check)


class WhatTheScriptsDo(unittest.TestCase):
    def test_redeploy_installs_then_launches_and_reports_missing_apk(self):
        r = run("redeploy.sh", ["my.apk"], [QUEST, PHONE], files=["my.apk"])
        ins = [i for i, x in enumerate(r["calls"]) if "install -r my.apk" in x]
        mon = [i for i, x in enumerate(r["calls"]) if f"monkey -p {PKG}" in x]
        self.assertTrue(ins and mon and ins[0] < mon[0], r["calls"])
        self.assertIn("installed and launched", r["out"])
        missing = run("redeploy.sh", ["nope.apk"], [QUEST])
        self.assertEqual(missing["rc"], 1)
        self.assertIn("apk not found: nope.apk", missing["err"])
        self.assertEqual(missing["calls"], [])

    def test_pull_telemetry_copies_log_and_survives_missing_shots(self):
        r = run("pull-telemetry.sh", ["out"], [QUEST, PHONE])
        self.assertEqual(r["rc"], 0, r["err"])
        self.assertTrue(os.path.exists(os.path.join(r["work"], "out", "MQ3.log")))
        self.assertIn("pulled to out", r["out"])
        r = run("pull-telemetry.sh", ["out"], [QUEST], {"MOCK_PULL_FAIL_MATCH": "shots"})
        self.assertEqual(r["rc"], 0)
        r = run("pull-telemetry.sh", ["out"], [QUEST], {"MOCK_PULL_FAIL_MATCH": "MQ3.log"})
        self.assertEqual(r["rc"], 1)
        self.assertIn("could not pull MQ3.log", r["err"])

    def test_capture_asks_waits_ignores_stale_and_cleans_the_label(self):
        r = run("capture.sh", ["menu"], [QUEST, PHONE], {"MOCK_SHOT_AFTER": "2", "MOCK_LABEL": "menu"})
        self.assertEqual(r["rc"], 0, r["err"])
        self.assertTrue(any("capture.request" in x and "menu" in x for x in r["calls"]))
        self.assertIn("saved ./shot_120000_menu.png", r["out"])
        r = run("capture.sh", ["menu"], [QUEST], {"MOCK_SHOT_AFTER": "2", "MOCK_LABEL": "menu", "MOCK_STALE": "1"})
        self.assertFalse(any("shot_110000" in x and " pull" in x for x in r["calls"]))
        r = run("capture.sh", ["menu", "3"], [QUEST], {"MOCK_SHOT_AFTER": "never"})
        self.assertEqual(r["rc"], 1)
        self.assertIn("timed out waiting for the capture", r["err"])
        r = run("capture.sh", ["a b;rm -rf x", "5"], [QUEST], {"MOCK_SHOT_AFTER": "2", "MOCK_LABEL": "abrm-rfx"})
        req = [x for x in r["calls"] if "capture.request" in x]
        self.assertTrue(req and ";" not in req[0] and "a b" not in req[0], req)


if __name__ == "__main__":
    unittest.main(verbosity=1)
