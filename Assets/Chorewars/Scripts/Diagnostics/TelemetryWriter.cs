using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Chorewars.Diagnostics
{
    /// <summary>
    /// The single file behind every telemetry line, written off the main thread.
    /// </summary>
    /// <remarks>
    /// <see cref="TelemetryLogger"/> opened, appended and closed the file for every event. That is
    /// fine for a handful of lines and ruinous at frame rate, which is where the slice diagnostics
    /// live: the measurement would dominate the thing being measured. So lines go into a ring buffer
    /// and a worker thread drains it.
    ///
    /// The buffer is a ring, not a queue, because telemetry must never stall the game. When it fills,
    /// the oldest line is overwritten and the loss is counted, so a report can say how much of the run
    /// was lost rather than quietly presenting a gap as a lull. A full disk, a dropped frame and a
    /// device that stopped reporting should not all look the same.
    ///
    /// Nothing here throws. A run that dies because it could not write a log line is worse than a run
    /// with no log.
    /// </remarks>
    public static class TelemetryWriter
    {
        /// <summary>Lines held before the oldest starts being overwritten.</summary>
        public const int Capacity = 8192;

        const string FileName = "MQ3.log";
        const string RotatedName = "MQ3.1.log";
        const long MaxBytes = 10L * 1024 * 1024;
        const int FlushIntervalMs = 1000;
        const int MaxLinesPerFlush = 1024;

        static readonly string[] Ring = new string[Capacity];

        /// <summary>Guards the ring and its counters.</summary>
        static readonly object Gate = new object();

        /// <summary>Guards the file, so the worker and an explicit flush cannot write at once.</summary>
        static readonly object DrainGate = new object();

        static int _head;
        static int _count;
        static int _written;
        static int _dropped;
        static bool _started;
        static bool _hooked;

        static AutoResetEvent _signal;
        static Thread _worker;
        static volatile bool _running;

        static string _directory;
        static string _path;
        static StreamWriter _writer;

        /// <summary>
        /// Where to write, instead of <see cref="Application.persistentDataPath"/>. Set before the
        /// first line. For tests, which should not scribble in the real log.
        /// </summary>
        public static string DirectoryOverride { get; set; }

        /// <summary>Where the log is being written, or null before the first line.</summary>
        public static string Path
        {
            get { lock (Gate) return _path; }
        }

        /// <summary>Lines handed to the disk, excluding the ones dropped.</summary>
        public static int WrittenLines
        {
            get { lock (Gate) return _written; }
        }

        /// <summary>
        /// Lines lost to a full buffer or an unwritable file. Non-zero means the run outran the writer
        /// and the report is missing the oldest part of the burst.
        /// </summary>
        public static int DroppedLines
        {
            get { lock (Gate) return _dropped; }
        }

        /// <summary>Lines in the buffer waiting for the next flush.</summary>
        public static int PendingLines
        {
            get { lock (Gate) return _count; }
        }

        /// <summary>Whether a worker is draining the buffer.</summary>
        public static bool Running => _running;

        /// <summary>Adds one already-serialised JSON line. Never blocks on the disk, never throws.</summary>
        public static void Line(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            EnsureStarted();

            lock (Gate)
            {
                if (_count == Capacity)
                {
                    // Full: overwrite the oldest and account for it.
                    Ring[_head] = json;
                    _head = (_head + 1) % Capacity;
                    _dropped++;
                }
                else
                {
                    Ring[(_head + _count) % Capacity] = json;
                    _count++;
                }
            }

            // Set outside the lock: a wake-up that arrives a flush late costs nothing, a missed one
            // costs a second of delay.
            Signal();
        }

        /// <summary>
        /// Writes everything buffered right now. For tests, and for the moments where the log has to
        /// be on disk — a quit, or the end of a slice.
        /// </summary>
        public static void Flush() => Drain();

        /// <summary>Stops the worker and writes what is left.</summary>
        public static void Shutdown()
        {
            _running = false;
            Signal();

            try { _worker?.Join(2000); } catch (Exception) { }

            _worker = null;
            Drain();
            CloseWriter();
        }

        /// <summary>Empties everything and reopens the log from scratch. For tests.</summary>
        public static void Reset()
        {
            Shutdown();

            lock (Gate)
            {
                Array.Clear(Ring, 0, Ring.Length);
                _head = 0;
                _count = 0;
                _written = 0;
                _dropped = 0;
                _directory = null;
                _path = null;
                _started = false;
            }
        }

        static void Signal()
        {
            try { _signal?.Set(); } catch (ObjectDisposedException) { }
        }

        static void EnsureStarted()
        {
            if (_started) return;

            lock (Gate)
            {
                if (_started) return;
                _started = true;

                // persistentDataPath is main-thread only, so hold on to it here rather than asking
                // again from the worker.
                _directory = !string.IsNullOrEmpty(DirectoryOverride)
                    ? DirectoryOverride
                    : Application.persistentDataPath;
                _path = string.IsNullOrEmpty(_directory) ? FileName : System.IO.Path.Combine(_directory, FileName);

                _signal = new AutoResetEvent(false);
                _running = true;
                _worker = new Thread(Work)
                {
                    IsBackground = true,
                    Name = "ChorewarsTelemetry",
                    // Below normal: a busy device should drain the log with what is left rather than
                    // competing with the frame being measured.
                    Priority = System.Threading.ThreadPriority.BelowNormal,
                };
                _worker.Start();

                if (!_hooked)
                {
                    _hooked = true;
                    Application.quitting += Shutdown;
                }
            }
        }

        static void Work()
        {
            while (_running)
            {
                try { _signal.WaitOne(FlushIntervalMs); } catch (ObjectDisposedException) { return; }

                Drain();
            }
        }

        static void Drain()
        {
            lock (DrainGate) DrainLocked();
        }

        static void DrainLocked()
        {
            if (!_started) return;

            while (true)
            {
                List<string> batch;

                lock (Gate)
                {
                    if (_count == 0) return;

                    int take = Math.Min(_count, MaxLinesPerFlush);
                    batch = new List<string>(take);

                    for (int i = 0; i < take; i++) batch.Add(Ring[(_head + i) % Capacity]);

                    _head = (_head + take) % Capacity;
                    _count -= take;
                }

                int written = WriteBatch(batch);

                lock (Gate)
                {
                    _written += written;
                    _dropped += batch.Count - written;
                }
            }
        }

        /// <summary>Writes what it can and says how many lines landed.</summary>
        static int WriteBatch(List<string> batch)
        {
            if (_writer == null && !OpenWriter()) return 0;

            int written = 0;

            try
            {
                MaybeRotate();

                for (int i = 0; i < batch.Count; i++)
                {
                    _writer.WriteLine(batch[i]);
                    written++;
                }

                _writer.Flush();
            }
            catch (Exception)
            {
                // Give up on this handle; the next batch tries a fresh one. Lines already written
                // stand, the rest are counted as lost by the caller.
                CloseWriter();
            }

            return written;
        }

        static bool OpenWriter()
        {
            try
            {
                if (!string.IsNullOrEmpty(_directory) && !Directory.Exists(_directory))
                    Directory.CreateDirectory(_directory);

                _writer = new StreamWriter(_path, append: true) { AutoFlush = false };
                return true;
            }
            catch (Exception)
            {
                _writer = null;
                return false;
            }
        }

        static void CloseWriter()
        {
            try { _writer?.Flush(); } catch (Exception) { }
            try { _writer?.Dispose(); } catch (Exception) { }

            _writer = null;
        }

        static void MaybeRotate()
        {
            try
            {
                if (_writer == null) return;
                if (_writer.BaseStream.Length < MaxBytes) return;

                CloseWriter();

                var rotated = System.IO.Path.Combine(_directory ?? ".", RotatedName);
                if (File.Exists(rotated)) File.Delete(rotated);
                if (File.Exists(_path)) File.Move(_path, rotated);

                OpenWriter();
            }
            catch (Exception)
            {
                // A log that cannot rotate is still a log.
            }
        }
    }
}
