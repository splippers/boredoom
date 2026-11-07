using System.Text;

namespace Chorewars.Runtime
{
    /// <summary>
    /// The words of the in-headset room-scan readout: whether the headset has allowed spatial data, what surfaces it
    /// has handed over, and what to do next. Pure text, so it is tested without a headset.
    /// </summary>
    public static class ScanReadoutText
    {
        /// <param name="counts">Surfaces by kind: floor, ceiling, wall, door, window, other.</param>
        public static string Format(bool permission, int[] counts, int rooms, int doorways, int restarts)
        {
            counts = counts ?? new int[6];
            var sb = new StringBuilder();
            sb.AppendLine("ROOM SCAN");
            sb.AppendLine(permission ? "Permission: granted" : "Permission: NOT granted yet");
            sb.AppendLine($"Floors {counts[0]}  Ceilings {counts[1]}  Walls {counts[2]}");
            sb.AppendLine($"Doors {counts[3]}  Windows {counts[4]}  Other {counts[5]}");
            sb.AppendLine($"Rooms {rooms}  Doorways {doorways}");
            sb.Append(Advice(permission, counts, restarts));
            return sb.ToString();
        }

        public static string Advice(bool permission, int[] counts, int restarts)
        {
            counts = counts ?? new int[6];
            if (!permission) return "Say yes to the spatial data question.";
            int total = 0;
            foreach (var c in counts) total += c;
            if (total == 0) return restarts > 0 ? "Nothing yet. Run Space Setup (hold both fists)." : "Waiting for the headset...";
            if (counts[0] == 0) return "No floor found. Run Space Setup again.";
            if (counts[3] == 0) return "No door frames. Label doors in Space Setup.";
            return "Good. Hold both fists to rescan.";
        }
    }
}
