using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Chorewars.Diagnostics
{
    /// <summary>Logs the state that decides whether passthrough shows (AR session, camera manager, camera clear colour, scene) whenever any of it changes.</summary>
    public sealed class PassthroughWatch : MonoBehaviour
    {
        string _last;
        float _next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!AndroidPermissions.IsDevice || FindFirstObjectByType<PassthroughWatch>() != null) return;
            var go = new GameObject(nameof(PassthroughWatch));
            DontDestroyOnLoad(go);
            go.AddComponent<PassthroughWatch>();
        }

        void OnEnable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

        static bool Flag(string name) => System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, name));

        // Loading a scene was found to drop the headset's passthrough layer while the AR session still reports tracking.
        // Restart the camera manager (and, with the test flag, the whole AR session) just after the load to bring it back.
        void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (Flag("bisect-norecover") || Time.frameCount < 10) return;
            StartCoroutine(Recover(scene.name));
        }

        IEnumerator Recover(string sceneName)
        {
            yield return null; yield return null;
            var mgr = FindFirstObjectByType<ARCameraManager>();
            var session = FindFirstObjectByType<ARSession>();
            bool full = Flag("bisect-session");
            if (full && session != null) session.enabled = false;
            if (mgr != null) mgr.enabled = false;
            yield return null; yield return null;
            if (mgr != null) mgr.enabled = true;
            if (full && session != null) session.enabled = true;
            TelemetryLogger.Info("PassthroughWatch", $"recovery after loading {sceneName}: camera manager restarted{(full ? " with the AR session" : "")}.");
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            var cam = Camera.main;
            var mgr = FindFirstObjectByType<ARCameraManager>();
            var session = FindFirstObjectByType<ARSession>();
            string state = $"scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} arState={ARSession.state} session={(session != null ? session.enabled.ToString() : "none")} camMgr={(mgr != null ? mgr.enabled.ToString() : "none")} cam={(cam != null ? cam.clearFlags + "/a" + cam.backgroundColor.a.ToString("0.0") + "/en=" + cam.enabled : "none")} planes={FindObjectsByType<ARPlaneManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length}";
            if (state == _last) return;
            _last = state;
            TelemetryLogger.Info("PassthroughWatch", state);
        }
    }
}
