#!/usr/bin/env python3
import os
import subprocess
import sys
import tempfile
import unittest


SCRIPT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "summarise-telemetry.py")


class TestSummariseTelemetry(unittest.TestCase):
    def run_script(self, log_content):
        with tempfile.NamedTemporaryFile("w", encoding="utf-8", delete=False, suffix=".log") as f:
            f.write(log_content)
            path = f.name
        try:
            proc = subprocess.run(
                [sys.executable, SCRIPT, path],
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
            )
            return proc.returncode, proc.stdout, proc.stderr
        finally:
            os.unlink(path)

    def test_one_run(self):
        log = (
            '{"ts":"2025-10-15T12:00:00.000000Z","sub":"sys","sev":"INFO","msg":"BeforeSceneLoad"}\n'
            '{"ts":"2025-10-15T12:00:05.000000Z","sub":"sys","sev":"INFO","msg":"verdicts: scan OK, camera BROKEN (18 of 30 fps), coverage OK"}\n'
        )
        rc, out, err = self.run_script(log)
        self.assertEqual(rc, 0)
        self.assertIn("## Run 1", out)
        self.assertIn("Start:", out)
        self.assertIn("End:", out)
        self.assertIn("Duration:", out)
        self.assertIn("camera", out)
        self.assertIn("BROKEN", out)
        self.assertIn("18 of 30 fps", out)
        self.assertIn("Skipped 0 unreadable line(s).", out)

    def test_two_runs(self):
        log = (
            '{"ts":"2025-10-15T12:00:00.000000Z","sub":"sys","sev":"INFO","msg":"BeforeSceneLoad"}\n'
            '{"ts":"2025-10-15T12:00:01.000000Z","sub":"sys","sev":"INFO","msg":"verdicts: scan OK"}\n'
            '{"ts":"2025-10-15T12:01:00.000000Z","sub":"sys","sev":"INFO","msg":"BeforeSceneLoad"}\n'
            '{"ts":"2025-10-15T12:01:02.000000Z","sub":"sys","sev":"INFO","msg":"verdicts: scan BROKEN"}\n'
        )
        rc, out, err = self.run_script(log)
        self.assertEqual(rc, 0)
        self.assertIn("## Run 1", out)
        self.assertIn("## Run 2", out)
        self.assertIn("Skipped 0 unreadable line(s).", out)

    def test_broken_and_way_off_worst_offenders(self):
        log = (
            '{"ts":"2025-10-15T12:00:00.000000Z","sub":"sys","sev":"INFO","msg":"BeforeSceneLoad"}\n'
            '{"ts":"2025-10-15T12:00:01.000000Z","sub":"sys","sev":"INFO","msg":"verdicts: scan OK, camera BROKEN (fps low), audio WAY_OFF (no sound), net OK"}\n'
            '{"ts":"2025-10-15T12:00:02.000000Z","sub":"sys","sev":"INFO","msg":"verdicts: camera OK, audio BROKEN (intermittent)"}\n'
        )
        rc, out, err = self.run_script(log)
        self.assertEqual(rc, 0)
        self.assertIn("Worst offenders", out)
        # WAY_OFF has severity 2 > BROKEN 1, so it should be listed with worst severity
        self.assertIn("audio", out)
        self.assertIn("WAY_OFF", out)
        self.assertIn("BROKEN", out)
        self.assertIn("camera", out)
        # audio appears in multiple verdict lines (WAY_OFF once, BROKEN once later) - bad_lines count should reflect verdict lines where verdict != OK
        self.assertIn("(2 verdict lines)", out)

    def test_all_ok(self):
        log = (
            '{"ts":"2025-10-15T12:00:00.000000Z","sub":"sys","sev":"INFO","msg":"BeforeSceneLoad"}\n'
            '{"ts":"2025-10-15T12:00:01.000000Z","sub":"sys","sev":"INFO","msg":"verdicts: scan OK, camera OK, audio OK"}\n'
        )
        rc, out, err = self.run_script(log)
        self.assertEqual(rc, 0)
        self.assertIn("Worst offenders", out)
        self.assertIn("None.", out)

    def test_unreadable_line_counted(self):
        log = (
            '{"ts":"2025-10-15T12:00:00.000000Z","sub":"sys","sev":"INFO","msg":"BeforeSceneLoad"}\n'
            'not json\n'
            '{"ts":"2025-10-15T12:00:01.000000Z","sub":"sys","sev":"INFO","msg":"verdicts: scan OK"}\n'
            '{broken json\n'
        )
        rc, out, err = self.run_script(log)
        self.assertEqual(rc, 0)
        self.assertIn("Skipped 2 unreadable line(s).", out)

    def test_empty_file(self):
        rc, out, err = self.run_script("")
        self.assertEqual(rc, 0)
        self.assertIn("Skipped 0 unreadable line(s).", out)

    def test_missing_file_exit_code(self):
        with tempfile.NamedTemporaryFile(delete=True) as f:
            missing = f.name
        # file is deleted
        rc, out, err = self.run_script_missing(missing)
        self.assertNotEqual(rc, 0)

    def run_script_missing(self, path):
        proc = subprocess.run(
            [sys.executable, SCRIPT, path],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
        return proc.returncode, proc.stdout, proc.stderr

    def test_no_arguments_usage_and_exit_code(self):
        proc = subprocess.run(
            [sys.executable, SCRIPT],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
        self.assertEqual(proc.returncode, 2)
        self.assertIn("usage: summarise-telemetry.py LOGFILE", proc.stderr)

    def test_verdict_detail_kept(self):
        log = (
            '{"ts":"2025-10-15T12:00:00.000000Z","sub":"sys","sev":"INFO","msg":"BeforeSceneLoad"}\n'
            '{"ts":"2025-10-15T12:00:01.000000Z","sub":"sys","sev":"INFO","msg":"verdicts: camera BROKEN (18 of 30 fps)"}\n'
        )
        rc, out, err = self.run_script(log)
        self.assertEqual(rc, 0)
        self.assertIn("18 of 30 fps", out)
        self.assertIn("| camera | BROKEN | 18 of 30 fps |", out)

    def test_info_lines_not_reported_as_problems(self):
        log = (
            '{"ts":"2025-10-15T12:00:00.000000Z","sub":"sys","sev":"INFO","msg":"BeforeSceneLoad"}\n'
            '{"ts":"2025-10-15T12:00:01.000000Z","sub":"sys","sev":"INFO","msg":"All good"}\n'
            '{"ts":"2025-10-15T12:00:02.000000Z","sub":"sys","sev":"WARN","msg":"Something odd"}\n'
            '{"ts":"2025-10-15T12:00:03.000000Z","sub":"sys","sev":"ERROR","msg":"Bad thing"}\n'
        )
        rc, out, err = self.run_script(log)
        self.assertEqual(rc, 0)
        self.assertIn("### Warnings and errors", out)
        self.assertIn("Something odd", out)
        self.assertIn("Bad thing", out)
        # INFO lines should not appear as problem entries
        self.assertNotIn(": All good", out.split("### Warnings and errors")[1] if "### Warnings and errors" in out else out)

    def test_seven_fractional_digit_timestamp_parses(self):
        # Real MQ3 logs carry 7 fractional digits, which datetime.fromisoformat cannot take directly;
        # parse_time must truncate to 6 digits and the duration must still be exact.
        log = (
            '{"ts":"2026-10-16T16:01:17.4294980Z","sub":"sys","sev":"INFO","msg":"BeforeSceneLoad"}\n'
            '{"ts":"2026-10-16T16:01:19.4294980Z","sub":"sys","sev":"INFO","msg":"verdicts: scan OK"}\n'
        )
        rc, out, err = self.run_script(log)
        self.assertEqual(rc, 0)
        self.assertIn("Skipped 0 unreadable line(s).", out)  # parsed, not skipped
        self.assertIn("Start: 2026-10-16 16:01:17 UTC", out)
        self.assertIn("End: 2026-10-16 16:01:19 UTC", out)
        self.assertIn("Duration: 2.00 s", out)

    def test_end_advances_with_later_events(self):
        log = (
            '{"ts":"2025-10-16T16:00:00.000000Z","sub":"sys","sev":"INFO","msg":"BeforeSceneLoad"}\n'
            '{"ts":"2025-10-16T16:00:02.000000Z","sub":"sys","sev":"INFO","msg":"camera warming"}\n'
            '{"ts":"2025-10-16T16:00:05.000000Z","sub":"sys","sev":"INFO","msg":"scene ready"}\n'
            '{"ts":"2025-10-16T16:00:03.000000Z","sub":"sys","sev":"INFO","msg":"late out-of-order event"}\n'
        )
        rc, out, err = self.run_script(log)
        self.assertEqual(rc, 0)
        # End advances with the latest event; the out-of-order 16:00:03 line must not pull it back.
        self.assertIn("Start: 2025-10-16 16:00:00 UTC", out)
        self.assertIn("End: 2025-10-16 16:00:05 UTC", out)
        self.assertIn("Duration: 5.00 s", out)

    def test_way_off_ranks_above_broken_in_worst_offenders(self):
        log = (
            '{"ts":"2025-10-16T16:00:00.000000Z","sub":"sys","sev":"INFO","msg":"BeforeSceneLoad"}\n'
            '{"ts":"2025-10-16T16:00:01.000000Z","sub":"sys","sev":"INFO","msg":"verdicts: camera BROKEN (fps low), audio WAY_OFF (no sound)"}\n'
        )
        rc, out, err = self.run_script(log)
        self.assertEqual(rc, 0)
        worst = out.split("### Worst offenders")[1].split("###")[0]
        self.assertIn("- audio: WAY_OFF", worst)
        self.assertIn("- camera: BROKEN", worst)
        self.assertLess(worst.index("- audio: WAY_OFF"), worst.index("- camera: BROKEN"))


if __name__ == "__main__":
    unittest.main()
