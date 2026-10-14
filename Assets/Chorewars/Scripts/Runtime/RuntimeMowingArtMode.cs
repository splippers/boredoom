using System.Collections.Generic;
using UnityEngine;
using Chorewars.Core;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Mowing Art: work the floor in a decorative pattern rather than neat stripes. The pattern is
    /// drawn as translucent tiles; each tile you pass over turns solid green, and the finished picture
    /// is worth up to 500 on top of the usual coverage score.
    /// </summary>
    /// <remarks>
    /// The tiles are laid out from the real <see cref="RuntimeCoverageMode.PlayBounds"/> rather than the
    /// hard-coded 5x5 box the Inspector-authored version uses as a placeholder, so the art always fits
    /// the room the player actually scanned. Tiles are matched to the tool by a dictionary keyed on the
    /// tile coordinate, so marking one is constant-time rather than a scan of every tile.
    /// </remarks>
    public sealed class RuntimeMowingArtMode : RuntimeCoverageMode
    {
        private const float TileSizeMeters = 0.4f;
        private const float MaxArtBonus = 500f;
        private const float ResampleIntervalSeconds = 0.25f;

        /// <summary>The picture to mow. Fixed here; the Inspector-authored version exposes four.</summary>
        private const Pattern SelectedPattern = Pattern.Diamond;

        private enum Pattern { Stripes, Diamond, Chevron }

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color PendingColour = new(0.92f, 0.80f, 0.25f, 0.28f);
        private static readonly Color DoneColour = new(0.25f, 0.92f, 0.45f, 0.42f);

        private readonly Dictionary<Vector2Int, int> _tileByCoord = new();
        private Renderer[] _tileRenderers;
        private MaterialPropertyBlock[] _tileBlocks;
        private bool[] _tileDone;
        private Transform _artRoot;
        private Material _tileMaterial;
        private int _tilesDone;
        private float _nextObjectiveAt;

        public override string ProfileModeId => "mowing-art";

        public override string ModeDisplayName => "Mowing Art";

        protected override void OnModeBegun()
        {
            base.OnModeBegun();
            BuildPattern();
        }

        protected override void OnModeEnded(ChoreResult result)
        {
            if (_artRoot != null) Destroy(_artRoot.gameObject);
            base.OnModeEnded(result);
        }

        protected override void OnTrackedPositionSampled(Vector3 worldPos)
        {
            base.OnTrackedPositionSampled(worldPos);
            CompleteTileAt(worldPos);
        }

        protected override void OnModeTick(float elapsedSeconds, float coveragePercent)
        {
            if (_tileDone == null || Time.time < _nextObjectiveAt) return;

            _nextObjectiveAt = Time.time + ResampleIntervalSeconds;
            Hud?.SetObjective($"ART {_tilesDone}/{_tileDone.Length} tiles   (+{GetBonusPoints():0})");
        }

        /// <summary>Completing the picture is worth up to 500.</summary>
        protected override float GetBonusPoints()
        {
            if (_tileDone == null || _tileDone.Length == 0) return base.GetBonusPoints();

            float fraction = (float)_tilesDone / _tileDone.Length;
            return base.GetBonusPoints() + fraction * MaxArtBonus;
        }

        private void BuildPattern()
        {
            var bounds = PlayBounds;

            int countX = Mathf.Max(1, Mathf.FloorToInt(bounds.size.x / TileSizeMeters));
            int countZ = Mathf.Max(1, Mathf.FloorToInt(bounds.size.z / TileSizeMeters));

            // Centre the tile grid inside the play area so the art sits in the middle of the room.
            float originX = bounds.center.x - countX * TileSizeMeters * 0.5f;
            float originZ = bounds.center.z - countZ * TileSizeMeters * 0.5f;

            _artRoot = new GameObject("MowingArt").transform;
            _artRoot.SetParent(transform, false);

            var coords = new List<Vector2Int>();
            for (int x = 0; x < countX; x++)
            for (int z = 0; z < countZ; z++)
                if (InPattern(x, z)) coords.Add(new Vector2Int(x, z));

            _tileRenderers = new Renderer[coords.Count];
            _tileBlocks = new MaterialPropertyBlock[coords.Count];
            _tileDone = new bool[coords.Count];
            _tileMaterial = PlaceholderMaterials.CreateTransparent(PendingColour, PendingColour.a);

            for (int i = 0; i < coords.Count; i++)
            {
                var coord = coords[i];
                float centreX = originX + (coord.x + 0.5f) * TileSizeMeters;
                float centreZ = originZ + (coord.y + 0.5f) * TileSizeMeters;

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = $"Tile_{coord.x}_{coord.y}";
                quad.transform.SetParent(_artRoot, false);
                quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                quad.transform.position = new Vector3(centreX, bounds.min.y + 0.014f, centreZ);
                quad.transform.localScale = new Vector3(TileSizeMeters * 0.94f, TileSizeMeters * 0.94f, 1f);

                var collider = quad.GetComponent<Collider>();
                if (collider != null) Destroy(collider);

                var renderer = quad.GetComponent<Renderer>();
                renderer.sharedMaterial = _tileMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                _tileRenderers[i] = renderer;
                _tileBlocks[i] = new MaterialPropertyBlock();
                _tileBlocks[i].SetColor(ColorId, PendingColour);
                renderer.SetPropertyBlock(_tileBlocks[i]);

                _tileByCoord[coord] = i;
            }

            Debug.Log($"[BoreDOOM] Mowing Art laid out {coords.Count} tiles ({SelectedPattern}) in a {countX}x{countZ} grid.");
        }

        private static bool InPattern(int x, int z) => SelectedPattern switch
        {
            Pattern.Stripes => z % 2 == 0,
            Pattern.Diamond => (x + z) % 2 == 0,
            Pattern.Chevron => (x + z) % 3 != 2,
            _ => true
        };

        private void CompleteTileAt(Vector3 worldPos)
        {
            if (_tileByCoord.Count == 0) return;

            var bounds = PlayBounds;
            int countX = Mathf.Max(1, Mathf.FloorToInt(bounds.size.x / TileSizeMeters));
            int countZ = Mathf.Max(1, Mathf.FloorToInt(bounds.size.z / TileSizeMeters));
            float originX = bounds.center.x - countX * TileSizeMeters * 0.5f;
            float originZ = bounds.center.z - countZ * TileSizeMeters * 0.5f;

            int cx = Mathf.FloorToInt((worldPos.x - originX) / TileSizeMeters);
            int cz = Mathf.FloorToInt((worldPos.z - originZ) / TileSizeMeters);

            if (!_tileByCoord.TryGetValue(new Vector2Int(cx, cz), out int index)) return;
            if (_tileDone[index]) return;

            _tileDone[index] = true;
            _tilesDone++;
            _tileBlocks[index].SetColor(ColorId, DoneColour);
            _tileRenderers[index].SetPropertyBlock(_tileBlocks[index]);
        }
    }
}
