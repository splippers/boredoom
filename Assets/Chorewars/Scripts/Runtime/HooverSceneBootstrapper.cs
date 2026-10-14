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

        private static HooverSceneBootstrapper _instance;

        private RuntimeHooverMode _mode;
        private string _pendingScene;
        private float _giveUpAt;
        private Camera _camera;

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
            if (_mode != null) return;

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
                var questScan = scanGo.AddComponent<QuestScanSource>();
                questScan.Configure(meshTracker);
                scanSource = questScan;
            }

            // Prefer the scan's known extent; fall back to the room provider.
            var scannedBounds = scanSource.KnownBounds;
            var bounds = scannedBounds ?? provider.RoomBounds;
            var source = scannedBounds != null ? "Scan" : provider.Source;
            Debug.Log($"{LogPrefix}: play area source={source}, size={bounds.size.x:0.0}x{bounds.size.z:0.0} m");

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
            floorVisualizer.Build(bounds, room.FloorRenderer);

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
                toolRenderer.sharedMaterial = PlaceholderMaterials.CreateOpaque(new Color(1f, 0.55f, 0.1f));

            var pathTracker = tool.AddComponent<PathTracker>();
            var toolDriver = tool.AddComponent<PlaceholderToolDriver>();
            toolDriver.Configure(FindRightController(), bounds, toolSpeedMetersPerSecond, toolHeightMeters);

            // 5) The mode itself.
            var chore = ScriptableObject.CreateInstance<ChoreDefinition>();
            chore.choreId = "hoover";
            chore.displayName = "Standard Coverage";
            chore.type = ChoreType.Hoovering;
            chore.targetCoveragePercent = 80f;

            var modeGo = new GameObject("RuntimeHooverMode");
            modeGo.transform.SetParent(root, false);
            var mode = modeGo.AddComponent<RuntimeHooverMode>();
            mode.chore = chore;
            mode.trackedTool = tool.transform;
            mode.coverageMap = coverageMap;
            mode.pathTracker = pathTracker;
            mode.spatialMeshTracker = meshTracker;

            // 6) Placeholder HUD + summary.
            var hudGo = new GameObject("PlaceholderHud");
            hudGo.transform.SetParent(root, false);
            var hud = hudGo.AddComponent<PlaceholderHud>();
            hud.Build(camera);
            hud.SetSource($"{source} | {bounds.size.x:0.0}x{bounds.size.z:0.0} m | WASD or arrows to hoover, E to end");

            // 7) Dollhouse + scan -> dollhouse pipeline (placeholder clean/dirty materials).
            var dollhouseGo = new GameObject("Dollhouse");
            dollhouseGo.transform.SetParent(root, false);
            var dollhouse = dollhouseGo.AddComponent<DollhouseManager>();
            dollhouse.ConfigureMaterials(
                PlaceholderMaterials.CreateOpaque(new Color(0.33f, 0.31f, 0.29f)),
                PlaceholderMaterials.CreateOpaque(new Color(0.30f, 0.85f, 0.45f)));

            var pipeline = dollhouseGo.AddComponent<ScanToDollhousePipeline>();
            pipeline.Configure(dollhouse, coverageMap);

            var presenter = dollhouseGo.AddComponent<DollhousePresenter>();

            // 8) Flow: scan (button + progress + done), dollhouse presented, then chore begins.
            mode.ConfigureRuntime(hud, floorVisualizer, toolDriver, timeLimitSeconds);

            var flowGo = new GameObject("ScanFlow");
            flowGo.transform.SetParent(root, false);
            var flow = flowGo.AddComponent<ScanFlowController>();
            flow.Configure(hud, scanSource, pipeline, presenter, dollhouseGo.transform, mode, camera);

            _mode = mode;

            Debug.Log($"{LogPrefix}: Hoover runtime ready — awaiting room scan (limit {timeLimitSeconds:0}s).");
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
