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
        private int _totalRooms;
        private int _cleanRooms;

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
            public float coveragePercent;
            public bool isClean;
        }

        public void RegisterRoom(string roomId, Renderer[] roomRenderers)
        {
            if (_rooms.ContainsKey(roomId)) return;

            var room = new DollhouseRoom
            {
                roomId = roomId,
                renderers = new List<Renderer>(roomRenderers),
                isClean = false
            };
            _rooms[roomId] = room;
            _totalRooms++;

            SetRoomCleanVisual(room, 0f);
        }

        public void OnCoverageUpdate(string roomId, float coveragePct)
        {
            if (!_rooms.TryGetValue(roomId, out var room)) return;

            room.coveragePercent = coveragePct;

            if (coveragePct >= 95f && !room.isClean)
            {
                room.isClean = true;
                _cleanRooms++;
                StartCoroutine(AnimateRoomTransition(room, 0f, 1f));
            }
            else if (!room.isClean)
            {
                SetRoomCleanVisual(room, coveragePct / 100f);
            }

            if (_cleanRooms >= _totalRooms && _totalRooms > 0)
                OnAllRoomsClean();
        }

        private void SetRoomCleanVisual(DollhouseRoom room, float t)
        {
            foreach (var r in room.renderers)
            {
                if (r == null) continue;
                if (cleanMat != null && dirtyMat != null)
                    r.material = t >= 1f ? cleanMat :
                                 t <= 0f ? dirtyMat :
                                 transitioningMat ?? (t > 0.5f ? cleanMat : dirtyMat);
                var c = r.material.color;
                c.a = Mathf.Lerp(0.6f, 1f, t);
                r.material.color = c;
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
