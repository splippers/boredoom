using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Chorewars.Integration
{
    public class LocalNetworkAPI : MonoBehaviour
    {
        [System.Serializable]
        private class CoverageMessage
        {
            public string type;
            public float pct;
            public string player;
        }

        [Header("Discovery (UDP)")]
        [SerializeField] private int discoveryPort = 27877;
        [SerializeField] private string discoveryMessage = "CHOREWARS_BOREDOOM_DISCOVERY_V1";

        [Header("Coverage Broadcast")]
        [SerializeField] private bool enableCoverageBroadcast = true;
        [SerializeField] private int receivePort = 27878;

        public event Action<float> OnCoverageBroadcastReceived;

        private UdpClient _udp;
        private UdpClient _receiver;
        private Coroutine _receiveLoop;

        private void OnEnable()
        {
            try
            {
                _udp = new UdpClient();
                _udp.EnableBroadcast = true;
            }
            catch (Exception)
            {
                _udp = null;
            }

            if (enableCoverageBroadcast)
                StartReceiving();
        }

        private void OnDisable()
        {
            _udp?.Close();
            _udp = null;

            StopReceiving();
        }

        public void BroadcastDiscovery()
        {
            if (_udp == null) return;
            byte[] bytes = Encoding.UTF8.GetBytes(discoveryMessage);
            _udp.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, discoveryPort));
        }

        public void BroadcastCoverageUpdate(float pct)
        {
            if (_udp == null) return;

            var msg = new CoverageMessage
            {
                type = "coverage_update",
                pct = pct,
                player = SystemInfo.deviceName
            };

            string json = JsonUtility.ToJson(msg);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            _udp.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, receivePort));
        }

        private void StartReceiving()
        {
            try
            {
                _receiver = new UdpClient(receivePort);
                _receiver.EnableBroadcast = true;
                _receiveLoop = StartCoroutine(ReceiveLoop());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LocalNetworkAPI] Receive setup failed: {e.Message}");
            }
        }

        private void StopReceiving()
        {
            if (_receiveLoop != null)
            {
                StopCoroutine(_receiveLoop);
                _receiveLoop = null;
            }
            _receiver?.Close();
            _receiver = null;
        }

        private IEnumerator ReceiveLoop()
        {
            while (_receiver != null)
            {
                if (_receiver.Available > 0)
                {
                    try
                    {
                        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                        byte[] data = _receiver.Receive(ref remote);
                        string json = Encoding.UTF8.GetString(data);

                        var msg = JsonUtility.FromJson<CoverageMessage>(json);
                        if (msg != null && msg.type == "coverage_update")
                            OnCoverageBroadcastReceived?.Invoke(msg.pct);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[LocalNetworkAPI] Receive error: {e.Message}");
                    }
                }
                yield return null;
            }
        }
    }
}
