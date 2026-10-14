using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Chorewars.Integration;
using UnityEngine;

namespace Chorewars.Dollhouse
{
    public class DollhouseManager : MonoBehaviour
    {
        /// <summary>Shader property used for the per-room tint (Unlit/Color and Standard both use it).</summary>
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Scale & Layout")]
        [SerializeField] private float scale = 0.05f;
        [SerializeField] private Vector3 dollhouseOrigin = new(0f, 0.1f, 0.5f);
        [SerializeField] private float roomSpacing = 0.01f;

        [Header("Materials")]
        [SerializeField] private Material dirtyMat;
        [SerializeField] private Material cleanMat;
        [SerializeField] private Material transitioningMat;
        [SerializeField] private float transitionDuration = 0.8f;

        [Header("Share Card")]
        [SerializeField] private Camera captureCamera;
        [SerializeField] private int captureWidth = 1080;
        [SerializeField] private int captureHeight = 1080;

        [Header("Network")]
        [SerializeField] private CraicKenReporter reporter;

        private readonly Dictionary<string, DollhouseRoom> _rooms = new();

        // Latest coverage per room, kept for the end-of-session summary so it can show a per-room
        // breakdown after play has finished.
        private readonly Dictionary<string, float> _roomFinalCoverage = new();

        private int _totalRooms;
        private int _cleanRooms;
        private bool _allCleanAnnounced;

        /// <summary>
        /// Raised once per room, the first time it reaches the clean threshold, with the room id and
        /// its miniature root transform. Lets presentation layers (celebration, audio, haptics) hang
        /// off a genuine event instead of polling coverage.
        /// </summary>
        public event Action<string, Transform> RoomCleaned;

        /// <summary>Final coverage percentage for each registered room (id → 0–100).</summary>
        public IReadOnlyDictionary<string, float> RoomFinalCoverage => _roomFinalCoverage;

        public Vector3 WorldToDollhouse(Vector3 worldPos) => dollhouseOrigin + worldPos * scale;

        /// <summary>
        /// Runtime configuration for the clean/dirty materials. When the dollhouse is built from
        /// code these serialized fields are null, which would make coverage tinting a no-op.
        /// </summary>
        public void ConfigureMaterials(Material dirty, Material clean, Material transitioning = null)
        {
            dirtyMat = dirty;
            cleanMat = clean;
            transitioningMat = transitioning;
        }

        private class DollhouseRoom
        {
            public string roomId;
            public List<Renderer> renderers = new();

            // The subset of renderers allowed to change colour as the room is cleaned. Not every
            // renderer in a room is fair game: a miniature's walls are a translucent shell you are
            // meant to see through, and restyling them with the opaque clean/dirty material hides
            // everything the room contains.
            public List<Renderer> tintRenderers = new();

            public float coveragePercent;
            public bool isClean;

            // One property block per room, reused for every renderer in it. A room is tinted as a
            // unit, so sharing the block is correct and avoids per-frame allocation.
            public readonly MaterialPropertyBlock Block = new();
        }

        public void RegisterRoom(string roomId, Renderer[] roomRenderers)
            => RegisterRoom(roomId, roomRenderers, roomRenderers);

        /// <summary>
        /// Registers a room, with an explicit list of the renderers whose appearance should track
        /// cleanliness. Everything in <paramref name="roomRenderers"/> counts as part of the room (it
        /// is what the celebration pulses); only <paramref name="tintRenderers"/> are restyled.
        /// </summary>
        public void RegisterRoom(string roomId, Renderer[] roomRenderers, Renderer[] tintRenderers)
        {
            if (_rooms.ContainsKey(roomId)) return;

            var room = new DollhouseRoom
            {
                roomId = roomId,
                renderers = new List<Renderer>(roomRenderers),
                tintRenderers = new List<Renderer>(tintRenderers ?? roomRenderers),
                isClean = false
            };
            _rooms[roomId] = room;
            _roomFinalCoverage[roomId] = 0f;
            _totalRooms++;

            SetRoomCleanVisual(room, 0f);
        }

