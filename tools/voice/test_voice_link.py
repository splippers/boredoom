#!/usr/bin/env python3
"""End-to-end test of voice_link.py with a fake headset. Run with the voice venv's python.

Written by: Claude (AI). Passenger: Jon Davis (ideas man; directed and tested, did not write this code).
"""
import json
import os
import socket
import subprocess
import sys
import tempfile
import time
import wave

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from voice_link import AUDIO, END, TEXT, WAV, read_frame, send_frame  # noqa: E402

PORT = 17777
state = tempfile.mkdtemp(prefix="voicetest-")
server = subprocess.Popen([sys.executable, os.path.join(HERE, "voice_link.py"), "--port", str(PORT), "--state-dir", state, "--merge-wait", "0.3"],
                          stdout=subprocess.PIPE, text=True)
failures = []


def check(name, ok, detail=""):
    print(("PASS " if ok else "FAIL ") + name + (f"  [{detail}]" if detail and not ok else ""))
    if not ok:
        failures.append(name)


try:
    print(server.stdout.readline().strip())  # model loads before it listens
    sock = socket.create_connection(("127.0.0.1", PORT), timeout=60)

    wav_path = os.path.join(state, "said.wav")
    subprocess.run(["espeak-ng", "-s", "140", "-w", os.path.join(state, "raw.wav"), "this is the kitchen"], check=True)
    subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-i", os.path.join(state, "raw.wav"), "-ar", "16000", "-ac", "1",
                    "-f", "s16le", wav_path.replace(".wav", ".pcm")], check=True)
    pcm = open(wav_path.replace(".wav", ".pcm"), "rb").read()
    for i in range(0, len(pcm), 3200):  # 100 ms chunks, like a microphone
        send_frame(sock, AUDIO, pcm[i:i + 3200])
    send_frame(sock, END, b"")

    heard = None
    deadline = time.time() + 60
    while time.time() < deadline and heard is None:
        line = server.stdout.readline()
        if not line:
            break
        rec = json.loads(line)
        if rec.get("event") == "heard":
            heard = rec["text"].lower()
    check("kitchen sentence is transcribed", heard is not None and "kitchen" in heard, str(heard))
    check("inbox file holds the same line", "kitchen" in open(os.path.join(state, "inbox.jsonl")).read().lower())

    open(os.path.join(state, "outbox", "1.txt"), "w").write("Hello Jon")
    sock.settimeout(20)
    got = {}
    while "wav" not in got or "text" not in got:
        kind, payload = read_frame(sock)
        if kind == TEXT:
            got["text"] = payload.decode()
        elif kind == WAV:
            got["wav"] = payload
    check("spoken reply arrives as caption", got.get("text") == "Hello Jon", str(got.get("text")))
    check("spoken reply is a real wav", got["wav"][:4] == b"RIFF" and len(got["wav"]) > 2000)

    send_frame(sock, AUDIO, b"\x00\x00" * 100)  # a click: must not produce a transcript
    send_frame(sock, END, b"")
    time.sleep(1)
    lines = [json.loads(l) for l in open(os.path.join(state, "inbox.jsonl"))]
    check("a click is not transcribed", sum(1 for r in lines if r.get("event") == "heard") == 1)
finally:
    server.terminate()

print("\nALL PASS" if not failures else f"\n{len(failures)} FAILED")
sys.exit(1 if failures else 0)
