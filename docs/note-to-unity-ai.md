# Note to Unity AI (from the BoreDOOM! Claude session, 2026-11-03)

Written by: Claude (AI). Passenger: Jon Davis (directed and tested; did not write this code).

Please analyse why the headset's room scan returns nothing in BoreDOOM. Facts first, guesses after.

## Project
- Unity 6000.4.5f1, Built-in RP, IL2CPP, OpenXR, Meta Quest 3. Package com.boredoom.chorewars.
- Open /home/jon/boredoom (NOT the copy on /mnt/SANDIEGO; that one has an old manifest and goes into safe mode).
- Rules: minimal focused changes, no drive-by refactors, do not commit Packages/manifest.json, ProjectSettings or opencode.jsonc. Full passthrough AR; no opaque overlays.
- Another session is editing the working tree (ScanReadout*, DoorGlow, SceneSurfaceReader, HouseMapDriver). Do not overwrite uncommitted changes; check `git status` first.

## What was installed
- Builds/BoreDOOM-Quest.apk (built 2026-10-31, commit 1dbac9b), installed with `adb install -r` on the Quest (serial 2G0YC1ZFB408XG).
- USE_SCENE permissions granted and flagged user-set (com.oculus.permission.USE_SCENE, horizonos.permission.USE_SCENE).

## What the device log says (MQ3.log, 2026-11-03 23:02-23:04)
- Permission, camera and mesh verdicts OK. xrDisplay flips WAY_OFF/OK on every pause/resume (probably harmless).
- `room outline from the headset: floors=0 ceilings=0 walls=0 doors=0 windows=0 other=0`
- `plane manager restarted (no room surfaces yet); restart 1..4`, still nothing.
- 23:04:27 `scene capture requested`, 23:04:28 `the headset's room scan is open`, 23:04:30 `scene capture finished; reading the room it saved`. Space Setup was open only ~3 s, so it may have closed without saving a room.
- Earlier (before the last reinstall) the same code found floors=1 ceilings=1 walls=4 windows=1 other=6, so the approach works when a room exists.

## Questions
1. Is the plane manager (ARPlaneManager, requestedDetectionMode Horizontal|Vertical) set up correctly, and does it need the OpenXR Meta "Anchor/Scene" feature enabled in the OpenXR settings for the Android target?
2. Does the manifest in the built APK declare com.oculus.permission.USE_SCENE, com.oculus.permission.USE_ANCHOR_API and the scene-support feature (com.oculus.feature.* / horizonos)?
3. Is the 3 s Space Setup visit a bug in how MetaOpenXRSessionSubsystem.TryRequestSceneCapture is called/resumed, or just the user closing it?
4. After a saved room exists, does the app need a full restart (not just plane manager toggle) to see it?

Code to read: Assets/Chorewars/Scripts/AR/SceneSurfaceReader.cs, DoorGlow.cs, Runtime/SceneRoomBuilder.cs, Runtime/HooverSceneBootstrapper.cs. Reference that works: NibBall at /mnt/SANDIEGO/Projects/binary4babies/NibBall. Handover: docs/handover-game-ai.md.

Please report findings; do not change Packages/manifest.json without telling Jon.
