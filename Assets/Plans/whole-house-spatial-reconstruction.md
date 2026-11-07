# Project Overview
- **Game Title:** BoreDOOM!
- **High-Level Concept:** A clean Quest launch experience with imported intro graphics and music, followed by a dollhouse of the rooms already saved in Meta Space Setup. This milestone deliberately excludes Claudia, BoreDOOM's custom scanning/reconstruction workflow and the unfinished vacuum-recognition game.
- **Players:** Single player.
- **Inspiration / Reference Games:** Existing BoreDOOM identity and a tabletop dollhouse of the real home; no Matterport-equivalent reconstruction claim.
- **Tone / Art Direction:** Imported metallic/orange BoreDOOM branding, comfortable passthrough presentation, readable neutral miniature geometry.
- **Target Platform:** Android / Meta Quest 3.
- **Screen Orientation / Resolution:** Stereoscopic, device-selected headset resolution; world-space UI with a fixed-position gaze-selectable panel.
- **Render Pipeline:** Built-in; Unity 6000.4.5f1, New Input System, AR Foundation 6.6.0, Meta OpenXR 2.6.1, OpenXR 1.18.0. Reverify versions before implementation.

## Authoritative revised scope
This section supersedes the previous whole-house reconstruction plan retained below as history. The user selected:
1. **Intro and dollhouse only** for the clean build.
2. **BoreDOOM_Logo.png + BoreDOOM_CombatTheme.wav** for the intro.
3. **Meta's saved room data** for the dollhouse.

### Latest user correction — overrides tabletop presentation below
The user reports no audible intro music and requires the in-game dollhouse to reveal above an outstretched **left palm**, with pinch/pull rotation and zoom. Fix the selected clip's explicit loading/preload and foreground playback lifecycle, and add truthful audio state logs; do not alter headset volume automatically (device media stream is unmuted at 9/15 and the source clip has nonzero samples).

Add a dedicated event-driven XR Hands presentation controller for the clean miniature, reusing the existing tested PalmGate, PinchZoom and TwoHandGrab maths without restoring legacy PalmDollhouse/Claudia/custom scanning. Hide until the left palm is up; use show/hide hysteresis and hide on tracking/data loss. Keep the miniature above the palm, preserve rotation/zoom through updates, and operate only on presentation transforms. Right-hand pinch + sideways drag rotates; pinch + pull toward the body zooms. Two-hand pinch + spread/close zooms and turning the hand-to-hand axis rotates. Do not reveal a hidden house merely because a pinch occurs. Bound scale to a comfortable palm-sized range; Recenter resets gesture view rather than forcing a tabletop placement.

Verify audio data/playback, palm gating, tracking-space conversion, gesture maths, source-data immutability and no visibility override from the room reader. Compile, test and deploy an **update** of the already-clean app without clearing its saves/permissions again. User confirmation of audible music and actual hand interaction remains required; logs alone cannot prove audibility.

The user requests clearing all installed BoreDOOM versionsfrom the Quest. Discovery currently identifies one candidate package, **com.boredoom.chorewars**. Different APK builds sharing this package replace one another rather than coexist. Re-enumerate at deployment; verify any additional candidate's identity before uninstalling it. Do not remove unrelated games with 'doom' in their names.

A clean uninstall removes BoreDOOM's app-local profile, preferences, saved house data and cached files. **Keep Meta Space Setup, device-wide spatial data, other apps, local project media and source code intact.** Build and validate a fresh APK before uninstalling the working app. Do not use uninstall -k, adb install -r, forced permission grants, or repush old house/profile saves for this clean deployment.

Earlier requests for green slime, vacuum recognition/running-noise detection and pre-hoover clutter outlines are deferred, not implemented or represented as working in this milestone. The tracking method remains undecided. No camera image processing, microphone capture, PC voice relay, cloud backend, GLB importer or extra XR SDK is required here.

# Game Mechanics
## Core Gameplay Loop
Launch → imported logo and intro music → explicit Continue → read the rooms already present in Meta Space Setup → show the miniature house → select a level and inspect/recenter it → optionally return to the intro. No chore timer starts, no scan progress is fabricated and no room is guessed when Meta data is missing.

The dollhouse is a schematic built from measured scene surfaces, not a saved photogrammetric mesh. This keeps the first milestone small and testable and supports autonomy/competence: explicit navigation, truthful availability and readable controls rather than an automatic chore or scan sequence.

## Controls and Input Methods
- Preserve the existing XR rig, headset pose tracking, passthrough and safety boundaries.
- Use the existing head-gaze uGUI pointer and New Input System event-driven controls. Voice is not required.
- Intro: Continue and Mute/Unmute.
- Dollhouse: level selection, Recenter and Back to Intro; keep no more than 2–3 primary targets together.
- Room inspection is stationary and virtual. Do not move the live XR origin to center the miniature, disable Guardian, simulate a headset-on state, or require walking stairs.
- Do not request microphone/headset-image permissions in this build. Request only spatial permission when entering dollhouse setup, with explicit denial/settings recovery.

