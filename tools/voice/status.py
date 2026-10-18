#!/usr/bin/env python3
"""Show Claude's status in the headset without speaking. Usage: status.py "working\ta build"   or   status.py idle

Written by: Claude (AI). Passenger: Jon Davis (ideas man; directed and tested, did not write this code).
"""
import os
import sys
import time

outbox = os.path.expanduser("~/.local/share/boredoom-voice/outbox")
os.makedirs(outbox, exist_ok=True)
text = " ".join(sys.argv[1:]).replace("\\t", "\t").strip() or "idle"
stamp = time.time_ns()
tmp = os.path.join(outbox, f".{stamp}.part")
open(tmp, "w", encoding="utf-8").write(text)
os.rename(tmp, os.path.join(outbox, f"{stamp}.status"))
