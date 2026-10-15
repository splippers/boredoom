#!/usr/bin/env python3
"""Summarise a BoreDoom headset log (MQ3.log) as a one-page Markdown report.

Authorship: first draft by Qwen 3 4B (AI) from a written spec; its structure and the defects listed in the
review were rewritten and corrected by Claude (AI). Directed and tested by Jon Davis, who did not write it.

Input: one JSON object per line with ts, sub, sev, msg. Anything else is skipped and counted.
A new run starts at each msg beginning "BeforeSceneLoad". A msg beginning "verdicts:" carries comma-separated
"<component> <VERDICT>[ (detail)]" items, VERDICT being OK, BROKEN, WAY_OFF or UNKNOWN.
"""
import json
import re
import sys
from datetime import datetime

SEVERITY = {"OK": 0, "UNKNOWN": 0, "BROKEN": 1, "WAY_OFF": 2}
ITEM = re.compile(r"^(\S+)\s+(OK|BROKEN|WAY_OFF|UNKNOWN)\b\s*(?:\((.*)\))?\s*$")


def parse_time(text):
    """ISO-8601 UTC; real logs carry seven fractional digits, which fromisoformat does not accept."""
    text = re.sub(r"(\.\d{6})\d+", r"\1", text.strip()).replace("Z", "+00:00")
    return datetime.fromisoformat(text)


def read_runs(path):
    runs, skipped = [], 0
    with open(path, encoding="utf-8", errors="replace") as handle:
        for raw in handle:
            try:
                row = json.loads(raw)
                stamp, sub, sev, msg = parse_time(row["ts"]), row["sub"], row["sev"], row["msg"]
            except (ValueError, KeyError, TypeError):
                skipped += 1
                continue
            if not isinstance(msg, str):
                skipped += 1
                continue
            if msg.startswith("BeforeSceneLoad") or not runs:
                runs.append({"start": stamp, "end": stamp, "verdicts": [], "problems": []})
            run = runs[-1]
            run["end"] = max(run["end"], stamp)
            if msg.startswith("verdicts:"):
                items = {}
                for part in msg[len("verdicts:"):].split(","):
                    found = ITEM.match(part.strip())
                    if found:
                        items[found.group(1)] = (found.group(2), found.group(3) or "")
                run["verdicts"].append(items)
            if sev in ("WARN", "ERROR"):
                run["problems"].append((stamp, sub, msg))
    return runs, skipped


def render(runs, skipped):
    lines = []
    for number, run in enumerate(runs, 1):
        lines += [f"## Run {number}", f"Start: {run['start'].strftime('%Y-%m-%d %H:%M:%S')} UTC",
                  f"End: {run['end'].strftime('%Y-%m-%d %H:%M:%S')} UTC",
                  f"Duration: {(run['end'] - run['start']).total_seconds():.2f} s", ""]

        if not run["verdicts"]:
            lines += ["No verdicts recorded.", ""]
        else:
            last, worst, bad_lines = {}, {}, {}
            for items in run["verdicts"]:
                for name, (verdict, detail) in items.items():
                    last[name] = (verdict, detail)
                    if SEVERITY[verdict] > SEVERITY.get(worst.get(name, "OK"), 0):
                        worst[name] = verdict
                    if verdict != "OK":
                        bad_lines[name] = bad_lines.get(name, 0) + 1
            lines += ["| component | verdict | detail |", "| --- | --- | --- |"]
            lines += [f"| {name} | {verdict} | {detail} |" for name, (verdict, detail) in last.items()]
            lines += ["", "### Worst offenders"]
            offenders = sorted(worst.items(), key=lambda item: -SEVERITY[item[1]])
            lines += [f"- {name}: {verdict} ({bad_lines[name]} verdict lines)" for name, verdict in offenders] or ["None."]
            lines.append("")

        lines.append("### Warnings and errors")
        lines += [f"- {stamp.strftime('%H:%M:%S')} {sub}: {msg}" for stamp, sub, msg in run["problems"]] or ["None."]
        lines.append("")
    lines.append(f"Skipped {skipped} unreadable line(s).")
    return "\n".join(lines)


def main():
    if len(sys.argv) != 2:
        print("usage: summarise-telemetry.py LOGFILE", file=sys.stderr)
        return 2
    runs, skipped = read_runs(sys.argv[1])
    print(render(runs, skipped))
    return 0


if __name__ == "__main__":
    sys.exit(main())
