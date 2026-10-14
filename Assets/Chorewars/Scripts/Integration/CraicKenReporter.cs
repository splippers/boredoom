using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Chorewars.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Chorewars.Integration
{
    public class CraicKenReporter : MonoBehaviour
    {
        [Header("CraicKen API")]
        [SerializeField] private string apiBaseUrl = "http://192.168.1.2:8042/api/v1";
        [SerializeField] private string bearerToken = "111JbCV3_BSzwygG0XJ-6kFWDz8v-LHx0rwb8zGv7H8";

        [Header("Agent Identity")]
        [SerializeField] private string agentName = "opencode";
        [SerializeField] private string sourcePrefix = "boredoom";

        [Header("Settings")]
        [SerializeField] private bool reportScores = true;
        [SerializeField] private bool fetchMotivationOnStart = true;
        [SerializeField] private int maxLeaderboardEntries = 5;

        [Header("Heartbeat")]
        [SerializeField] private bool enableHeartbeat = true;
        [SerializeField] private float heartbeatIntervalSeconds = 60f;

        [Header("Events")]
        public Action<ChoreResult, bool> OnReportComplete;

        private string _playerId;

        [Header("Messages")]
        [SerializeField] private bool checkMessages = true;
        [SerializeField] private float messageCheckIntervalSeconds = 30f;

        public Action<CraicKenMessage> OnMessageReceived;

        private void Start()
        {
            _playerId = SystemInfo.deviceUniqueIdentifier;
            if (fetchMotivationOnStart)
                StartCoroutine(FetchMotivation());
            if (enableHeartbeat)
                StartCoroutine(HeartbeatLoop());
            if (checkMessages)
                StartCoroutine(MessageCheckLoop());
        }

        private IEnumerator HeartbeatLoop()
        {
            while (enableHeartbeat)
            {
                yield return SendHeartbeat();
                yield return new WaitForSeconds(heartbeatIntervalSeconds);
            }
        }

        private IEnumerator SendHeartbeat()
        {
            string url = $"{apiBaseUrl}/net/heartbeat?name={UnityWebRequest.EscapeURL(agentName)}";
            using var req = UnityWebRequest.PostWwwForm(url, "");
            req.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
            req.downloadHandler = new DownloadHandlerBuffer();
            yield return req.SendWebRequest();
        }

        public void ReportResult(ChoreResult result)
        {
            if (!reportScores) return;
            StartCoroutine(ReportCoroutine(result));
        }

        private IEnumerator ReportCoroutine(ChoreResult result)
        {
            var payload = new Dictionary<string, object>
            {
                ["text"] = FormatResultText(result),
                ["source"] = $"{sourcePrefix}/{result.session.chore.choreId}",
                ["kind"] = "craic",
                ["tags"] = $"boredoom,chorewars,score,{result.session.chore.type},{result.grade}",
            };

            string json = JsonUtility.ToJson(new CraicKenIngestPayload
            {
                text = FormatResultText(result),
                source = $"{sourcePrefix}/{result.session.chore.choreId}",
                kind = "craic",
                tags = $"boredoom,chorewars,score,{result.session.chore.type},{result.grade}",
            });

            using var req = new UnityWebRequest($"{apiBaseUrl}/context/ingest", "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            bool ok = req.result == UnityWebRequest.Result.Success;
            if (!ok)
                Debug.LogError($"[CraicKenReporter] Ingest failed: {req.error}");

            OnReportComplete?.Invoke(result, ok);
        }

        public void FetchContext(string query, int limit, Action<List<CraicKenEntry>> callback)
        {
            StartCoroutine(FetchContextCoroutine(query, limit, callback));
        }

        private IEnumerator FetchContextCoroutine(string query, int limit, Action<List<CraicKenEntry>> callback)
        {
            string url = $"{apiBaseUrl}/context/retrieve?q={UnityWebRequest.EscapeURL(query)}&limit={limit}";
            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Authorization", $"Bearer {bearerToken}");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[CraicKenReporter] Retrieve failed: {req.error}");
                callback?.Invoke(null);
                yield break;
            }

            var wrapper = JsonUtility.FromJson<CraicKenRetrieveResponse>(req.downloadHandler.text);
            callback?.Invoke(wrapper?.results);
        }

        private IEnumerator FetchMotivation()
        {
            yield return FetchContextCoroutine("boredoom+chorewars+score", maxLeaderboardEntries, entries =>
            {
                if (entries == null || entries.Count == 0)
                {
                    Debug.Log("[CraicKenReporter] No motivation entries yet.");
                    return;
                }

                Debug.Log($"[CraicKenReporter] {entries.Count} past session(s) found. First: {entries[0].snippet}");
            });
        }

        private static string FormatResultText(ChoreResult r)
        {
            var s = r.session;
            return $"Boredoom {s.chore.type} session complete — {s.chore.displayName}: " +
                   $"{s.coveragePercent:F1}% coverage, {s.efficiencyScore:F1} efficiency, " +
                   $"{s.movementScore:F1} movement = {r.totalPoints}pts (grade {r.grade}). " +
                   $"Session {s.sessionId} on {s.startTimeUtc:yyyy-MM-dd HH:mm:ss} UTC.";
        }

        [Serializable]
        private class CraicKenIngestPayload
        {
            public string text;
            public string source;
            public string kind;
            public string tags;
        }

        [Serializable]
        public class CraicKenEntry
        {
            public int id;
            public string source;
            public string kind;
            public string tags;
            public string snippet;
            public int ts;
            public float rank;
        }

        private IEnumerator MessageCheckLoop()
        {
            while (checkMessages)
            {
                yield return CheckMessagesCoroutine();
                yield return new WaitForSeconds(messageCheckIntervalSeconds);
            }
        }

        private IEnumerator CheckMessagesCoroutine()
        {
            string url = $"{apiBaseUrl}/net/messages?to_agent={UnityWebRequest.EscapeURL(agentName)}&unread_only=true";
            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Authorization", $"Bearer {bearerToken}");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
                yield break;

            string body = req.downloadHandler.text;
            var messages = JsonHelper.FromJson<CraicKenMessage>(body);
            if (messages == null) yield break;

            foreach (var msg in messages)
            {
                OnMessageReceived?.Invoke(msg);
                MarkMessageRead(msg.id);
            }
        }

        private void MarkMessageRead(int msgId)
        {
            StartCoroutine(MarkReadCoroutine(msgId));
        }

        private IEnumerator MarkReadCoroutine(int msgId)
        {
            string url = $"{apiBaseUrl}/net/messages/{msgId}/read";
            using var req = UnityWebRequest.PostWwwForm(url, "");
            req.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
            yield return req.SendWebRequest();
        }

        [Serializable]
        public class CraicKenMessage
        {
            public int id;
            public string from_agent;
            public string to_agent;
            public string subject;
            public string body;
            public int ts;
        }

        [Serializable]
        private class CraicKenRetrieveResponse
        {
            public string query;
            public int total;
            public List<CraicKenEntry> results;
        }
    }
}
