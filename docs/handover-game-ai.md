# Handover: BoreDoom / Chorewars (for the "Game AI" session)

Written by: Claude Sonnet 5.5 (AI). Jon Davis directs and tests on the headset; he does not write code.
State as of 2026-11-03, branch `pickle/pop`, last commit `1dbac9b`.

## What this is
Hands-free passthrough-AR chore game for Meta Quest 3 (Unity 6000.4.5f1, Built-in RP, IL2CPP, OpenXR, package
`com.boredoom.chorewars`). Gaze, nod/shake and voice only. Jon is racing a rival game. Claudia, a Scots red-haired
floating head, is the in-headset voice of the AI; she follows the player.

## Standing rules (do not break)
- Never commit Jon's uncommitted `Packages/manifest.json`, `ProjectSettings/*`, `opencode.jsonc`, `.mcp.json`,
  `Packages/packages-lock.json`, generated `Assets/XR`, `.utmp/`, telemetry dumps. The manifest also carries
  `com.unity.xr.hands 1.5.0` (Jon said yes); a fresh checkout needs that line.
- Commit messages: `Written by: Claude Sonnet 5.5 (AI)`, `Passenger: Jon Davis (ideas man; directed and tested, did not write this code)`,
  plus the Co-Authored-By / Claude-Session trailers. Never describe the work as Jon's. Commit and push each finished change.
- Privacy: no passthrough images to disk or network without Jon's say-so. Headset screenshots show his real room; mask or crop, delete.
  His late mother's voice recordings are private and local only: not in this repo, not in the build, not cloned, until he raises it.
- Several adb devices exist. Identify the Quest with `tools/quest-device.sh` (model Quest_3, product eureka). Never hardcode serials in tracked
  files. The Samsung phones are not the Quest. Do not change power or stay-awake settings.
- Unity MCP: the old local unity-mcp (port 57597, `UNITY_MCP_TOKEN`) is retired. Use the cloud `ai-game-developer` MCP (entry in the untracked `.mcp.json`; never write its key to a tracked file).
- Verify other agents' claims (Pickle once reported a Quest serial that did not exist).

## Build, test, install (on LAVE)
- Batch Unity hangs silently unless PipeWire is blocked. Always: `PIPEWIRE_REMOTE=/nonexistent PULSE_SERVER=/nonexistent/none`.
  See memory file `feedback-unity-batch-hang-pipewire.md`.
- Only one Unity may have the project open. Close Jon's editor first; a GUI Unity on a different path (the SANDIEGO copy is stale) is the wrong one.
- Build: `Unity -batchmode -nographics -quit -projectPath . -executeMethod Chorewars.Build.QuestBuild.BuildFromMenu -logFile <log>` -> `Builds/BoreDOOM-Quest.apk`.
- Tests: `-runTests -testPlatform EditMode -testResults <abs xml> -logFile <log>` without `-quit`; kill the leftover Unity afterwards by exact PID.
  Currently 224 pass.
- Install: `tools/redeploy.sh`. After a fresh install set the scene permission flag (see below).
- Never `pkill -f` or `kill $(pgrep -f ...)` from the shell (it kills your own shell).

## Headset facts
- The Quest can appear on USB and Wi-Fi at once; use `adb -s <serial>`. `adb reverse tcp:7777 tcp:7777` drops whenever the app or cable resets;
  a loop script re-applying it helped.
- Device files in `/sdcard/Android/data/com.boredoom.chorewars/files/`: `MQ3.log`, `house.json`, `floor-grids.json`, `player_profile.json`.
  Request files polled every 0.5 s: `scene-capture.request` (opens Meta Space Setup), `ceiling-scan.request` (room name), `ceiling-height.request`,
  `ceiling-cancel.request`, `floor-scan.request` (room name), `floor-done.request`, `floor-cancel.request`, `name-room.request`, `show-house.request`.
- Spatial data (room scan) lesson, taken from the NibBall game (Binary4Babies, `/mnt/SANDIEGO/Projects/binary4babies/NibBall`):
  after install the permission can be granted without the USER_SET flag, so no prompt appears and the scan stays empty. Fix:
  `pm grant` both `com.oculus.permission.USE_SCENE` and `horizonos.permission.USE_SCENE`, then
  `pm set-permission-flags com.boredoom.chorewars <perm> user-set`, then restart the app. The room reader also now asks again every 8 s and
  restarts the plane manager if the answer arrives late. This produced floors=1 ceilings=1 walls=4 windows=1 other=6 on the headset.
- Meta only returns what was captured in Space Setup. Door frames only appear if labelled there.

## Voice link
- PC server: `tools/voice/voice_link.py` on 127.0.0.1:7777 (faster-whisper base.en, Piper en_GB-alba-medium). Start detached with the venv python in
  `~/.local/share/boredoom-voice/bin/python`. `say.py` speaks, `status.py` shows status. Inbox: `~/.local/share/boredoom-voice/inbox.jsonl`.
- Conversation layer: `tools/voice/conversation.py` joins broken sentences, drops noise words, flags `to_her`.
- If Claudia is missing: voice server down, `adb reverse` dropped, or the app is not resumed (a Meta Guardian dialog or sleeping headset stops it).
- Beethoven "Ode to Joy" cue (`pw-play`, not paplay) on the PC speakers when Jon must pick up the headset.

## What is built (all committed)
- Claudia: fiery red hair, Scots wording ("och", "aye"), follows the player, gaze bar opens the mic.
- Dollhouse on the left palm, room outlines drawn in the real room, named rooms, linked rooms.
- Ceiling scan with two-look height measurement; floor-space scan (green walked, red furniture, saved grid).
- `RoomScan.cornersXZ` keeps the real room polygon; `RoomOutlines` draws it. `SceneRoomBuilder` carries floor outlines through.
- `DoorGlow` (`Assets/Chorewars/Scripts/AR/DoorGlow.cs`): pulsing outline on door frames from the headset scene, requests Space Setup, and hands
  scene rooms to `HouseMapDriver.AdoptSceneRooms`.

## Open work, in Jon's priority order
1. Verify on the headset: the room outline follows real walls; Claudia connects; door glow; floor scan. Jon has not yet confirmed the latest builds.
2. Jon wants a Matterport-style digital twin of his house (textured, walkable, dollhouse). Geometry from Space Setup scene mesh; texture from
   passthrough camera frames only if Jon agrees to save pictures on the headset (asked, not answered). Then export a mesh to the PC.
3. Every room must be scannable like NibBall: add an in-headset readout (permission state, surfaces, walls, doors) and a hands-free trigger
   (both fists held, or voice) that calls `scene-capture` without a request file. Scene room ids (`Scene-N`) are ranked by area, so they can reorder
   between scans and break room names; make them stable.
4. Door-frame scanning to link rooms (Jon asked; partly covered by the headset's door frames).
5. Jon said "stop the game running its books and windows and things while testing": never clarified. Ask him.
6. Unity MCP: resolved. The old plugin route was dropped; Unity is reached through the AI Game Developer cloud MCP.
7. Unity AI (Jon is adding it for graphics, sound, music). Check Unity's terms on generated assets before shipping them.
8. Hospital swarm (SysMedic board, posting as ARCHITECT) holds BoreDoom survey picks; the credential lives outside the repo. Do not post without Jon.

## Where things are
`Assets/Chorewars/Scripts/{AR,Runtime,Voice,HandsFree,Editor}`, tests in `Assets/Chorewars/Tests/Editor`, voice tools in `tools/voice`, memory notes in
`~/.claude/projects/-home-jon-boredoom/memory/`.