# UI
Reuse procedural world-space uGUI/legacy Text conventions rather than migrating to TMP or UI Toolkit.

```
INTRO (passthrough remains visible outside the panel)
+-------------------------------------+
|         [BoreDOOM_Logo.png]          |
|        Welcome to BoreDOOM!         |
|       [ CONTINUE ] [ MUTE ]          |
+-------------------------------------+

DOLLHOUSE SETUP
+-------------------------------------+
| Your home — 6 room surfaces, 2 levels|
|        [scaled room miniature]      |
| [ LEVEL ] [ RECENTER ] [ BACK ]      |
+-------------------------------------+

NO META ROOM DATA / PERMISSION DENIED
No saved room data is available.
Complete Space Setup in headset Settings,
then return and retry reading it.
[ RETRY READ ] [ BACK ]
```

Counts in the wireframe are illustrative, not acceptance assertions. Show the actual surfaces/levels the headset returns. Do not launch a new scene capture, show the old HOUSE SCAN panel, revive the Claudia avatar, or cover the physical room with raw scan meshes.

Intro music is a dedicated non-spatial AudioSource at a conservative configurable volume. Loop while the intro is open, provide mute, and fade out on Continue. There are no existing AudioMixers discovered; **do not create or restructure mixers without a separate request**. No intro audio should leak into dollhouse inspection or overlap duplicate sources/listeners.

# Key Asset & Context
## Selected imported media
- Assets/Chorewars/ImportedMedia/June/named/images/BoreDOOM_Logo.png — imported 1024x1024 Texture2D. Inspect the actual alpha/background before configuring its UI import; use this asset as-is rather than generating/remastering art.
- Assets/Chorewars/ImportedMedia/June/named/audio/BoreDOOM_CombatTheme.wav — imported stereo AudioClip, 44.1 kHz, approximately 30.77 seconds.
- Keep original and alternative June media untouched. Do not copy all media into Resources or accidentally include every alternative in the APK.

## Existing architecture and reuse
- QuestXrRigBootstrap constructs one persistent XROrigin, tracked camera, AudioListener and on-device ARSession/ARCameraManager. Retain this core; inspect diagnostics attachments for unintended network/camera-image activity.
- BootstrapSceneRouter currently routes Bootstrap immediately to the last-used legacy mode.
- QuestBuild currently builds HooverMode directly, bypassing Bootstrap. The clean build needs an explicit new launch scene list so the intro cannot be skipped by build configuration or old preferences.
- HooverSceneBootstrapper creates scan controllers, room outlines, named-house persistence, floor/ceiling scanners and the old dollhouse pipeline. Clean scenes must not trigger this path.
- VoiceLinkClient/InstructionPanel can instantiate Claudia and start microphone/network work. Inspect all RuntimeInitializeOnLoad and OnEnable entry points, not just whether the avatar is visible.
- HeadGazePointer, GazePanelPlacement and ModeQuickSwitchPanel contain reusable uGUI input/event/raycaster conventions. Avoid activating the legacy chore menu when reusing helpers.
- DollhouseManager, DollhousePresenter and HouseMapPresenter are possible reuse points, but they reference coverage, networking or old room metadata. Reuse only safe presentation/math; do not instantiate the entire legacy scan-to-dollhouse flow just to get a miniature.
- SceneRoomBuilder's existing height-insensitive merging can turn six floor surfaces into four rooms. The clean adapter must preserve source identity and floor elevation; do not merge stacked storeys by XZ overlap.
- SpatialMeshTracker's raw renderers were recently disabled to prevent a purple shell. The new setup does not need this tracker or raw mesh display at all.

## Intended new/modified assets
- New scenes: Assets/Chorewars/Scenes/Intro.unity and Assets/Chorewars/Scenes/DollhouseSetup.unity, kept compatible with the runtime-built XR rig.
- New Scripts/UI/IntroController.cs: explicit logo/audio references, Continue/Mute, fade and scene transition, lifecycle cleanup.
- New Scripts/Runtime/MetaDollhouseSetup.cs: readiness, spatial permission and non-capture reading of saved Meta plane data.
- New Scripts/Dollhouse/MetaRoomMiniature.cs: floor/wall schematic meshes, bounded tabletop scaling, level selection and recentering.
- Small Built-in-compatible miniature materials with explicit references; never renderer-without-material creation.
- Modify QuestBuild to add an explicit clean-build entry point and scene list; preserve its old mode-build path for development rather than silently repointing every existing method.
- Modify only necessary legacy runtime entry guards so intro/setup do not instantiate Claudia, microphone, PC relay, chore modes, custom scan flows or old save restoration. Preserve dormant development code and tests; this approval removes their activation from the clean product, not an indiscriminate source-directory deletion.
- Configure only the selected logo's importer as required for UI, after impact analysis; retain stereo audio and original media files.
- Update build scene configuration as required for loading the two clean scenes, without unrelated PlayerSettings/package changes.
- Add focused EditMode/runtime tests and a clean-install checklist under docs.