        public void OnCoverageUpdate(string roomId, float coveragePct)
        {
            if (!_rooms.TryGetValue(roomId, out var room)) return;

            room.coveragePercent = coveragePct;
            _roomFinalCoverage[roomId] = coveragePct;

            if (coveragePct >= 95f && !room.isClean)
            {
                room.isClean = true;
                _cleanRooms++;
                StartCoroutine(AnimateRoomTransition(room, 0f, 1f));
                RoomCleaned?.Invoke(roomId, CommonRoot(room.renderers));
            }
            else if (!room.isClean)
            {
                SetRoomCleanVisual(room, coveragePct / 100f);
            }

            // Announce the fully-clean dollhouse exactly once. Without the guard this ran on every
            // coverage push (~4x per room per second), spawning a share-card coroutine each time.
            if (!_allCleanAnnounced && _totalRooms > 0 && _cleanRooms >= _totalRooms)
            {
                _allCleanAnnounced = true;
                OnAllRoomsClean();
            }
        }

        /// <summary>Lowest common ancestor of a room's renderers — the miniature room root we can pulse.</summary>
        private static Transform CommonRoot(List<Renderer> renderers)
        {
            Transform candidate = null;

            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;

                if (candidate == null)
                {
                    candidate = renderer.transform;
                    continue;
                }

                while (candidate != null && !renderer.transform.IsChildOf(candidate))
                    candidate = candidate.parent;

                if (candidate == null) return null;
            }

            return candidate;
        }

        private void SetRoomCleanVisual(DollhouseRoom room, float t)
        {
            Material material = null;

            if (cleanMat != null && dirtyMat != null)
            {
                material = t >= 1f ? cleanMat :
                           t <= 0f ? dirtyMat :
                           transitioningMat != null ? transitioningMat : (t > 0.5f ? cleanMat : dirtyMat);
            }

            // Tint via a shared material plus a property block, not r.material. The r.material getter
            // clones the material per renderer, which would give every room its own material instance
            // and break batching. sharedMaterial keeps one material; the block carries the per-room
            // alpha that the transition animates.
            var block = room.Block;
            block.Clear();

            if (material != null)
            {
                var c = material.color;
                c.a = Mathf.Lerp(0.6f, 1f, t);
                block.SetColor(ColorId, c);
            }

            foreach (var r in room.tintRenderers)
            {
                if (r == null) continue;

                if (material != null) r.sharedMaterial = material;
                r.SetPropertyBlock(block);
            }
        }

        private IEnumerator AnimateRoomTransition(DollhouseRoom room, float from, float to)
        {
            float elapsed = 0f;
            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(from, to, elapsed / transitionDuration);
                SetRoomCleanVisual(room, t);
                yield return null;
            }
            SetRoomCleanVisual(room, to);
        }

        private void OnAllRoomsClean()
        {
            Debug.Log("[DollhouseManager] All rooms clean! Generating share card...");
            StartCoroutine(GenerateShareCard());
        }

        private IEnumerator GenerateShareCard()
        {
            yield return new WaitForEndOfFrame();

            if (captureCamera == null)
            {
                Debug.LogWarning("[DollhouseManager] No capture camera set.");
                yield break;
            }

            var rt = new RenderTexture(captureWidth, captureHeight, 24);
            var orig = captureCamera.targetTexture;
            captureCamera.targetTexture = rt;

            Texture2D tex = new(captureWidth, captureHeight, TextureFormat.RGB24, false);
            captureCamera.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
            tex.Apply();

            captureCamera.targetTexture = orig;
            RenderTexture.active = null;
            Destroy(rt);

            byte[] png = ImageConversion.EncodeToPNG(tex);
            Destroy(tex);

            string path = Path.Combine(Application.persistentDataPath,
                $"boredoom_share_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(path, png);
            Debug.Log($"[DollhouseManager] Share card saved: {path}");

            if (reporter != null)
            {
                reporter.FetchContext("boredoom+share", 1, entries =>
                {
                    Debug.Log($"[DollhouseManager] Share card logged. Context entries found: {entries?.Count ?? 0}");
                });
            }
        }

        public float OverallCleanPercent =>
            _totalRooms > 0 ? (_cleanRooms / (float)_totalRooms) * 100f : 0f;
    }
}
