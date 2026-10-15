# CLAUDE.md — BoreDoom / Chorewars

## Project
- **What:** AR chore game (hoover/mow coverage, movement, scoring) for **Meta Quest 3**
- **Unity:** 6000.4.5f1, Built-in RP (not URP/HDRP)
- **Platform:** Android / Quest, OpenXR, IL2CPP
- **Repo:** `\\marvin\Projects\boredoom` | GitHub: `github.com:splippers/BoreDoom.git`
- **Local clone:** `C:\Users\Jon\Boredoom`

## Architecture
- Scenes in `Assets/Chorewars/Scenes/` are saved **without cameras/rigs** — runtime bootstraps add XROrigin + camera
- Bootstrap flow: `QuestXrRigBootstrap.cs` → `BootstrapSceneRouter.cs`
- Scenes: `Bootstrap.unity`, `HooverMode.unity`, `MowingMode.unity`

## Source of truth files
| Topic | Path |
|--------|------|
| Runtime XR rig + logs | `Assets/Chorewars/Scripts/Bootstrap/QuestXrRigBootstrap.cs` |
| Scene routing | `Assets/Chorewars/Scripts/Core/BootstrapSceneRouter.cs` |
| XR asset bootstrap (Editor only) | `Assets/Editor/BoreDoomXrManagementBootstrap.cs` |
| Build scenes | `ProjectSettings/EditorBuildSettings.asset` |
| Packages | `Packages/manifest.json` |
| Link / stripping | `Assets/link.xml` |
| Telemetry | `Assets/Chorewars/Scripts/Diagnostics/TelemetryLogger.cs` |

## AI assistant notes
- **MCP:** `com.ivanmurzak.unity.mcp` is in `Packages/manifest.json` — free Unity Editor ↔ AI bridge. Server auto-starts when Unity opens.
- **Connection:** opencode.json in repo root connects to Unity MCP server. Find port in Unity → Window → AI Game Developer.
- **Prerequisite:** Unity Editor must be open with the MCP package resolved for AI to control the editor.
- **Key constraint:** Avoid drive-by refactors. Make minimal, focused changes.
- **Safety:** Full passthrough AR preferred; avoid opaque full-screen overlays.


## Git attribution: Jon is a passenger
Jon directs the project, tests on the headset, and makes the decisions. He does **not** write the code. Every
commit message and PR description must make that plain, whoever (Claude, Pickle, any agent) writes it.
- Say which agent wrote the change, e.g. `Written by: Claude (AI)` or `Written by: Pickle / OpenCode (AI)`.
- Add `Passenger: Jon Davis (directed and tested; did not write this code)`.
- Never describe the work as Jon's ("I added...", "we fixed...") and never imply he reviewed code he has not read.
- Keep the `Co-Authored-By` / `Claude-Session` trailers that Claude Code adds.
- The git author identity is Jon's account (`jon@splippers.com`) only because the commit has to be filed under someone;
  it is not a claim that he authored the change. Commits already pushed under `Claude <noreply@anthropic.com>` stay as they are.
- PR bodies: open with one line saying the change was written by an AI agent and Jon only directed and tested it.

## Build notes
- IL2CPP backend
- Meta Quest Support + Oculus Touch Controller Profile required in OpenXR
- Build docs: `docs/quest3-build.md`
- Pull logs: `adb exec-out run-as com.boredoom.app cat /storage/emulated/0/Android/data/com.boredoom.app/files/MQ3.log`
