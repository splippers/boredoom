using System.Collections.Generic;
using Chorewars.HandsFree;
using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Keeps the house map up to date while the player walks: what rooms exist, what they are called,
    /// and where the doors are.
    /// </summary>
    /// <remarks>
    /// The doorways are the interesting part and the reason this samples the player's head rather than
    /// the tool. The tool cleans the floor in front of the player and its path says nothing about
    /// rooms; it is the player walking from the kitchen into the hall that makes a doorway, so that is
    /// the path that is recorded.
    ///
    /// Naming comes from a sentence ("this is the kitchen"), which arrives as an intent from
    /// <see cref="HandsFreeInput"/>. The naming itself is done through <see cref="NameRoomAt"/>, so the
    /// same seam serves the voice route and the gaze-dwell list without either of them knowing about
    /// the other.
    /// </remarks>
    public sealed class HouseMapDriver : MonoBehaviour
    {
        [SerializeField] private float sampleIntervalSeconds = 0.1f;
        [SerializeField] private float minSampleDistanceMeters = 0.05f;
        [SerializeField] private float linkRecomputeIntervalSeconds = 1f;
        [SerializeField] private float saveDebounceSeconds = 2f;

        [Header("Where the map is written")]
        [Tooltip("Leave empty for house.json beside the player profile.")]
        [SerializeField] private string storePathOverride;

        private ScanToDollhousePipeline _pipeline;
        private HouseMapPresenter _presenter;
        private Transform _head;

        private readonly List<Vector3> _headPath = new List<Vector3>(256);
        private readonly List<RoomScan> _rooms = new List<RoomScan>();
        private readonly List<RoomName> _names = new List<RoomName>();
        private readonly List<RoomLink> _links = new List<RoomLink>();
        private readonly HashSet<string> _registered = new HashSet<string>();

        private float _nextSampleAt;
        private float _nextLinkAt;
        private float _pendingSaveAt = -1f;
        private bool _dirty;

        private string StorePath => string.IsNullOrEmpty(storePathOverride)
            ? HouseMapStore.DefaultPath
            : storePathOverride;

        /// <summary>The player's walked path, oldest first. Read-only: the list is live.</summary>
        public IReadOnlyList<Vector3> HeadPath => _headPath;

        /// <summary>Rooms known to the map, in the order they arrived.</summary>
        public IReadOnlyList<RoomScan> Rooms => _rooms;

        /// <summary>How many doors the walk has implied.</summary>
        public int LinkCount => _links.Count;

        /// <summary>How many rooms have a name.</summary>
        public int NamedRoomCount => HouseMapBuilder.NamedRoomIds(_names).Count;

        public void Configure(ScanToDollhousePipeline pipeline, HouseMapPresenter presenter, Transform head)
        {
            _pipeline = pipeline;
            _presenter = presenter;
            _head = head;

            LoadSaved();
        }

        /// <summary>
        /// Restores names and links from the last session, so a house that was named yesterday is
        /// still called the same thing today. Rooms themselves are not restored: they come from the
        /// scan, and a room whose id no longer appears in the scan has nothing to name.
        /// </summary>
        private void LoadSaved()
        {
            var saved = HouseMapStore.Load(StorePath);

            _names.Clear();
            for (int i = 0; i < saved.names.Count; i++)
                if (saved.names[i].IsValid) _names.Add(saved.names[i]);

            _links.Clear();
            _links.AddRange(saved.links);

            if (_names.Count > 0 || _links.Count > 0)
                Debug.Log($"[HouseMap] restored {_names.Count} name(s) and {_links.Count} doorway(s) from {StorePath}.");
        }

        /// <summary>
        /// Names whichever room the given world point stands in. This is the single entry point for
        /// both the spoken route and the gaze-dwell list; it returns false when the player is standing
        /// somewhere no room was scanned, rather than inventing a room to name.
        /// </summary>
        public bool NameRoomAt(Vector3 worldPoint, string name)
        {
            if (!HouseMapBuilder.TryNameRoomAt(_rooms, worldPoint, name, out var named)) return false;

            HouseMapBuilder.ApplyName(_names, named);

            if (_presenter != null) _presenter.SetName(named.roomId, named.name);

            QueueSave();
            Debug.Log($"[HouseMap] {named.roomId} is now \"{named.name}\".");
            return true;
        }

        /// <summary>Names the room the player is currently standing in.</summary>
        public bool NameRoomHere(string name)
        {
            var head = _head != null ? _head : transform;
            return NameRoomAt(head.position, name);
        }

        /// <summary>
        /// Handles a hands-free request. Naming is the only one this driver owns; everything else
        /// belongs to whoever raised it.
        /// </summary>
        public bool Handle(IntentRequest request)
        {
            if (request.Intent != ChorewarsIntent.NameRoom) return false;
            if (string.IsNullOrEmpty(request.Argument)) return false;

            return NameRoomHere(request.Argument);
        }

        /// <summary>
        /// Starts listening to a hands-free input, if there is one. The voice route and the gaze-dwell
        /// list both arrive through the same <see cref="HandsFreeInput"/>, so this is the only wiring
        /// either of them needs.
        /// </summary>
        public void ListenTo(HandsFreeInput input)
        {
            if (input == null) return;

            input.Intent -= OnIntent;
            input.Intent += OnIntent;
        }

        private void OnIntent(IntentRequest request) => Handle(request);

        private void Update()
        {
            AdoptScannedRooms();
            SampleHeadPath();
            RecomputeLinks();
            FlushSaveIfDue();
        }

        /// <summary>Pick up rooms the scan has found since the last look.</summary>
        private void AdoptScannedRooms()
        {
            if (_pipeline == null) return;

            var rooms = _pipeline.Rooms;
            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (string.IsNullOrEmpty(room.roomId) || !_registered.Add(room.roomId)) continue;

                _rooms.Add(room);

                if (_presenter != null)
                {
                    _presenter.SetRoom(room);
                    _presenter.SetName(room.roomId, HouseMapBuilder.NameOf(_names, room.roomId));
                }

                _dirty = true;
                Debug.Log($"[HouseMap] room {room.roomId} adopted " +
                          $"({room.boundsXZ.width:0.0}x{room.boundsXZ.height:0.0} m) — {_rooms.Count} known.");
            }
        }

        private void SampleHeadPath()
        {
            if (_head == null) return;
            if (Time.unscaledTime < _nextSampleAt) return;

            _nextSampleAt = Time.unscaledTime + Mathf.Max(0.02f, sampleIntervalSeconds);

            var position = _head.position;

            if (_headPath.Count == 0)
            {
                _headPath.Add(position);
                return;
            }

            if (Vector3.Distance(_headPath[_headPath.Count - 1], position) < minSampleDistanceMeters) return;

            _headPath.Add(position);
        }

        private void RecomputeLinks()
        {
            if (_rooms.Count < 2 || _headPath.Count < 2) return;
            if (Time.unscaledTime < _nextLinkAt) return;

            _nextLinkAt = Time.unscaledTime + Mathf.Max(0.25f, linkRecomputeIntervalSeconds);

            var found = HouseMapBuilder.BuildLinks(_rooms, _headPath);

            if (SameLinks(found, _links)) return;

            _links.Clear();
            _links.AddRange(found);

            if (_presenter != null) _presenter.SetLinks(_links);

            QueueSave();
            Debug.Log($"[HouseMap] {_links.Count} doorway(s) after {_headPath.Count} path samples.");
        }

        /// <summary>
        /// Whether the walk implies the same doors as before. The path only grows, so this is what
        /// stops the links being rebuilt (and the save being queued) on every tick once the walk has
        /// settled.
        /// </summary>
        private static bool SameLinks(List<RoomLink> a, List<RoomLink> b)
        {
            if (a.Count != b.Count) return false;

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].PairKey != b[i].PairKey) return false;
                if (Vector3.SqrMagnitude(a[i].crossingPoint - b[i].crossingPoint) > 0.0001f) return false;
            }

            return true;
        }

        private void QueueSave()
        {
            _dirty = true;
            _pendingSaveAt = Time.unscaledTime + Mathf.Max(0.25f, saveDebounceSeconds);
        }

        private void FlushSaveIfDue()
        {
            if (!_dirty || _pendingSaveAt < 0f || Time.unscaledTime < _pendingSaveAt) return;

            SaveNow();
        }

        /// <summary>Writes the map out. Called on a debounce, and on pause/quit so a kill cannot lose it.</summary>
        public void SaveNow()
        {
            var data = new HouseMapData();
            data.SetRooms(_rooms);
            data.names = new List<RoomName>(_names);
            data.SetLinks(_links);

            HouseMapStore.Save(data, StorePath);

            _dirty = false;
            _pendingSaveAt = -1f;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && _dirty) SaveNow();
        }

        private void OnApplicationQuit()
        {
            if (_dirty) SaveNow();
        }
    }
}
