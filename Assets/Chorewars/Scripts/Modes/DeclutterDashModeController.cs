using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Chorewars.Modes
{
    /// <summary>
    /// Declutter Dash: race the clock to pick up and bin as many detected objects as possible.
    /// Uses OVRSceneObject labels (CHAIR, COUCH, TABLE) as proxies for clutterable items.
    /// Each object "collected" (player walks within arm's reach) scores points.
    ///
    /// Standalone MonoBehaviour — does not extend BaseCoverageModeController
    /// (coverage doesn't apply to decluttering).
    /// </summary>
    public class DeclutterDashModeController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float sessionDurationSeconds = 120f;
        [SerializeField] private float armReachMeters = 0.6f;
        [SerializeField] private int pointsPerItem = 50;

        [Header("HUD")]
        [SerializeField] private TMP_Text timerLabel;
        [SerializeField] private TMP_Text scoreLabel;
        [SerializeField] private TMP_Text itemsLabel;

        [Header("AR")]
        [SerializeField] private GameObject collectVfxPrefab;
        [SerializeField] private AudioClip collectSound;

        private float _timeRemaining;
        private int _score;
        private bool _running;
        private AudioSource _audio;
        private Transform _cameraRig; // XR camera / OVRCameraRig

        // Each clutter item: world position + whether already collected
        private readonly List<ClutterItem> _items = new();

        private class ClutterItem
        {
            public Vector3 worldPos;
            public string label;
            public bool collected;
            public GameObject indicator;
        }

        private void Awake()
        {
            _audio = GetComponent<AudioSource>();
            _cameraRig = Camera.main?.transform;
        }

        public void BeginSession()
        {
            _timeRemaining = sessionDurationSeconds;
            _score = 0;
            _running = true;
            ScanForClutter();
            RefreshHUD();
        }

        public void EndSession()
        {
            _running = false;
            foreach (var item in _items)
                if (item.indicator != null) Destroy(item.indicator);
        }

        private void Update()
        {
            if (!_running) return;

            _timeRemaining -= Time.deltaTime;
            if (_timeRemaining <= 0f)
            {
                _timeRemaining = 0f;
                EndSession();
                return;
            }

            if (_cameraRig != null)
                CheckCollection(_cameraRig.position);

            RefreshHUD();
        }

        private void ScanForClutter()
        {
            _items.Clear();

#if CHOREWARS_META_XR
            // Query OVRSceneObjects for furniture/clutter anchors
            var anchors = FindObjectsOfType<OVRSceneObject>();
            foreach (var anchor in anchors)
            {
                string lbl = anchor.Classification.Labels.Count > 0
                    ? anchor.Classification.Labels[0].ToString()
                    : "OBJECT";
                _items.Add(new ClutterItem
                {
                    worldPos = anchor.transform.position,
                    label    = lbl
                });
            }
#else
            // Editor placeholders
            for (int i = 0; i < 8; i++)
                _items.Add(new ClutterItem
                {
                    worldPos = new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f)),
                    label    = "ITEM"
                });
#endif
            RefreshHUD();
        }

        private void CheckCollection(Vector3 playerPos)
        {
            foreach (var item in _items)
            {
                if (item.collected) continue;
                float dist = Vector3.Distance(
                    new Vector3(playerPos.x, 0f, playerPos.z),
                    new Vector3(item.worldPos.x, 0f, item.worldPos.z));

                if (dist > armReachMeters) continue;

                item.collected = true;
                _score += pointsPerItem;

                if (collectVfxPrefab != null)
                    Destroy(Instantiate(collectVfxPrefab, item.worldPos, Quaternion.identity), 2f);

                if (_audio != null && collectSound != null)
                    _audio.PlayOneShot(collectSound);

                if (item.indicator != null) Destroy(item.indicator);
            }
        }

        private void RefreshHUD()
        {
            if (timerLabel != null)
            {
                int s = Mathf.CeilToInt(_timeRemaining);
                timerLabel.text = $"{s / 60}:{s % 60:D2}";
                timerLabel.color = _timeRemaining < 20f ? Color.red : Color.white;
            }

            if (scoreLabel  != null) scoreLabel.text = $"{_score} pts";

            if (itemsLabel != null)
            {
                int remaining = _items.FindAll(i => !i.collected).Count;
                itemsLabel.text = $"{remaining} items left";
            }
        }

        public int FinalScore => _score;
    }
}