# Implementation Steps
1. **Baseline and impact audit.** Dependency: approval of this revised plan. Recheck packages, input backend, scene order, current changes and all auto-initialization paths. Enumerate dependencies/dependents before changing existing assets. Run existing tests and preserve unrelated user edits. Retain temporary verification evidence only as needed; remove leftover test-only runner/screenshot with narrow impact checks.
2. **Create the isolated clean launch path.** Files: Intro.unity, DollhouseSetup.unity, IntroController.cs, relevant build configuration and legacy entry guards. Dependency: Step 1. Keep exactly one XR camera/listener/session and EventSystem. Ensure the clean path cannot read last-mode preferences, route to HooverMode, start Claudia/voice/microphone, create scan controls or restore app-local house.json. Preserve default Meta spatial/passthrough capabilities.
3. **Wire the imported intro assets.** Files: IntroController, selected logo/audio references and UI hierarchy. Dependency: Step 2. Show the exact chosen logo with aspect ratio preserved, no black/magenta material errors and no stretched/cropped title. Play one 2D music source with conservative volume, looping and mute; fade and release it on Continue. Handle pause/resume and repeated return without overlap. Do not add mixers, AI-generated assets or GLB packages.
4. **Read the default Meta scene model.** File: MetaDollhouseSetup. Dependency: Step 2 and verified AR services. Enable/reuse one ARPlaneManager under the correct XROrigin and inspect the installed API before coding. Wait for tracking and explicit spatial consent, then read existing floor/ceiling/wall surfaces and persistent source ids. Do not invoke TryRequestSceneCapture, timer-based scanning, mesh snapshots, exports or room-growth heuristics. Missing data remains an honest unavailable state; retry only rereads the system model.
5. **Build and present the miniature.** File: MetaRoomMiniature and material assets. Dependency: Step 4. Preserve polygon outlines, source ids, elevation and compatible ceiling heights; keep overlapping upstairs/downstairs surfaces distinct. Parent all schematic geometry under a separate presentation root. Fit it to a comfortable tabletop-size bounding box with meter-to-miniature scaling; keep the original measured coordinates unchanged. Provide a level filter and explicit recenter, optional ceiling cutaway and readable room count. Use neutral/transparent materials, no raw opaque room shell in physical passthrough. Do not pretend semantic furniture bounds are detailed furniture meshes.
6. **Verify the clean product before device removal.** Dependency: Steps 2–5. Run compilation, regression tests, isolated runtime/visual checks and build the clean APK to Builds/BoreDOOM-CleanIntro.apk. Verify its scene list, package id, direct asset inclusion, selected media, absent scan/voice initialization and generated permissions. A successfully built artifact is required before uninstalling anything.
7. **Clear BoreDOOM app variants and install once.** Dependency: Step 6 and connected, authorized Quest. Re-enumerate installed candidate packages and verify package identity/label. Uninstall each confirmed BoreDOOM package without retaining data. Verify absence and old app-local game data removal; do not clear Meta Space Setup or unrelated apps. Install the new APK fresh, verify installed hash matches the artifact and launch it. Do not repush the old profile, house.json or preferences.
8. **Headset acceptance and report.** Dependency: Step 7. Verify intro graphics/music/mute/fade, Continue, native room availability, miniature framing, level separation and recenter while the user is stationary. Inspect logs for absent Claudia/microphone/PC relay/custom scan flow and for material/passthrough errors. Report actual success or remaining hardware blockers; do not claim vacuum/slime/clutter features are implemented.

# Verification & Testing
- **Launch:** first scene is Intro; clean install and relaunch always reach the intro rather than an old last-mode preference. Continue loads setup exactly once; Back restores one intro/music source.
- **Audio/UI:** one AudioListener, one intro AudioSource, selected clip assigned, stereo preserved, mute works, Continue fades to silence, pause/resume does not duplicate playback. Logo is readable, aspect ratio preserved, buttons are large/gaze selectable, and EventSystem/raycaster/New Input System wiring is valid.
- **Removed product paths:** no VoiceAvatar, VoiceLinkClient, MicStreamer, InstructionPanel, HouseScanController, ScanReadout, custom FloorScanner/CeilingScanner or legacy ScanFlowController in runtime intro/setup. No microphone image-capture permission request, PC voice connection or custom scene-capture invocation. Tests should validate initializer guards, not just hidden UI.
- **Meta room model:** permission pending/refused/granted-late, no saved setup, session unavailable, data arriving after startup and retry/resume. No guessed room or fake completion. Stable source ids, translated/rotated origins, floor elevations and duplicate surfaces handled without cross-storey merging.
- **Miniature:** synthetic identical XZ rooms at elevations 0 and 3 m remain distinct; polygon outlines are respected; source positions are unchanged by table placement/recenter/level filter; all visible renderers have compatible materials. Geometry is bounded in size and does not obscure the physical room.
- **Regression:** existing tests remain green, with old development behavior retained outside clean scenes. No unrelated user files, SDK source, compatibility guards or scripting defines changed.
- **Build/install:** Android ARM64/IL2CPP and currently compatible settings; no unintended inclusion of all June media. Inspect the APK manifest/build scene list. Installed artifact hash matches. Confirm only verified BoreDOOM packages/data removed, Meta Space Setup preserved and one clean version installed.
- **Physical Quest:** user confirms graphics/music, clear passthrough, comfortable stationary dollhouse view, room/level selection and no Claudia/custom scan interface. If the headset is unavailable, leave deployment explicitly pending.

