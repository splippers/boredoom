using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Everything the player has told the house about itself: the rooms the scan found, what the
    /// player called them, and the doorways they walked through.
    /// </summary>
    [Serializable]
    public class HouseMapData
    {
        public List<RoomScan> rooms = new List<RoomScan>();
        public List<RoomName> names = new List<RoomName>();
        public List<RoomLink> links = new List<RoomLink>();

        /// <summary>Replace the stored rooms, keeping the names that still belong to one of them.</summary>
        /// <remarks>
        /// A rescan finds the same rooms under the same ids and losing the names would be rude; a room
        /// that the new scan did not find has no box to label, so its name is dropped rather than kept
        /// pointing at nothing.
        /// </remarks>
        public void SetRooms(IReadOnlyList<RoomScan> scanned)
        {
            rooms = scanned == null ? new List<RoomScan>() : new List<RoomScan>(scanned);

            var kept = new List<RoomName>(names.Count);
            for (int i = 0; i < names.Count; i++)
                if (names[i].IsValid && HouseMapBuilder.HasRoom(rooms, names[i].roomId))
                    kept.Add(names[i]);

            names = kept;
        }

        public void SetLinks(IReadOnlyList<RoomLink> found)
        {
            links = found == null ? new List<RoomLink>() : new List<RoomLink>(found);
        }
    }

    /// <summary>
    /// Reads and writes <see cref="HouseMapData"/> as house.json next to the player profile.
    /// </summary>
    /// <remarks>
    /// Kept apart from the profile on purpose: the profile is lifetime play statistics, and this is a
    /// description of a house. Losing one should not touch the other, and the shape of this will keep
    /// changing as the house model grows.
    ///
    /// Failure is never fatal. A house.json that cannot be read is treated as "no house named yet"
    /// rather than taking the scan down, because the worst case is that the player names the kitchen
    /// again.
    /// </remarks>
    public static class HouseMapStore
    {
        public const string FileName = "house.json";

        /// <summary>Deferred to a call, like the profile path: reading it during type load can throw.</summary>
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

        public static HouseMapData Load(string path = null)
        {
            path = string.IsNullOrEmpty(path) ? DefaultPath : path;

            try
            {
                if (!File.Exists(path)) return new HouseMapData();

                var data = JsonUtility.FromJson<HouseMapData>(File.ReadAllText(path));
                if (data == null) return new HouseMapData();

                data.rooms ??= new List<RoomScan>();
                data.names ??= new List<RoomName>();
                data.links ??= new List<RoomLink>();
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HouseMap] could not read {path}: {e.Message}");
                return new HouseMapData();
            }
        }

        /// <summary>Saves the map, and says whether it made it to disk.</summary>
        public static bool Save(HouseMapData data, string path = null)
        {
            if (data == null) return false;
            path = string.IsNullOrEmpty(path) ? DefaultPath : path;

            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(data, true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HouseMap] could not write {path}: {e.Message}");
                return false;
            }
        }
    }
}
