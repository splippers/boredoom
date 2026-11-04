# June 2026 generated media: what exists and where it could go

Written by: Claude (AI). Passenger: Jon Davis (directed and tested; did not write this code).

## Where the media is
- Original: Slartibartfast (Windows) at Documents\Projects\BoreDOOM!\ (GeneratedAssets, Assets\Audio, Sprites, Models, Materials, Fonts). Shared as SMB share "Jon".
- Local copy on LAVE: `/home/jon/boredoom-june-media/` (about 255 MB, NOT in git). `INDEX.md` there lists the 79 generated files with their prompt and model. Folders: `audio` (11 SFX), `music` (7 tracks), `images` (54), `models` (7 GLB), and `named/` (the files as the June game named them: audio, images, models, fonts, materials).
- Do not copy `WIKI.md` from the June project: it contains a live API token and a Supabase address.
- Generators used (check each service's terms before shipping): ElevenLabs SFX v2, Google Lyria 3, Gemini 3.1 Flash, Flux 2, Tripo.

## Current game structure (this repo, Assets/Chorewars/Scripts)
Runtime/ (feel + scan + dollhouse), Modes/ (16 mode controllers, ChoreModeRegistry), HandsFree/ (gaze, nod/shake, voice router), Voice/ (Claudia link, PCM), AR/ (scene surfaces, door glow, scan readout), UI/ (HUD, mode picker, session summary, scan panel), Orac/, Core/ (scoring, power-ups, sessions).
Today the game has NO audio files. Its only sound is a generated tick in `Runtime/ChoreJuice.cs`. Text uses the built-in font. There is no Resources folder for media and no sprites or 3D models.

## Where each asset could slot in (suggestions, Unity AI to confirm)
| Asset | Hook in current code |
|---|---|
| Hoover_PowerUp (SFX) | Hoover start / turbo, `RuntimeHooverMode`, `PowerUpSystem` |
| SFX_Rescue_Success, success chime | `RoomCleanCelebration` (room cleaned), `SessionSummaryUI` |
| SFX_Laundry_Cycle, SFX_Washing_Crockery | Laundry / dishes modes (`RuntimeDeclutterDashMode`, kitchen chores) |
| SFX_DustBunny_EvilGiggle | Missed-spot hint / Chaos Assessment (`MissedSpotHint`, `RuntimeChaosAssessmentMode`) |
| DoorCreak, stone-grinding hum | Doorway found (`DoorGlow`) and room unlocked |
| SFX_FinnMcCool_Enter | Companion/boss intro (Chore Wars battle mode) |
| Combat themes (CombatTheme, SynthSlayer x3, Tropical_Treasure, Dizzy_Style) + Lyria MP3s | Per-mode background music; the old game ramped pitch 1.0x to 1.3x with combo |
| BoreDOOM logo, IV logo, Victory_Splash, "CHORE ACCOMPLISHED" | Title / `SessionSummaryUI` |
| HUD scanner reticle, targeting brackets, Entity tag backgrounds | Scan readout, gaze dwell ring (`GazeDwellSelector`), `ScanReadout`, `HouseScanPanel` |
| MetalMania font | Title and headline text only (the old game limited it to size 36+) |
| GLBs: Battle Vacuum, Plasma Duster, Doric column, Grand Door | Tool visuals, dollhouse decor, doorway art on `DoorGlow` |
| Floor / basalt / industrial material maps | `ARFloorOverlay` / `CoverageFloorVisualizer` skins |

Notes: the June audio is full-length WAV (up to 31 MB). Convert to compressed Ogg or MP3 for the Quest before building (APK is ~40 MB now). Four June files were 46-byte empty stubs and were skipped. Keep passthrough clear: no opaque full-screen art.

## Suggested order for Unity AI
1. Add an AudioManager (load by name from Resources) and wire 5 SFX: powerup, success, door, giggle, tick.
2. Per-mode music with a volume that stays low under Claudia's voice.
3. Logo + HUD sprites on the existing world-space panels.
4. Models and floor skins last (size and Quest performance).
