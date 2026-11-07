# Clean BoreDOOM intro and dollhouse

Build with **BoreDOOM > Quest > Build clean intro and dollhouse** (`QuestBuild.BuildCleanApk`). Output: `Builds/BoreDOOM-CleanIntro.apk`.

The APK includes only `Intro` and `DollhouseSetup`. Intro references the imported `BoreDOOM_Logo.png` and stereo `BoreDOOM_CombatTheme.wav` directly. Continue fades the music and opens setup; Back returns to intro. Mute is available on the intro.

Setup reads Meta's already-saved Space Setup floor/wall polygons through AR Foundation. It does not perform a BoreDOOM scan, restore house.json, grow rooms, acquire camera images or start Claudia/PC voice/microphone capture. Spatial consent is requested when entering setup. Complete Space Setup in the headset's own Settings if no floors are returned. The miniature is a measured-polygon schematic, not a photogrammetric reconstruction. Level selection preserves floor elevation; Recenter affects the miniature and menu only, never the live XR origin.

The clean build temporarily disables Meta camera-image support while building, preserving passthrough and restoring the prior editor setting afterward. Legacy development scenes and code remain available, but their automatic systems are guarded in clean scenes.

## Fresh device installation

1. Build successfully and inspect the artifact before deleting the old app.
2. Use `tools/quest-device.sh` to identify the actual Quest and query installed BoreDOOM package ids. Do not uninstall unrelated games whose names contain 'doom'.
3. Uninstall each confirmed BoreDOOM package **without retaining its data**. `com.boredoom.chorewars` is the current package. This removes its profile/preferences/saved house data; Meta Space Setup remains device-wide and must not be cleared.
4. Install `Builds/BoreDOOM-CleanIntro.apk` fresh (not an update install), compare the installed APK hash with the local artifact and launch it.
5. Confirm intro logo/music/mute, Continue and fade, dollhouse levels/recenter and clear passthrough. Logs should show `CleanIntro`/`CleanSetup`, with no Claudia/voice/house-scan initialization.

## Palm/audio update

The dollhouse now stays hidden until the left palm is held face-up. Lowering the palm or losing tracking hides it. Right-hand pinch plus sideways drag turns the house; pinch and pull toward the body zooms. Pinching with both hands and spreading/closing them changes zoom, while turning the hand-to-hand axis rotates it. Zoom is bounded to 0.4–2.5x of the 0.30 m default model extent. Recenter resets the hand-controlled view. Room-data readiness and palm reveal are separate gates, so receiving fresh room data does not force the house visible.

The selected intro clip now preloads, is explicitly loaded before playback, and resumes after headset focus returns. `CleanIntro` logs report actual loading/playing/sample cursor/volume/mute state; these do not substitute for a listener's confirmation of audible sound. `PalmMiniature` logs report tracking connection and actual reveal/hide transitions.

Build this revision with `QuestBuild.BuildCleanApk("Builds/BoreDOOM-PalmAudio.apk")`. Install it as an update to the already-clean app; do not repeat the destructive fresh-install steps or clear current permissions/data.

Slime, vacuum recognition/running-noise detection and clutter outlines are not included in this milestone. No other June media variants, generated models or new GLB importer are needed.
