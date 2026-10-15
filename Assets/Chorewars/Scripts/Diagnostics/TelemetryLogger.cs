using System;
using UnityEngine;

namespace Chorewars.Diagnostics
{
    /// <summary>
    /// JSON line logging to <see cref="Application.persistentDataPath"/>/MQ3.log, for events worth
    /// reading by hand. Written through <see cref="TelemetryWriter"/>, so a burst cannot stall the
    /// frame that produced it.
    /// </summary>
    /// <remarks>
    /// This is the human log: a handful of lines an hour, each a sentence. It is not the place for
    /// per-frame numbers — those want <see cref="TelemetrySamples"/>, which keeps its values in
    /// memory and reports statistics rather than a line each.
    /// </remarks>
    public static class TelemetryLogger
    {
        [Serializable]
        struct Line
        {
            public string ts;
            public string sub;
            public string sev;
            public string msg;
        }

        public static void Info(string subsystem, string message) => Write(subsystem, "INFO", message, null);

        public static void Warn(string subsystem, string message) => Write(subsystem, "WARN", message, null);

        public static void Error(string subsystem, string message, Exception ex = null) =>
            Write(subsystem, "ERROR", message, ex);

        static void Write(string subsystem, string severity, string message, Exception ex)
        {
            if (ex != null)
                message = $"{message} | {ex.GetType().Name}: {ex.Message}";

            var line = new Line
            {
                ts = DateTime.UtcNow.ToString("o"),
                sub = subsystem,
                sev = severity,
                msg = message
            };

            // Handed to the shared writer rather than written here, so a burst of events cannot stall
            // the frame that produced them.
            TelemetryWriter.Line(JsonUtility.ToJson(line));
        }
    }
}
