using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Chorewars.Core;
using Chorewars.Dollhouse;

namespace Chorewars.UI
{
    /// <summary>
    /// Full-screen end-of-session overlay. Shows points, grade, coverage, and a share button.
    /// Assign in Inspector: panel, labels, share button, snapshot camera.
    /// </summary>
    public class SessionSummaryUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Labels")]
        [SerializeField] private TMP_Text gradeLabel;
        [SerializeField] private TMP_Text pointsLabel;
        [SerializeField] private TMP_Text coverageLabel;
        [SerializeField] private TMP_Text choreTitleLabel;
        [SerializeField] private TMP_Text durationLabel;

        [Header("Share")]
        [SerializeField] private Button shareButton;
        [SerializeField] private DollhouseSnapshotCamera snapshotCamera;
        [SerializeField] private TMP_Text shareStatusLabel;

        [Header("Dismiss")]
        [SerializeField] private Button closeButton;

        private ChoreResult _lastResult;

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (shareButton  != null) shareButton.onClick.AddListener(OnSharePressed);
            if (closeButton  != null) closeButton.onClick.AddListener(Hide);
        }

        public void Show(ChoreResult result)
        {
            _lastResult = result;

            if (choreTitleLabel != null)
                choreTitleLabel.text = result.session?.chore?.displayName ?? "Chore";

            if (gradeLabel != null)
            {
                gradeLabel.text = result.grade;
                gradeLabel.color = GradeColour(result.grade);
            }

            if (pointsLabel != null)
                pointsLabel.text = $"{result.totalPoints:N0} pts";

            if (coverageLabel != null)
                coverageLabel.text = $"{result.session?.coveragePercent:F1}% covered";

            if (durationLabel != null)
            {
                float secs = result.session != null
                    ? (float)(System.DateTime.UtcNow - result.session.startTimeUtc).TotalSeconds
                    : 0f;
                durationLabel.text = FormatDuration(secs);
            }

            if (shareStatusLabel != null) shareStatusLabel.text = "";

            if (panel != null) panel.SetActive(true);
            if (canvasGroup != null) StartCoroutine(FadeIn());
        }

        private void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void OnSharePressed()
        {
            if (snapshotCamera == null)
            {
                if (shareStatusLabel != null) shareStatusLabel.text = "No camera assigned";
                return;
            }

            var tex = snapshotCamera.Capture();
            if (tex != null)
            {
                if (shareStatusLabel != null) shareStatusLabel.text = "Saved to gallery!";
                // NativeShare / platform share sheet could go here
            }
        }

        private IEnumerator FadeIn()
        {
            canvasGroup.alpha = 0f;
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(t / 0.35f);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        private static Color GradeColour(string grade) => grade switch
        {
            "S"  => new Color(1f, 0.84f, 0f),   // gold
            "A"  => new Color(0.4f, 0.9f, 0.4f), // green
            "B"  => new Color(0.4f, 0.7f, 1f),   // blue
            "C"  => new Color(1f, 0.7f, 0.2f),   // amber
            _    => new Color(0.8f, 0.3f, 0.3f)  // red
        };

        private static string FormatDuration(float seconds)
        {
            int m = (int)seconds / 60;
            int s = (int)seconds % 60;
            return m > 0 ? $"{m}m {s:D2}s" : $"{s}s";
        }
    }
}
