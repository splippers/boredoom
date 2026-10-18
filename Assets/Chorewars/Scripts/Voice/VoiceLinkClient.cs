using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using Chorewars.Diagnostics;
using UnityEngine;

namespace Chorewars.Voice
{
    /// <summary>
    /// The headset's end of the voice link: keeps a TCP connection to the PC and hands frames across.
    /// </summary>
    /// <remarks>
    /// The PC listens on its own loopback; <c>adb reverse tcp:7777 tcp:7777</c> makes the headset's
    /// 127.0.0.1:7777 reach it, so nothing is exposed on the network. Reading and writing happen on one
    /// background thread; the main thread only touches two queues. It reconnects forever, because the
    /// link is a convenience and must never take the game down with it.
    /// </remarks>
    public sealed class VoiceLinkClient : MonoBehaviour
    {
        public string Host = "127.0.0.1";
        public int Port = 7777;

        readonly ConcurrentQueue<VoiceFrame> _incoming = new ConcurrentQueue<VoiceFrame>();
        readonly ConcurrentQueue<byte[]> _outgoing = new ConcurrentQueue<byte[]>();
        Thread _thread;
        volatile bool _stop;
        volatile bool _connected;

        public bool Connected => _connected;

        public bool TryDequeue(out VoiceFrame frame) => _incoming.TryDequeue(out frame);

        /// <summary>Queues a frame for the PC. Dropped when not connected: stale audio is worse than none.</summary>
        public void Send(byte kind, byte[] payload)
        {
            if (!_connected) return;
            _outgoing.Enqueue(VoiceLinkProtocol.Encode(kind, payload ?? Array.Empty<byte>()));
        }

        void OnEnable()
        {
            _stop = false;
            _thread = new Thread(Run) { IsBackground = true, Name = "VoiceLink" };
            _thread.Start();
        }

        void OnDisable()
        {
            _stop = true;
        }

        void Run()
        {
            while (!_stop)
            {
                try { Session(); }
                catch (Exception) { /* a failed attempt is normal while the PC is away */ }

                _connected = false;
                for (int i = 0; i < 20 && !_stop; i++) Thread.Sleep(100);
            }
        }

        void Session()
        {
            using (var client = new TcpClient())
            {
                var connect = client.BeginConnect(Host, Port, null, null);
                if (!connect.AsyncWaitHandle.WaitOne(2000)) return;
                client.EndConnect(connect);

                var stream = client.GetStream();
                var reader = new VoiceFrameReader();
                var buffer = new byte[16 * 1024];
                _connected = true;

                while (!_stop && client.Connected)
                {
                    while (_outgoing.TryDequeue(out var bytes)) stream.Write(bytes, 0, bytes.Length);

                    if (client.Client.Poll(0, SelectMode.SelectRead))
                    {
                        if (client.Available == 0) return; // the PC closed the connection

                        int read = stream.Read(buffer, 0, buffer.Length);
                        foreach (var frame in reader.Feed(buffer, read)) _incoming.Enqueue(frame);
                        if (reader.Faulted) return;
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
            }
        }
    }
}