---
# Previous whole-house plan — superseded historical reference
The remainder is retained only to document the earlier request. It is not authorization to implement its reconstruction, capture, export or chore systems in this clean intro/dollhouse milestone.

# Project Overview
- **Game Title:** BoreDOOM! / Chorewars
- **High-Level Concept:** Turn household chores into rewarding passthrough-AR challenges. This work adds a trustworthy, persistent geometric digital twin of the house that can be inspected as a dollhouse or full-scale model and reused by chore systems.
- **Players:** Single-player scanning and inspection; preserve existing game modes and multiplayer code.
- **Inspiration / Reference Games:** Matterport-style connected house, dollhouse and inspection experience; geometry-first rather than photorealistic reconstruction.
- **Tone / Art Direction:** Existing playful BoreDOOM presentation; neutral, readable mesh materials and measured-versus-estimated indicators for scanning.
- **Target Platform:** Android / Meta Quest 3; Editor simulation for automated verification.
- **Screen Orientation / Resolution:** Stereoscopic headset rendering at device-selected resolution; world-space UI rather than a fixed portrait/landscape phone layout.
- **Render Pipeline:** Built-in. Unity 6000.4.5f1; AR Foundation 6.6.0; Meta OpenXR 2.6.1; OpenXR 1.18.0.

## Confirmed scope and constraints
User chose **geometry first**, confirmed **multiple floors**, and reports that the current scan produces nothing useful. Deliver real measured geometry, incremental room capture, cross-session alignment, local persistence, connected levels, dollhouse/full-scale inspection and geometry export. Camera texturing, photo capture, cloud processing and photogrammetry are explicitly outside this phase.

No existing Assets/Plans directory or approved plan file was found during discovery; this is the initial plan for this request. This document is an approval proposal, not a record of completed implementation.

Two approaches considered:
1. **Retain installed AR Foundation + Meta OpenXR (recommended):** integrates with the existing rig and surfaces; supports scene capture and persistent anchors without adding a second XR stack. Main limitation: reads Meta Space Setup data, not an unrestricted continuous house reconstruction stream.
2. **Adopt a Meta SDK/MRUK or external reconstruction path:** richer scene abstractions or different reconstruction capabilities, but introduces dependencies, migration and possibly PC tooling. Do not install packages or switch SDKs implicitly. Revisit only if the first hardware capability checkpoint demonstrates an unmet requirement.

**Important product boundary:** Matterport-like organization and viewing is feasible as a design target; equivalent capture detail, automatic watertight geometry, furniture accuracy and unlimited multi-room availability are not promised. Missing rooms must be captured through supported Space Setup workflows and aligned; unsupported capture must be surfaced, not hidden by boxes.

# Game Mechanics
## Core Gameplay Loop
### House scan loop
Enter House Scan without starting a chore timer → check permissions/session/provider → open Meta Space Setup when needed → resume and inspect measured data → associate room and level → commit a validated room revision → repeat for other rooms/storeys → save and inspect the assembled house → explicitly export geometry if desired.

Scanning remains active until the user finishes or pauses. A period with no mesh changes means waiting, not whole-house completion. Provider failures and missing geometry stop promotion to a successful measured scan. Partial captures can be retained with clear status.

### Reuse in chores
Existing gameplay continues to consume room metadata through RoomScan/IScanSource compatibility seams. The new house dataset is authoritative for measured geometry and level identity. Coverage remains a separate presentation/gameplay layer; walking is not evidence that a wall was scanned. Do not redesign all seven chore mechanics in this task.

Feedback emphasizes competence and autonomy (SDT): users can see evidence of a successful scan, choose when to finish, and recover without losing previously captured rooms. Avoid fake time-based progress implying measurement completeness.

