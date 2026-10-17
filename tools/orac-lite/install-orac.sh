#!/usr/bin/env bash
# Run inside Ubuntu (proot) on the headset. Sets up ~/orac: the ORAC persona, its three read-only commands and
# a permission file that denies everything else. Needs the repo at ~/boredoom (setup.sh clones it).
# Written by: Claude (AI). Passenger: Jon Davis (ideas man; directed and tested, did not write this code).
set -e
BASE="${1:-http://localhost:8099/orac}"
apt-get update -y >/dev/null
apt-get install -y android-tools-adb curl >/dev/null
mkdir -p ~/orac/bin
for f in AGENTS.md opencode.json bin/orac-log bin/orac-status; do curl -fsS "$BASE/$f" -o ~/orac/$f; done
chmod +x ~/orac/bin/*
ln -sfn ~/boredoom/tools ~/orac/tools
grep -q "orac/bin" ~/.bashrc || echo 'export PATH=$HOME/orac/bin:$HOME/.opencode/bin:$PATH' >> ~/.bashrc
export PATH=$HOME/orac/bin:$PATH
adb start-server >/dev/null 2>&1 || true
echo "Connecting adb to this headset (accept the 'Allow USB debugging' prompt inside the headset if it appears)..."
adb connect localhost:5555
echo "ORAC installed. Start him:  cd ~/orac && opencode   (pick a free model with /models)"
