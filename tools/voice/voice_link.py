#!/usr/bin/env python3
"""PC side of the headset <-> Claude voice link.

Written by: Claude (AI). Passenger: Jon Davis (ideas man; directed and tested, did not write this code).

The headset connects to 127.0.0.1:PORT through `adb reverse tcp:PORT tcp:PORT`, so audio never leaves the LAN.
Wire format, both directions: 1 byte type, 4 bytes big-endian length, then the payload.

  headset -> PC   0x01 AUDIO  raw PCM, signed 16-bit little-endian, 16000 Hz, mono (any chunk size)
                  0x02 END    the player stopped talking; transcribe what was sent since the last END
  PC -> headset   0x10 WAV    a complete .wav file to play (Claude's spoken reply)
                  0x11 TEXT   UTF-8 caption to show with it

What was heard is appended, one JSON object per line, to INBOX (default ~/.local/share/boredoom-voice/inbox.jsonl)
and printed to stdout, so a watcher can wake Claude on each line.
Claude speaks by dropping a text file into OUTBOX_DIR (see say.py); this server speaks it with espeak-ng.

Nothing is kept except the transcript text: audio is held in memory only until it has been transcribed.
"""
import argparse
import json
import os
import queue
import socket
import struct
import subprocess
import sys
import tempfile
import threading
import time
import wave

AUDIO, END, WAV, TEXT = 0x01, 0x02, 0x10, 0x11
RATE = 16000
STATE_DIR = os.path.expanduser("~/.local/share/boredoom-voice")
MAX_UTTERANCE_SECONDS = 60


def send_frame(sock, kind, payload):
    sock.sendall(struct.pack(">BI", kind, len(payload)) + payload)


def read_exact(sock, n):
    buf = bytearray()
    while len(buf) < n:
        chunk = sock.recv(n - len(buf))
        if not chunk:
            raise ConnectionError("closed")
        buf += chunk
    return bytes(buf)


def read_frame(sock):
    kind, length = struct.unpack(">BI", read_exact(sock, 5))
    return kind, read_exact(sock, length) if length else b""


def pcm_to_float(pcm):
    import numpy as np
    return np.frombuffer(pcm, dtype="<i2").astype("float32") / 32768.0


def speak_to_wav(text, path):
    """espeak-ng, slightly slow and low: clear in a headset and not mistaken for a person."""
    subprocess.run(["espeak-ng", "-s", "150", "-p", "40", "-w", path, text], check=True, timeout=30)


def log_line(inbox, record):
    line = json.dumps(record, ensure_ascii=False)
    with open(inbox, "a", encoding="utf-8") as f:
        f.write(line + "\n")
    print(line, flush=True)


class Hearing:
    def __init__(self, model_name):
        from faster_whisper import WhisperModel
        self.model = WhisperModel(model_name, device="cpu", compute_type="int8")

    def transcribe(self, pcm):
        if len(pcm) < RATE * 2 // 4:  # shorter than a quarter of a second: a click, not speech
            return ""
        segments, _ = self.model.transcribe(pcm_to_float(pcm), language="en", vad_filter=True, beam_size=1)
        return " ".join(s.text.strip() for s in segments).strip()


def serve_client(conn, hearing, inbox, outbox_dir):
    pcm = bytearray()
    outgoing = queue.Queue()
    alive = threading.Event()
    alive.set()

    def pump_outbox():
        while alive.is_set():
            for name in sorted(os.listdir(outbox_dir)):
                if not name.endswith(".txt"):
                    continue
                path = os.path.join(outbox_dir, name)
                try:
                    text = open(path, encoding="utf-8").read().strip()
                    os.remove(path)
                    if text:
                        outgoing.put(text)
                except OSError:
                    pass
            time.sleep(0.2)

    def pump_send():
        while alive.is_set():
            try:
                text = outgoing.get(timeout=0.2)
            except queue.Empty:
                continue
            with tempfile.NamedTemporaryFile(suffix=".wav") as tmp:
                speak_to_wav(text, tmp.name)
                data = open(tmp.name, "rb").read()
            try:
                send_frame(conn, TEXT, text.encode("utf-8"))
                send_frame(conn, WAV, data)
                log_line(inbox, {"t": time.strftime("%H:%M:%S"), "event": "spoke", "text": text})
            except OSError:
                return

    threading.Thread(target=pump_outbox, daemon=True).start()
    threading.Thread(target=pump_send, daemon=True).start()
    log_line(inbox, {"t": time.strftime("%H:%M:%S"), "event": "headset connected"})
    try:
        while True:
            kind, payload = read_frame(conn)
            if kind == AUDIO:
                if len(pcm) < RATE * 2 * MAX_UTTERANCE_SECONDS:
                    pcm += payload
            elif kind == END:
                text = hearing.transcribe(bytes(pcm))
                pcm.clear()
                if text:
                    log_line(inbox, {"t": time.strftime("%H:%M:%S"), "event": "heard", "text": text})
    except ConnectionError:
        pass
    finally:
        alive.clear()
        log_line(inbox, {"t": time.strftime("%H:%M:%S"), "event": "headset disconnected"})
        conn.close()


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--port", type=int, default=7777)
    ap.add_argument("--model", default="base.en", help="faster-whisper model name (tiny.en, base.en, small.en)")
    ap.add_argument("--state-dir", default=STATE_DIR)
    args = ap.parse_args()

    os.makedirs(os.path.join(args.state_dir, "outbox"), exist_ok=True)
    inbox = os.path.join(args.state_dir, "inbox.jsonl")
    outbox_dir = os.path.join(args.state_dir, "outbox")
    hearing = Hearing(args.model)

    srv = socket.socket()
    srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind(("127.0.0.1", args.port))  # loopback only: the headset reaches it through adb reverse
    srv.listen(1)
    print(f"voice link listening on 127.0.0.1:{args.port} (model {args.model})", flush=True)
    while True:
        conn, _ = srv.accept()
        serve_client(conn, hearing, inbox, outbox_dir)


if __name__ == "__main__":
    sys.exit(main())