## Controls and Input Methods
- Verified active input backend: New Input System (activeInputHandler=1). Use event-driven InputActions for any new keyboard/controller accessibility actions.
- Main headset path: large head-gaze dwell targets and existing typed HandsFreeInput intents; voice is optional, never required to recover an empty scan.
- Actions: Open House Scan, Capture/Update Room, Finish, Retry, Save, View House, Select Level, Export, Cancel. Add typed intents only where the existing vocabulary lacks an unambiguous action.
- Existing request-file commands remain optional diagnostic adapters calling the same controller, not the only means of scanning.
- Do not disable Guardian, change device power settings, or require walking stairs while interacting with menus. Perform stair/level selection while stationary.
- Inspection is a separate, explicit mode with a safe stationary view and selected room viewpoints; virtual movement must not move the live XR tracking origin or alter scan coordinates. Keep physical passthrough scanning unobstructed.

# UI
Reuse procedural world-space uGUI and legacy Text conventions used by PlaceholderHud. No UI Toolkit/TMP migration, new companion, or general visual redesign.

Suggested scan panel (normally no more than 2–3 primary targets):
```
HOUSE SCAN — Ground floor / Kitchen
Permission: Granted   Session: Tracking
Planes: 12   Meshes: 3   Triangles: 18,240
Alignment: Localized   Data: Measured mesh
[ CAPTURE / UPDATE ROOM ]  [ VIEW HOUSE ]
More: level, save, export, diagnostics
```
Empty/error state:
```
NO ROOM GEOMETRY RECEIVED
Reason: permission / no Space Setup / provider / waiting
[ OPEN SPACE SETUP ]  [ RETRY ]  [ BACK ]
Existing house retained; nothing new marked complete.
```
Review state:
```
HOUSE REVIEW — 8 rooms / 2 levels
[ DOLLHOUSE / FULL SCALE ]  [ SELECT LEVEL ]
More: room viewpoints, save, export, return to passthrough
```
- Show measured mesh, simplified scene surfaces, legacy estimated room and unaligned capture as different states.
- Show counts/status, not a fabricated capture percentage. Expose provider running state, scene request result, geometry revision, pending generation and last failure in expandable diagnostics.
- Mesh debug display off by default in live passthrough; explicitly enable translucent/wireframe inspection.
- Export displays local output path and counts; no automatic upload or camera image export.

# Key Asset & Context
## Observed current state
- Android OpenXR loader is configured. Meta Quest Session, Mesh, Planes and Camera features are enabled; **Anchors is disabled**.
- Graphics APIs: Vulkan then OpenGLES3; Android minimum API 32. Camera Image Support is enabled, but camera capture is not needed or authorized by this geometry-first phase.
- QuestXrRigBootstrap creates a persistent XROrigin and on-device ARSession/ARCameraManager.
- SpatialMeshTracker directly polls XRMeshSubsystem and generates meshes under a gameplay root. It does not explicitly consume mesh transforms, lacks robust in-flight/stale-callback ownership, and exports an OBJ snapshot rather than a reloadable house dataset.
- SceneSurfaceReader creates ARPlaneManager on XROrigin. **Verified correction:** ARPlane.center in installed AR Foundation 6.6.0 already transforms centerInPlaneSpace to world space; its current use is correct and must not be transformed again. The reader moves the XR origin once for floor alignment; future canonical-frame work must coordinate that movement.
- QuestScanSource stops on timer/count settling and emits an assumed play-space/default room when no scan exists; measured scan success must no longer be inferred from this fallback.
- ScanToDollhousePipeline produces procedural room shells, clears its miniature on StartScan, and primarily compares room bounds when deciding whether to rebuild.
- SceneRoomBuilder uses footprint overlap for merging/stable ids without a height/level check. It flattens doorway height to the lowest floor and does not retain plane trackable ids. Meta plane trackable ids persist for the same Space Setup; recapture may need geometric rematching.
- HouseMapDriver restores positions directly from house.json without verified relocalization. It puts restored rooms in growth anchors, and GrowRoomFromSample does not guard measured rooms even though CloseGaps does. Measured provenance must survive restart.
- PersistentAnchors is a stub; MetaXrHomeOriginProvider is gated OVR SDK code. Do not enable CHOREWARS_META_XR merely to use the installed Unity provider.
- AutoScanDirector has another count/coverage-based stop path. It must not end dedicated whole-house scans.
- DoorGlow owns scene-capture request and resume handling and also adopts scene rooms; centralize capture ownership to avoid competing updates.

