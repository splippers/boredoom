# BoreDOOM components proven on the Quest 3 (candidates for com.craicken.core)

Written by: Claude (AI). Passenger: Jon Davis (directed and tested; did not write this code).
Information only: nothing here has been moved. Core is `~/craickencore` (0.1.0); changes to it need Jon's go-ahead.
Core already has: XrRig, HeadTracker, XrInput, QuestPermissions, RoomSurfaces, TextWrap, OpenXrFeatures, QuestBuild.

| Component (BoreDOOM path) | Proven on headset? | Core gap it fills |
|---|---|---|
| Passthrough rig bootstrap (`Bootstrap/QuestXrRigBootstrap.cs`) | Yes, clean intro log shows passthrough on | Compare with XrRig.Build(ArPassthrough); likely already covered |
| Scene permission handling + USER_SET lesson (`Diagnostics/AndroidPermissions.cs`, `AR/SceneSurfaceReader.cs`) | Yes (Binary4Babies method) | Restart-the-plane-manager-after-consent; refresh without erasing data; the `pm set-permission-flags` fix belongs in a doc/tool |
| Space Setup room reading, multi-level floors (`Runtime/SceneRoomBuilder.cs`, `AR/SceneSurfaceReader.cs`) | Yes, "6 floor surfaces / 2 levels" | Rooms from planes with stable ids; more than RoomSurfaces' snapshot |
| Open the headset's Space Setup (`AR/DoorGlow.cs` RequestSceneCapture, `AR/HouseScanController.cs`) | Yes, opens and resumes | TryRequestSceneCapture wrapper with resume handling |
| Palm-raised miniature with hand zoom/turn (`Dollhouse/PalmMiniatureController.cs`, `Runtime/PalmDollhouse.cs`) | Yes (hand tracking), gesture tests pass | Hand-gesture input; world-in-hand pattern |
| Gaze dwell selector + head gaze pointer (`HandsFree/GazeDwellSelector.cs`) | Yes | Hands-free selection for any game |
| Voice link to the PC (`Voice/`, `tools/voice/voice_link.py`) | Yes, but flaky link | Needs the PC side; maybe a separate package |
| Device log + request files + camera/hand probes (`Diagnostics/`) | Yes | Debug harness shared by all projects |
| Batch build + test recipe (`Editor/QuestBuild.cs`, PipeWire fix) | Yes | Overlaps core QuestBuild; merge, do not duplicate |
| Intro/menu panels with see-through backing (`UI/IntroController.cs` CleanMenu) | Yes | World-space menu kit; keep backing <= ~30% opaque for AR |

Do not promote: chore modes, scoring, Claudia, house-map game logic, June media (project-specific).
