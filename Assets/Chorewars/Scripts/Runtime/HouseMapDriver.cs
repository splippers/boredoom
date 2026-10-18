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
    ///
    /// Naming is also how a room comes to exist. The scan is not depended on for the shape of the
    /// house: a name said where no room covers the player makes one on the spot, and their walk grows
    /// it to the size of the space they are in. That is what gives a headset with no usable meshing
    /// more than one room, and so any doors at all.
    /// </remarks>
    public sealed class HouseMapDriver : MonoBehaviour
    {
        [SerializeField] private float sampleIntervalSeconds = 0.1f;
        [SerializeField] private float minSampleDistanceMeters = 0.05f;
        [SerializeField] private float linkRecomputeIntervalSeconds = 1f;
        [SerializeField] private float saveDebounceSeconds = 2f;

        [Header("Rooms the player makes by naming one")]
        [Tooltip("How wide a room is when it is made, in metres. The walk grows it from there.")]
        [SerializeField] private float roomSeedSizeMeters = 2f;
        [Tooltip("Floor kept around the furthest place the player walked, so the walls are not on their path.")]
        [SerializeField] private float roomGrowthMarginMeters = HouseMapBuilder.DefaultGrowthMarginMeters;
        [Tooltip("How far a room may grow from the spot it was named at; stops it following the player across the house.")]
        [SerializeField] private float roomMaxHalfExtentMeters = HouseMapBuilder.DefaultMaxRoomHalfExtentMeters;
        [SerializeField] private float roomWallHeightMeters = 2.4f;

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

        // Where each walk-made room was named. Growth is capped around this, so a room cannot slide
        // across the house following the player. Rooms the scan found are not in here and are not grown.
        private readonly Dictionary<string, Vector2> _roomAnchors = new Dictionary<string, Vector2>();

        // Ids of the boxes that only stand in for a room (the play area). They are not rooms the map
        // holds, but they are remembered so their boxes can be taken out of the miniature once there is
        // a real room to draw.
        private readonly HashSet<string> _assumedRooms = new HashSet<string>();

        private float _nextSampleAt;
        private float _nextLinkAt;
        private float _nextMiniatureAt;
        private float _pendingSaveAt = -1f;
        private bool _dirty;

        /// <summary>
        /// Where the map is read and written: <c>house.json</c> beside the player profile unless the
        /// inspector says otherwise.
        /// </summary>
        /// <remarks>
        /// Settable so a test can keep its house to itself rather than writing over the player's.
        /// </remarks>
        public string StorePath
        {
            get => string.IsNullOrEmpty(storePathOverride) ? HouseMapStore.DefaultPath : storePathOverride;
            set => storePathOverride = value;
        }

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
            DrawRestoredHouse();
        }

        /// <summary>Puts the restored rooms, names and doors back on the miniature.</summary>
        private void DrawRestoredHouse()
        {
            if (_presenter == null) return;

            for (int i = 0; i < _rooms.Count; i++)
            {
                _presenter.SetRoom(_rooms[i]);
                _presenter.SetName(_rooms[i].roomId, HouseMapBuilder.NameOf(_names, _rooms[i].roomId));
            }

            if (_links.Count > 0) _presenter.SetLinks(_links);
        }

        /// <summary>
        /// Restores the house from the last session: the rooms the player made by naming them, what
        /// they called them, and the doorways they walked through.
        /// </summary>
        /// <remarks>
        /// The walk itself is not restored — the head path is this session's — so the doors found last
        /// time are kept as a baseline for this session's walk to add to. Without that, a restart would
        /// empty the dollhouse of doors until the player happened to pace through every one again.
        /// </remarks>
        private void LoadSaved()
        {
            var saved = HouseMapStore.Load(StorePath);

            _rooms.Clear();
            _roomAnchors.Clear();
            _registered.Clear();

            for (int i = 0; i < saved.rooms.Count; i++)
            {
                var room = saved.rooms[i];
                if (string.IsNullOrEmpty(room.roomId) || room.assumed) continue;

                _rooms.Add(room);
                _registered.Add(room.roomId);

                // Growth is capped around where a room was named. That spot is not stored, so the room
                // as it stands is the anchor: it may still grow, but only around where it now is.
                _roomAnchors[room.roomId] = room.boundsXZ.center;
            }

            _names.Clear();
            for (int i = 0; i < saved.names.Count; i++)
                if (saved.names[i].IsValid) _names.Add(saved.names[i]);

            _links.Clear();
            _links.AddRange(saved.links);

            if (_rooms.Count > 0 || _names.Count > 0 || _links.Count > 0)
            {
                Debug.Log($"[HouseMap] restored {_rooms.Count} room(s), {_names.Count} name(s) and " +
                          $"{_links.Count} doorway(s) from {StorePath}.");
            }
        }

        /// <summary>
        /// Names whichever room the given world point stands in, making the room first if the player is
        /// standing on floor that belongs to none.
        /// </summary>
        /// <remarks>
        /// This is the single entry point for both the spoken route and the gaze-dwell list, and making
        /// the room is why a house with no usable scan still has rooms at all. On a headset where the
        /// meshing yields nothing, the only box is the stand-in for the play area; without this a
        /// second room could never exist — naming a space would relabel the play area over and over,
        /// and no walk could ever connect two rooms, so there would be no doors.
        ///
        /// The stand-in box is deliberately not in <see cref="_rooms"/>, so "this is the kitchen" said
        /// anywhere in the play space makes a kitchen rather than renaming everything.
        /// </remarks>
        public bool NameRoomAt(Vector3 worldPoint, string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            if (!HouseMapBuilder.TryNameRoomAt(_rooms, worldPoint, name, out var named))
            {
                if (!MakeRoomAt(worldPoint, out var made)) return false;

                named = HouseMapBuilder.NameFor(made.roomId, name);
            }

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
            RefreshMiniature();
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

                if (room.assumed)
                {
                    // A box standing in for the play area rather than a room the player has. The map
                    // does not hold it, or naming a spot inside it could only ever relabel the whole
                    // play space; but its id is remembered so a new room cannot take it and its box can
                    // be taken out of the miniature once there is a real room to draw.
                    _assumedRooms.Add(room.roomId);
                    HideAssumedBoxes();

                    Debug.Log($"[HouseMap] {room.roomId} stands in for the play area; it is not a room the player named.");
                    continue;
                }

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
            GrowRoomFromSample(position);
        }

        /// <summary>
        /// Grows the room the player is standing in to cover where they just walked.
        /// </summary>
        /// <remarks>
        /// Only rooms the player named are grown. A room the scan found has already been measured, and
        /// the walk has nothing to add to it.
        /// </remarks>
        private void GrowRoomFromSample(Vector3 position)
        {
            if (_roomAnchors.Count == 0) return;

            string roomId = HouseMapBuilder.FindRoomIdAt(_rooms, position);
            if (string.IsNullOrEmpty(roomId)) return;
            if (!_roomAnchors.TryGetValue(roomId, out var anchor)) return;

            bool grew = HouseMapBuilder.TryGrowRoom(_rooms, roomId, anchor,
                new Vector2(position.x, position.z), roomGrowthMarginMeters, roomMaxHalfExtentMeters);

            if (!grew) return;

            if (_presenter != null && TryReadRoom(roomId, out var grown)) _presenter.SetRoom(grown);

            QueueSave();
        }

        /// <summary>
        /// Makes a room wherever the player is standing, for a space they have named that no room
        /// covers.
        /// </summary>
        private bool MakeRoomAt(Vector3 worldPoint, out RoomScan made)
        {
            made = default;

            string roomId = HouseMapBuilder.NextRoomId(_rooms, _registered);
            made = HouseMapBuilder.CreateRoomAt(roomId, worldPoint,
                Mathf.Max(0.25f, roomSeedSizeMeters) * 0.5f, roomWallHeightMeters);

            _rooms.Add(made);
            _registered.Add(roomId);
            _roomAnchors[roomId] = new Vector2(worldPoint.x, worldPoint.z);

            if (_presenter != null) _presenter.SetRoom(made);
            if (_pipeline != null) _pipeline.EnsureRoom(made);

            // The stand-in box is the play area, not a room. It goes as soon as there is a real room
            // to draw in its place, or the dollhouse would show a house bigger than the player's.
            HideAssumedBoxes();

            _dirty = true;
            Debug.Log($"[HouseMap] made {roomId} where the player stood " +
                      $"({made.boundsXZ.width:0.0}x{made.boundsXZ.height:0.0} m) — {_rooms.Count} room(s) known.");
            return true;
        }

        private bool TryReadRoom(string roomId, out RoomScan room)
        {
            for (int i = 0; i < _rooms.Count; i++)
            {
                if (_rooms[i].roomId != roomId) continue;

                room = _rooms[i];
                return true;
            }

            room = default;
            return false;
        }

        private void HideAssumedBoxes()
        {
            if (_pipeline == null || _assumedRooms.Count == 0) return;
            if (_roomAnchors.Count == 0) return;

            foreach (var roomId in _assumedRooms) _pipeline.SetMiniatureVisible(roomId, false);
        }

        private void RecomputeLinks()
        {
            if (_rooms.Count < 2) return;
            if (Time.unscaledTime < _nextLinkAt) return;

            _nextLinkAt = Time.unscaledTime + Mathf.Max(0.25f, linkRecomputeIntervalSeconds);

            // Door frames in the headset's room outline link rooms before anyone walks through them.
            var found = HouseMapBuilder.BuildLinks(_rooms, _headPath);
            foreach (var door in SceneRoomBuilder.PublishedDoors)
                if (HouseMapBuilder.HasRoom(_rooms, door.fromRoomId) && HouseMapBuilder.HasRoom(_rooms, door.toRoomId)
                    && !HouseMapBuilder.RecordsDoor(found, door)) found.Add(door);

            // The walked path only ever grows, so a door found on it stays found; a door restored from
            // last session stays too, because this session has no walk through it to find it again.
            var merged = new List<RoomLink>(_links);
            for (int i = 0; i < found.Count; i++)
                if (!HouseMapBuilder.RecordsDoor(merged, found[i])) merged.Add(found[i]);

            if (SameLinks(merged, _links)) return;

            _links.Clear();
            _links.AddRange(merged);

            if (_presenter != null) _presenter.SetLinks(_links);

            QueueSave();
            Debug.Log($"[HouseMap] {_links.Count} doorway(s) after {_headPath.Count} path samples.");
        }

        /// <summary>
        /// Keeps the miniature holding the rooms the map knows, at the size the map says they are.
        /// </summary>
        /// <remarks>
        /// This cannot be done only when a room changes. A scan that starts after the player has
        /// already named a room rebuilds the miniature and takes their rooms out of it, so the map puts
        /// them back; and a room the walk has grown has to be redrawn at the size it is now. One pass a
        /// second is enough for both, and cheap: a room that has not changed is not rebuilt.
        /// </remarks>
        private void RefreshMiniature()
        {
            if (_pipeline == null || _rooms.Count == 0) return;
            if (Time.unscaledTime < _nextMiniatureAt) return;

            _nextMiniatureAt = Time.unscaledTime + Mathf.Max(0.25f, linkRecomputeIntervalSeconds);

            bool anyChanged = false;
            for (int i = 0; i < _rooms.Count; i++)
            {
                if (!_roomAnchors.ContainsKey(_rooms[i].roomId)) continue;   // scan rooms are the pipeline's
                if (_pipeline.EnsureRoom(_rooms[i])) anyChanged = true;
            }

            // A room that grew moved the wall its doorway stands on, so the arch is put back on it.
            if (anyChanged && _presenter != null) _presenter.SetLinks(_links);
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