## Existing files likely modified
- Assets/Editor/BoreDoomXrManagementBootstrap.cs: explicit relevant feature configuration/preflight, with no unrelated resets.
- Assets/Chorewars/Scripts/Bootstrap/QuestXrRigBootstrap.cs: connect one AR subsystem/anchor service to the persistent rig.
- Assets/Chorewars/Scripts/AR/{SpatialMeshTracker,SceneSurfaceReader,DoorGlow,HouseScanSessionController,HomeOriginAligner}.cs
- Assets/Chorewars/Scripts/Runtime/{HooverSceneBootstrapper,QuestScanSource,ScanFlowController,IScanSource,SceneRoomBuilder,HouseModel,HouseMapDriver,HouseMapStore,ScanToDollhousePipeline,HouseMapPresenter,RoomOutlines,PlaceholderHud}.cs
- Assets/Chorewars/Scripts/HandsFree/{AutoScanDirector,ChorewarsIntent,VoiceCommandRouter}.cs as required for shared actions.
- Assets/Chorewars/Scripts/Diagnostics/AndroidPermissions.cs: consistent callback-based geometry/anchor permission state and denial recovery.
- Existing FloorScanner/CeilingScanner, FloorGrid persistence, coverage and all RoomScan/HouseMapBuilder consumers: regression review for added level/frame/provenance fields; change only necessary compatibility handling.

## Proposed additive assets
- Scripts/AR/HouseScanController.cs: dedicated orchestration and diagnostic state machine.
- Scripts/Integration/ArFoundationHomeOriginProvider.cs: async ARAnchorManager add/save/load adapter compatible with installed packages.
- Scripts/Runtime/HouseCoordinateFrame.cs: explicit session/world/home/room transforms and alignment validation.
- Scripts/Runtime/HouseGeometryData.cs and HouseGeometryStore.cs: versioned metadata and chunked triangle data; source ids, revision, level, bounds, provenance, anchor reference and integrity data.
- Scripts/Runtime/HouseMeshPresenter.cs: shared geometry for dollhouse, level filtering and full-scale review.
- Scripts/Runtime/HouseGeometryExporter.cs: consistent local OBJ + metadata export.
- Scripts/UI/HouseScanPanel.cs: procedural world-space interface using existing gaze/input conventions.
- Prefabs/Spatial/SpatialMeshChunk.prefab if required by ARMeshManager, plus a small Built-in-compatible neutral mesh material. Do not modify package source.
- Tests/Editor: focused scan state, coordinate frame, geometry storage, multi-level matching and export tests; extend existing scene/house/presenter tests.
- docs/{spatial-mapping,quest-meshing-provider,retrieving-exports}.md: replace obsolete setup advice with verified steps/limits.

Dependency graph tool was not available in the current planning toolset. AssetDatabase.GetDependencies and source-reference searches were used as a partial fallback; asset-only dependencies do not capture C# references. During implementation, re-check dependencies/dependents before every existing-asset modification using the dependency tool if available, otherwise combine AssetDatabase and scoped source-reference analysis.

# Implementation Steps
## 1. Establish a truthful hardware capability checkpoint
**Files:** new HouseScanController and diagnostics, AndroidPermissions, SceneSurfaceReader, HouseScanPanel, relevant bootstrap integration.
**Dependencies:** user approval; current project/SDK state rechecked before edits.
- Baseline existing compilation/tests and preserve user changes/settings. Inspect build manifest output and installed Quest firmware/scene state, not just Editor feature toggles.
- Verify one XR loader, one ARSession, rig transform hierarchy, scene permission declaration and runtime answer, provider descriptors/running states, tracking, and Space Setup scene-capture request/resume.
- Replace overlapping permission polling with bounded requests, fresh OS-state checks and explicit denial/settings recovery; no recurring prompt spam or automatic permission grants.
- Expose success/failure and actual plane/mesh/triangle evidence directly in the headset. Save non-image diagnostics only.
- Test one known room on the physical Quest. Record whether actual mesh topology or only simplified surfaces arrive.
- **Gate:** do not claim scanning fixed or proceed as if unlimited house data is available until this proves real geometry. If no measured data arrives, diagnose permissions, runtime scene setup, provider and lifecycle first. If the supported provider/runtime cannot supply the required geometry, report the unmet requirement and present a compatible SDK/runtime/capture alternative for approval.

## 2. Use a single correct mesh ingestion owner
**Files:** SpatialMeshTracker, QuestXrRigBootstrap, new mesh prefab/material, HouseGeometryData.
**Dependencies:** Step 1 provider evidence.
- Prefer ARMeshManager under XROrigin to handle provider transforms and mesh-generation scheduling. Keep SpatialMeshTracker as the compatibility facade for MeshCount, world vertex collection and legacy exporters. Do not concurrently generate meshes through both managers.
- Confirm the installed AR Foundation event/signature from package source before coding; subscribe to real added/updated/removed mesh results, not count-only heuristics.
- Deep-copy validated triangle data with explicit source transform into the canonical frame at commit. Provider meshes remain owned by the provider; persistent data owns its copies.
- Handle revision changes with unchanged count, removals, generation failure, stop/restart, late callbacks and destruction. Bound concurrency/memory and avoid requesting unchanged geometry every poll.
- If retaining low-level generation is demonstrably required, implement the same transform, revision, callback and ownership protections; document the reason instead of silently changing paths.

