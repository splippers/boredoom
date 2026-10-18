using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Chorewars.AR;
using Chorewars.Core;
using Chorewars.Dollhouse;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Builds the whole Hoover loop at runtime, so no scene authoring is required:
    /// room provider -> coverage map -> placeholder room/floor -> tracked tool -> mode -> HUD.
    /// Uses a fake room in the editor and the spatial-mesh provider on device. A second camera
    /// is never created (QuestXrRigBootstrap owns the rig).
    /// </summary>
    public sealed class HooverSceneBootstrapper : MonoBehaviour
    {
        private const string LogPrefix = "BoreDoom/HooverBootstrap";
        private static readonly string[] ModeScenes = { "HooverMode" };

        [SerializeField] private float timeLimitSeconds = 90f;
        [SerializeField] private Vector2 editorRoomSizeMeters = new Vector2(4f, 4f);
        [SerializeField] private float toolHeightMeters = 0.9f;
        [SerializeField] private float toolSpeedMetersPerSecond = 1.6f;

        // Placeholder eye for the photo bake: about standing height, with a lens wide enough to see
        // the floor around the tool. Both stand in for the headset and are expected to change with it.
        [SerializeField] private float photoEyeHeightMeters = 1.55f;
        [SerializeField] private float photoVerticalFieldOfViewDegrees = 65f;

        private static HooverSceneBootstrapper _instance;

        /// <summary>
        /// The house map for the running scene, or null before the scene is built. Callers that can
        /// name a room (voice, the gaze-dwell list) reach the map through here.
        /// </summary>
        public static HouseMapDriver HouseMap => _instance != null ? _instance._mapDriver : null;

        private RuntimeCoverageMode _mode;
        private string _pendingScene;
        private float _giveUpAt;
        private Camera _camera;
        private bool _built;

        // Everything the mode needs is built up-front and held here, so the mode itself can be stood
        // up later, at the moment the player picks one from the menu.
        private Transform _runtimeRoot;
        private PlaceholderHud _hud;
        private CoverageMap _coverageMap;
        private CoverageFloorVisualizer _floorVisualizer;
        private PhotoBakeDriver _bakeDriver;
        private Transform _tool;
        private PathTracker _pathTracker;
        private PlaceholderToolDriver _toolDriver;
        private SpatialMeshTracker _meshTracker;
        private RoomCleanCelebration _celebration;
        private DollhouseManager _dollhouse;
        private Transform _dollhouseRoot;
        private ScanToDollhousePipeline _pipeline;
        private DollhousePresenter _presenter;
        private HouseMapDriver _mapDriver;
        private IScanSource _scanSource;
        private Bounds _bounds;
        private string _sourceLabel;
        private PlaceholderRoom _room;

        // True when the play area had to be guessed at startup because the scan had not run yet
        // (the normal case on device). The guess is replaced once the scan reports a real extent.
        private bool _playAreaFromGuess;
        private bool _playAreaResolved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject(nameof(HooverSceneBootstrapper));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<HooverSceneBootstrapper>();
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void Start() => Queue(SceneManager.GetActiveScene());

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Queue(scene);

        private void Queue(Scene scene)
        {
            if (Array.IndexOf(ModeScenes, scene.name) < 0) return;
            if (_built || _mode != null) return;

            _pendingScene = scene.name;
            _giveUpAt = Time.unscaledTime + 5f;
            _camera = null;
        }

        private void Update()
        {
            if (_pendingScene == null) return;

            if (_camera == null) _camera = FindCamera();
            if (_camera == null)
            {
                if (Time.unscaledTime <= _giveUpAt) return;

                Debug.LogWarning($"{LogPrefix}: no camera appeared in '{_pendingScene}' after 5s; skipping build.");
                _pendingScene = null;
                return;
            }

            var scene = _pendingScene;
            _pendingScene = null;

            try
            {
                Build(scene, _camera);
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogPrefix}: build failed for '{scene}' — {e}");
            }
        }

        private void Build(string sceneName, Camera camera)
        {
            // Root lives in the active scene (not DontDestroyOnLoad) so it is torn down with the scene.
            var root = new GameObject($"HooverRuntime ({sceneName})").transform;

            // 1) Room provider (fallback play area when a scan cannot supply bounds yet).
            var providerGo = new GameObject("RoomProvider");
            providerGo.transform.SetParent(root, false);

            IRoomProvider provider;
            SpatialMeshTracker meshTracker = null;

            if (Application.isEditor)
            {
                var fakeProvider = providerGo.AddComponent<FakeRoomProvider>();
                fakeProvider.Configure(editorRoomSizeMeters, Vector2.zero, 0f);
                provider = fakeProvider;
            }
            else
            {
                var trackerGo = new GameObject("SpatialMeshTracker");
                trackerGo.transform.SetParent(root, false);
                meshTracker = trackerGo.AddComponent<SpatialMeshTracker>();

                var spatial = providerGo.AddComponent<SpatialRoomProvider>();
                spatial.Configure(trackerGo.transform);
                provider = spatial;
            }

            // 2) Scan source: simulated rooms in the editor, real mesh scan on device.
            var scanGo = new GameObject("ScanSource");
            scanGo.transform.SetParent(root, false);

            IScanSource scanSource;
            if (Application.isEditor)
            {
                scanSource = scanGo.AddComponent<FakeScanSource>();
            }
            else
            {
                var sceneReader = scanGo.AddComponent<SceneSurfaceReader>();
                var questScan = scanGo.AddComponent<QuestScanSource>();
                questScan.Configure(meshTracker, sceneReader);
                scanSource = questScan;
            }

            // Prefer the scan's known extent; fall back to the room provider.
            var scannedBounds = scanSource.KnownBounds;
            var bounds = scannedBounds ?? provider.RoomBounds;
            var source = scannedBounds != null ? "Scan" : provider.Source;

            // Free roaming: on the headset the player walks the whole house, so there must be no edge to reach.
            if (!Application.isEditor) bounds = RoamingPlayArea.EnsureRoaming(bounds);

            // A scan that has not run cannot know the room, and on device that is the normal case:
            // the play area starts as the room provider's guess and is replaced by the real extent
            // once the scan finishes (see OnScanAreaResolved).
            _playAreaFromGuess = scannedBounds == null;

            Debug.Log($"{LogPrefix}: play area source={source}, size={bounds.size.x:0.0}x{bounds.size.z:0.0} m{(_playAreaFromGuess ? " (a guess — no scan yet)" : "")}");

            // 2) Coverage map, given the real play-area bounds (the ±2 m default makes % meaningless).
            var mapGo = new GameObject("CoverageMap");
            mapGo.transform.SetParent(root, false);
            var coverageMap = mapGo.AddComponent<CoverageMap>();
            coverageMap.SetBounds(new Vector2(bounds.min.x, bounds.min.z), new Vector2(bounds.max.x, bounds.max.z));

            // 3) Placeholder room + cleaned-floor visualiser.
            var roomGo = new GameObject("PlaceholderRoom");
            roomGo.transform.SetParent(root, false);
            var room = roomGo.AddComponent<PlaceholderRoom>();
            room.Build(bounds);

            var floorVisualizer = roomGo.AddComponent<CoverageFloorVisualizer>();
            floorVisualizer.Build(bounds, room.FloorRenderer, RoamingPlayArea.FloorResolution(bounds));

            // 4) The tracked tool (placeholder "vacuum").
            var tool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tool.name = "HooverTool";
            tool.transform.SetParent(root, false);
            tool.transform.position = new Vector3(bounds.center.x, toolHeightMeters, bounds.center.z);
            tool.transform.localScale = new Vector3(0.3f, 0.05f, 0.3f);

            var toolCollider = tool.GetComponent<Collider>();
            if (toolCollider != null) Destroy(toolCollider);

            var toolRenderer = tool.GetComponent<Renderer>();
            if (toolRenderer != null)
            {
                toolRenderer.sharedMaterial = PlaceholderMaterials.CreateOpaque(new Color(1f, 0.55f, 0.1f));

                // On the headset the puck only floats in the room and confuses a first look at the game, so it
                // is not drawn there for now. The tool still moves and still paints coverage; only its mesh is off.
                if (!Application.isEditor) toolRenderer.enabled = false;
            }

            var pathTracker = tool.AddComponent<PathTracker>();
            var toolDriver = tool.AddComponent<PlaceholderToolDriver>();
            toolDriver.Configure(FindRightController(), bounds, toolSpeedMetersPerSecond, toolHeightMeters);

            // 4b) Pseudo-photogrammetry: cleaned floor shows what the camera saw rather than a flat
            //     tint. Presentation only, and safe to lose — with no frame source the bake does not
            //     run and the floor behaves exactly as it did before this layer existed.
            var bakeGo = new GameObject("PhotoBake");
            bakeGo.transform.SetParent(root, false);
            var bakeDriver = bakeGo.AddComponent<PhotoBakeDriver>();
            bakeDriver.Configure(CreateFrameSource(tool.transform, bounds), floorVisualizer, tool.transform);

            // 5) The mode is not built yet. The player picks one from the menu below, so the room,
            //    the scan and the dollhouse are all built first and then a mode is stood up on demand.

            // 6) Placeholder HUD + summary.
            var hudGo = new GameObject("PlaceholderHud");
            hudGo.transform.SetParent(root, false);
            var hud = hudGo.AddComponent<PlaceholderHud>();
            hud.Build(camera);

            // 6b) Juice: puffs + streak + procedural ticks for every newly cleaned cell.
            var juice = roomGo.AddComponent<ChoreJuice>();
            juice.Configure(coverageMap, hud, bounds.min.y);

            // 6c) Missed-spot hint: faint pulsing marker over the biggest uncleaned patch.
            var hint = roomGo.AddComponent<MissedSpotHint>();
            hint.Configure(coverageMap, camera, bounds.min.y);

            // 7) Dollhouse + scan -> dollhouse pipeline (placeholder clean/dirty materials).
            var dollhouseGo = new GameObject("Dollhouse");
            dollhouseGo.transform.SetParent(root, false);
            var dollhouse = dollhouseGo.AddComponent<DollhouseManager>();
            dollhouse.ConfigureMaterials(
                // A sandy brown that reads against a dark room (the old dark grey vanished); it turns green as the room is cleaned.
                PlaceholderMaterials.CreateOpaque(new Color(0.66f, 0.52f, 0.32f)),
                PlaceholderMaterials.CreateOpaque(new Color(0.30f, 0.85f, 0.45f)));

            var pipeline = dollhouseGo.AddComponent<ScanToDollhousePipeline>();
            pipeline.Configure(dollhouse, coverageMap, floorVisualizer);

            var presenter = dollhouseGo.AddComponent<DollhousePresenter>();

            // 7a) House map: the rooms the player named and the doors they walked through, drawn on
            // the miniature. The doors come from the player's own walk (the camera is the head); the
            // tool's path is useless here because it only cleans the floor in front of them.
            var mapPresenter = dollhouseGo.AddComponent<HouseMapPresenter>();
            mapPresenter.Configure(dollhouse, camera, dollhouseGo.transform);

            var mapDriver = dollhouseGo.AddComponent<HouseMapDriver>();
            mapDriver.Configure(pipeline, mapPresenter, camera.transform);

            // 7a') The rooms drawn in the real room as glowing outlines, so the player sees what the map knows.
            var outlinesGo = new GameObject("RoomOutlines");
            outlinesGo.transform.SetParent(root, false);
            outlinesGo.AddComponent<RoomOutlines>().Configure(mapDriver, camera.transform);

            // 7a'') Scan a room by looking at its ceiling corners (started by a request file).
            outlinesGo.AddComponent<CeilingScanner>().Configure(mapDriver, camera.transform);

            // 7b) Room-clean celebration: glow + confetti + floating points + haptics + slow-mo.
            var celebration = dollhouseGo.AddComponent<RoomCleanCelebration>();
            celebration.Configure(dollhouse, camera);

            // 7c) Player profile singleton: lifetime stats, achievements and personal bests. Nothing
            // in the project instantiates it, so without this the end-of-session compare has no data.
            if (PlayerProfile.Instance == null)
            {
                var profileGo = new GameObject("PlayerProfile");
                profileGo.transform.SetParent(root, false);
                profileGo.AddComponent<PlayerProfile>();
            }

            // 8) Hold on to everything the chosen mode will need, then show the chore menu.
            _runtimeRoot = root;
            _hud = hud;
            _coverageMap = coverageMap;
            _floorVisualizer = floorVisualizer;
            _tool = tool.transform;
            _pathTracker = pathTracker;
            _toolDriver = toolDriver;
            _meshTracker = meshTracker;
            _celebration = celebration;
            _dollhouse = dollhouse;
            _dollhouseRoot = dollhouseGo.transform;
            _pipeline = pipeline;
            _presenter = presenter;
            _mapDriver = mapDriver;
            _scanSource = scanSource;
            _bounds = bounds;
            _sourceLabel = source;
            _room = room;
            _bakeDriver = bakeDriver;
            _camera = camera;

            hud.ModeSelected -= OnModeSelected;
            hud.ModeSelected += OnModeSelected;
            hud.SetChorePanelVisible(false);
            hud.ShowModePanel("CHOOSE YOUR CHORE", "Pick a chore, then map the room.");

            Debug.Log($"{LogPrefix}: runtime ready — {RuntimeModeCatalog.Modes.Count} modes available (limit {timeLimitSeconds:0}s).");
            _built = true;
        }

        /// <summary>
        /// Stands up the chosen mode, wires it to the room that is already built, and starts the scan
        /// flow. One mode per scene build: the room, dollhouse and scan are shared, so changing chore
        /// means reloading.
        /// </summary>
        private void OnModeSelected(string modeId)
        {
            if (_mode != null) return;

            if (!RuntimeModeCatalog.TryGet(modeId, out var info))
            {
                Debug.LogWarning($"{LogPrefix}: unknown mode '{modeId}'.");
                return;
            }

            var mode = info.Build();
            mode.transform.SetParent(_runtimeRoot, false);
            mode.chore = RuntimeModeCatalog.CreateChore(info, 80f);
            mode.trackedTool = _tool;
            mode.coverageMap = _coverageMap;
            mode.pathTracker = _pathTracker;
            mode.spatialMeshTracker = _meshTracker;
            mode.ConfigureRuntime(_hud, _floorVisualizer, _toolDriver, _bounds, timeLimitSeconds);
            mode.celebration = _celebration;
            mode.dollhouse = _dollhouse;
            mode.pipeline = _pipeline;
            _mode = mode;

            _hud.HideModePanel();
            _hud.SetSource($"{_sourceLabel} | {_bounds.size.x:0.0}x{_bounds.size.z:0.0} m | {info.DisplayName} | WASD move, E end, J juice");

            var flowGo = new GameObject("ScanFlow");
            flowGo.transform.SetParent(_runtimeRoot, false);
            var flow = flowGo.AddComponent<ScanFlowController>();
            flow.Configure(_hud, _scanSource, _pipeline, _presenter, _dollhouseRoot, mode, _camera);
            flow.ScanAreaResolved = OnScanAreaResolved;

            Debug.Log($"{LogPrefix}: '{info.DisplayName}' selected — awaiting room scan.");
        }

        /// <summary>
        /// Replaces the guessed play area with the extent the scan actually found. Everything the
        /// chore is played on is sized from these bounds — the coverage map that scores it, the floor
        /// the tool paints, the placeholder room, the tool's own movement bounds, and each mode's
        /// geometry — and on device none of it can be known until the scan runs, so the whole play
        /// area is rebuilt here rather than trusted from startup. The mode has not begun yet, which
        /// is what makes rebuilding it safe: it builds its geometry afterwards, from these bounds.
        /// </summary>
        private void OnScanAreaResolved(Bounds scanned)
        {
            if (!_playAreaFromGuess || _playAreaResolved) return;
            if (!Application.isEditor) scanned = RoamingPlayArea.EnsureRoaming(scanned);
            if (_mode == null || !_mode.TryUpdatePlayBounds(scanned)) return;

            _playAreaResolved = true;
            _bounds = scanned;

            _coverageMap.SetBounds(
                new Vector2(scanned.min.x, scanned.min.z),
                new Vector2(scanned.max.x, scanned.max.z));

            _room.Build(scanned);
            _floorVisualizer.Build(scanned, _room.FloorRenderer, RoamingPlayArea.FloorResolution(scanned));

            // Rebuilding the visualiser replaced its texture, so the bake has to be pointed at the
            // new one and at the new floor height.
            if (_bakeDriver != null)
                _bakeDriver.Configure(CreateFrameSource(_tool, scanned), _floorVisualizer, _tool);

            // The tool's own bounds only drive keyboard movement in the editor; on device it follows
            // the controller. Keep them honest all the same.
            _toolDriver.Configure(FindRightController(), scanned, toolSpeedMetersPerSecond, toolHeightMeters);

            _hud.SetSource($"{_sourceLabel} | {scanned.size.x:0.0}x{scanned.size.z:0.0} m (scanned) | WASD move, E end, J juice");

            Debug.Log($"{LogPrefix}: play area re-derived from the scan — " +
                      $"{scanned.size.x:0.0}x{scanned.size.z:0.0} m at ({scanned.center.x:0.0}, {scanned.center.z:0.0}).");
        }

        /// <summary>
        /// The camera the photo bake photographs the floor with, or null when there is none.
        /// </summary>
        /// <remarks>
        /// In the editor this is a placeholder eye held above the tool, looking down. On device it
        /// returns null until the headset's frames are wired in behind the same interface — which is
        /// why the layer is allowed to be absent rather than assumed. Nothing downstream branches on
        /// whether a photograph exists; it just does not get one.
        /// </remarks>
        private ISurfaceFrameSource CreateFrameSource(Transform tool, Bounds bounds)
        {
            if (!Application.isEditor) return null;

            return new SyntheticFrameSource(
                tool,
                new Vector3(0f, photoEyeHeightMeters, 0f),
                photoVerticalFieldOfViewDegrees,
                1f,
                bounds.min.y);
        }

        private static Camera FindCamera()
        {
            var main = Camera.main;
            if (main != null) return main;

            foreach (var candidate in FindObjectsByType<Camera>())
                if (candidate != null && candidate.isActiveAndEnabled) return candidate;

            return null;
        }

        private static Transform FindRightController()
        {
            foreach (var candidate in FindObjectsByType<Transform>())
            {
                if (candidate == null) continue;

                var name = candidate.name;
                if (name.Contains("RightHand") || name.Contains("Right Hand") || name.Contains("Right Controller"))
                    return candidate;
            }

            return null;
        }
    }
}
