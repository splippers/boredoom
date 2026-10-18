using System.IO;
using Chorewars.Diagnostics;
using UnityEngine;

namespace Chorewars.HandsFree
{
    /// <summary>
    /// The "bring the menu back" switch for panels that stay where they were opened.
    /// </summary>
    /// <remarks>
    /// An anchored panel is left behind when the player walks away, which is the point of roaming, so there
    /// has to be a way to call it back. <see cref="Recall"/> makes every anchored panel place itself afresh in
    /// front of the player. On a device, creating <c>bring-menu.request</c> in the app's files folder does the
    /// same (the voice link uses this when the player says "bring the menu").
    /// </remarks>
    public static class WorldAnchoredPanels
    {
        public const string RequestFileName = "bring-menu.request";

        /// <summary>Goes up by one on every recall; each anchored panel compares it to the last one it saw.</summary>
        public static int RecallCount { get; private set; }

        public static void Recall() => RecallCount++;

        sealed class Watcher : MonoBehaviour
        {
            float _nextPollAt;

            void Update()
            {
                if (Time.unscaledTime < _nextPollAt) return;
                _nextPollAt = Time.unscaledTime + 0.5f;

                var path = Path.Combine(Application.persistentDataPath, RequestFileName);
                if (!File.Exists(path)) return;

                try { File.Delete(path); } catch { /* recall anyway */ }
                Recall();
                TelemetryLogger.Info("Panels", "menus recalled to the player");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!AndroidPermissions.IsDevice) return;

            var go = new GameObject("WorldAnchoredPanelsWatcher");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Watcher>();
        }
    }
}