## 3. Establish persistent house/room alignment
**Files:** ArFoundationHomeOriginProvider, HouseCoordinateFrame, HomeOriginAligner, bootstrap/feature preflight.
**Dependencies:** Steps 1–2; supported save/load anchor descriptors.
- Enable only the Meta Anchor feature and required permissions through explicit project configuration after impact review. Use ARAnchorManager.TryAddAnchorAsync, TrySaveAnchorAsync and TryLoadAnchorAsync supported by the installed packages.
- Use explicit async states with one in-flight operation, failure results, timeout recovery and stored SerializableGuid. Never treat a stub or an unlocalized anchor as resolved.
- Store geometry/room polygons in documented home or room-local coordinates; convert session-world samples using the current localized pose. Maintain meters and original Y elevations.
- Use per-room/submap anchors where necessary for capture spread across storeys; preserve room-to-home transforms and validate agreement before merging. A single anchor is not a guarantee of whole-house accuracy.
- Coordinate floor normalization/recentering with this service; no opportunistic origin shift after a capture without corresponding frame handling.
- On tracking loss/recenter or unresolved anchors, pause commits and hide/flag physical alignment. Existing house can be reviewed virtually but not placed confidently in AR.
- Anchor loss/new Space Setup permits explicit relocalization/reassociation, not silent reinterpretation of old world coordinates. Legacy unframed house.json is retained as unaligned/estimated until reassociated.

## 4. Correct multi-level room identity and topology
**Files:** IScanSource/RoomScan, SceneSurfaceReader, SceneRoomBuilder, HouseModel, HouseMapDriver, HouseMapStore, relevant room lookup consumers/tests.
**Dependencies:** Step 3 canonical coordinate contract.
- Add stable source trackable identity, provenance, level id/elevation and geometry revision without breaking existing serialized fields. Prefer persistent plane/source ids, with height-aware geometric rematching for recaptured setup.
- Transform plane centers/boundaries consistently. Retain measured vertical geometry and available door boundaries/orientation rather than only a flattened center.
- Never merge rooms solely because XZ overlaps: require compatible height/level and identity; preserve polygon outlines rather than converting genuine unions into arbitrary inflated rectangles.
- Match ceilings to the appropriate room/storey; do not erase floor elevations or clamp genuine house geometry into a universal 2.4 m shell.
- Room lookup/naming uses polygon membership plus level/height. Cross-level connectivity is explicitly typed stairs/transition data; do not infer doors between vertically stacked rooms or mistake tracking jumps for stair movement.
- Persist measured provenance; prohibit growth/CloseGaps for measured geometry after reload. Keep user-created estimates separate.
- Partial rescans update matched room revisions and retain rooms not currently returned. Explicit room deletion/reset is confirmed, not triggered by transient provider removals.

## 5. Separate house capture from chore sessions
**Files:** HouseScanController, HouseScanSessionController, QuestScanSource, ScanFlowController, DoorGlow, AutoScanDirector, HooverSceneBootstrapper, HouseScanPanel, input routing.
**Dependencies:** Steps 2–4.
- One controller owns scene-capture requests, permission readiness, pause/resume reload and commit. DoorGlow becomes a visualization/compatibility adapter.
- States: unavailable/permission-needed, ready, requesting Space Setup, suspended, importing, reviewing, committed/partial, error. Starting/stopping is idempotent.
- Scan/append room repeatedly without starting gameplay or clearing the house. Explicit finish/save; hardware/resource timeouts produce recoverable partial/error states, never artificial whole-house success.
- Retain a clearly labelled fallback only for gameplay compatibility. A guessed room cannot count as a successful house scan or measured export.
- Existing gaze targets and typed intents call the shared controller. Voice/request-file adapters do likewise. Ensure operation is usable without PC relay.

## 6. Persist and restore actual geometry
**Files:** HouseGeometryData/Store, HouseMapStore, controller and mesh facade.
**Dependencies:** Steps 2–5.
- Versioned manifest plus per-room/chunk topology files containing positions, triangle indices, optional normals/submeshes and documented transforms. Preserve untextured measured furniture geometry only where the device actually supplies it.
- Write temporary files and publish completed revisions atomically with recoverable previous state; validate schema, sizes, finite coordinates, indices and integrity on read.
- Save committed revisions on demand and pause; do not block on one gigantic whole-house JSON/OBJ allocation.
- Handle disk full, interruption, corrupt chunks and version migration without destroying previous rooms or player_profile.json. Local-only storage; no camera images, automatic network transfer or source-control inclusion of house data.

