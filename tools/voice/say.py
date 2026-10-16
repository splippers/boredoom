#!/usr/bin/env python3
"""Make the voice link speak a sentence in the headset. Usage: say.py "Walk to the kitchen door and say 'door'."

Written by: Claude (AI). Passenger: Jon Davis (ideas man; directed and tested, did not write this code).
"""
import os
import sys
import time

outbox = os.path.expanduser("~/.local/share/boredoom-voice/outbox")
os.makedirs(outbox, exist_ok=True)
text = " ".join(sys.argv[1:]).strip()
if not text:
    sys.exit("usage: say.py TEXT")
tmp = os.path.join(outbox, f".{time.time_ns()}.part")
open(tmp, "w", encoding="utf-8").write(text)
os.rename(tmp, tmp[:-5].replace("/.", "/") + ".txt")