## 7. Display the same house mesh at miniature and full scale
**Files:** HouseMeshPresenter, ScanToDollhousePipeline, HouseMapPresenter, RoomOutlines, HouseScanPanel/PlaceholderHud, material.
**Dependencies:** Steps 4–6.
- Measured mesh chunks are the visual source of truth; legacy shells remain labelled schematic/estimated only. Use one dataset for miniature and full-scale views without changing stored vertices.
- Level filters/exploded-level inspection, room names, measured bounds and connected-room/stair metadata. Rebuild on geometry revision/polygon/height change, not bounds alone.
- Use neutral readable Built-in materials and normals; do not promise photo textures or watertight reconstruction. Ceiling cutaway/level filters must not destructively edit source meshes.
- Full-scale inspection supports stationary room viewpoints using a separate presentation transform. Physical passthrough scan mode does not render opaque walls across the user's real surroundings.
- Keep coverage visualization separate and level-aware at room selection boundaries; preserve existing chores. Optional colliders only for inspection needs; do not bake collision for every update or add AI navigation.

## 8. Export, document and verify the complete house
**Files:** HouseGeometryExporter, SpatialMeshTracker legacy export adapter, docs and tests.
**Dependencies:** Steps 6–7.
- Export only validated committed measured geometry with invariant-culture numbers, independent vertex/normal offsets, correct winding/transform conversion, named objects, units and level/room metadata.
- Specify Unity-to-OBJ coordinate conventions and verify reimport. Include source/provenance for simplified or estimated data; exclude estimates from default measured export.
- Display local path and truthful triangle/room counts. Document explicit Quest file retrieval without hardcoded device serials, uploads or photo capture.
- Update outdated documentation: installed provider is already present; Space Setup/permissions and cross-session localization remain required.

# Verification & Testing
## Automated
- Baseline all existing EditMode tests; record actual results, do not rely on historical handover pass counts.
- Scan state tests: zero geometry never successful; permission denied/pending/granted-late; scene request refusal; suspend/resume/cancel; unchanged counts with changed topology; repeated start/stop; partial captures; late callbacks; no auto-finish from settling.
- Coordinate tests: translated/rotated XROrigin, nonzero floor offset, mesh source transforms, saved/reloaded anchor pose, recenter, tracking loss; canonical round-trip within 1 mm synthetic tolerance.
- Multi-storey fixtures: identical XZ rooms at Y=0 and Y=3 remain distinct; correct ceiling/door associations; stairs explicit; polygon room lookup; source id persistence and recapture reassociation; no measured growth after restart.
- Persistence tests: topology/metadata round-trip, legacy data preservation, incomplete commit, corrupt/missing chunks, disk failure simulation, nonfinite values/out-of-range indices, revision replacement, partial-room retention.
- Presenter tests: mesh-backed room count, level filtering, geometry update with identical bounds, height/polygon updates, miniature/full-scale transform equivalence, neutral material and source geometry unchanged.
- OBJ tests: multiple chunks with/without normals, non-English locale, winding/axis convention, independent offsets, export/reimport elevations and geometry counts, no empty success.
- Regression tests: HouseMap/Driver/Presenter/Store, MiniatureRoom, CeilingMath, RoomScanCorners, FloorGrid, gaze/input and mode scoring.

## Physical Quest checkpoints
1. Known room: headset shows actual permission/session/provider status and nonzero measured geometry; no guessed-room success.
2. Space Setup canceled and completed: app resumes safely and refreshes data, retains previous capture.
3. Two adjoining rooms: both remain present with correct relative placement; record exactly which scene rooms the device exposes.
4. Two storeys with overlapping footprints: distinct rooms and elevations, explicit stair connectivity; verify while stationary at each landing. Never disable safety boundaries to test.
5. Restart/recenter and revisit: localized geometry stays consistent; unresolved alignment blocks AR placement/merge. Suggested prototype target: <=10 cm positional and <=3 degrees yaw error at independently checked room landmarks. Treat this as an acceptance target, not guaranteed headset accuracy; report residuals and revise strategy if unmet.
6. Revisit/update one room: no duplicate house, no disappearing unrelated room, no unintended measured-room stretching.
7. Dollhouse/full-scale inspection and local export: inspect matching shape/elevations, import OBJ on PC and compare counts/units/room placement.
8. Performance: report Quest frame time, heap/native mesh memory and storage at actual house scale; target 72 Hz (13.9 ms/frame) with bounded processing. Profile dense datasets before adding detail; reduce display/collider cost, not saved measurement accuracy without consent.

## Build and reporting
Use the existing QuestBuild workflow after reading its current implementation. If batch Unity is needed, do not start a second editor on the same project; coordinate closing the GUI first. Use existing device-discovery/redeployment scripts only after reading them and identifying the actual Quest. No automatic settings reset, unrelated package upgrade, SDK guard bypass, git commit or push is part of this approval.

**Definition of done:** multiple measured rooms on multiple levels can be captured incrementally, restored with verified spatial alignment, reviewed from their actual persisted mesh, and exported locally. Failures show evidence and actionable recovery instead of claiming completion. Hardware checkpoints and actual test/build results must accompany any claim that whole-house scanning now works.