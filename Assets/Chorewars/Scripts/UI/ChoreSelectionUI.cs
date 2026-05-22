using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Chorewars.Core;
using Chorewars.Modes;

namespace Chorewars.UI
{
    public class ChoreSelectionUI : MonoBehaviour
    {
        [System.Serializable]
        public class ModeTile
        {
            public string modeId;
            public string displayName;
            public string tagline;
            public string sceneName;
            public Color accentColour = Color.white;
            public Sprite icon;
        }

        [SerializeField] private List<ModeTile> modes = new()
        {
            new() { modeId = "hoover", displayName = "Standard Coverage", tagline = "Cover every inch", sceneName = "HooverMode", accentColour = new Color(0.2f, 0.8f, 1f) },
            new() { modeId = "perfect-grid", displayName = "Perfect Grid", tagline = "Stripe-perfect alignment", sceneName = "HooverMode", accentColour = new Color(0.1f, 0.9f, 0.6f) },
            new() { modeId = "ghost-run", displayName = "Ghost Run", tagline = "Beat your past self", sceneName = "HooverMode", accentColour = new Color(0.6f, 0.4f, 1f) },
            new() { modeId = "chaos", displayName = "Chaos Assessment", tagline = "Scan the mess. No cleaning.", sceneName = "HooverMode", accentColour = new Color(1f, 0.2f, 0.2f) },
            new() { modeId = "mowing-art", displayName = "Mowing Art", tagline = "Trace decorative patterns", sceneName = "MowingMode", accentColour = new Color(0.3f, 0.9f, 0.3f) },
            new() { modeId = "declutter", displayName = "Declutter Dash", tagline = "Race the clock", sceneName = "HooverMode", accentColour = new Color(1f, 0.7f, 0.1f) },
            new() { modeId = "battle", displayName = "ChoreWars Battle", tagline = "1v1 LAN race", sceneName = "HooverMode", accentColour = new Color(1f, 0.3f, 0.3f) },
        };

        [Header("Mode Controller References")]
        [SerializeField] private BaseCoverageModeController standardMode;
        [SerializeField] private PerfectGridModeController perfectGridMode;
        [SerializeField] private GhostRunModeController ghostRunMode;
        [SerializeField] private ChaosAssessmentModeController chaosMode;
        [SerializeField] private MowingArtModeController mowingArtMode;
        [SerializeField] private DeclutterDashModeController declutterMode;
        [SerializeField] private ChoreWarsBattleModeController battleMode;

        [Header("Selection Panel")]
        [SerializeField] private GameObject selectionPanel;

        [Header("Lobby (ChoreWars Battle)")]
        [SerializeField] private GameObject battleLobbyPanel;
        [SerializeField] private TMP_Text lobbyStatusLabel;
        [SerializeField] private Button lobbyReadyButton;
        [SerializeField] private Button lobbyCancelButton;

        [Header("Session")]
        [SerializeField] private HUDController hud;
        [SerializeField] private SessionSummaryUI summaryUI;

        private BaseCoverageModeController _activeMode;

        private void Awake()
        {
            if (battleLobbyPanel != null) battleLobbyPanel.SetActive(false);
        }

        public void SelectMode(string modeId)
        {
            switch (modeId)
            {
                case "hoover":
                    ActivateCoverageMode(standardMode);
                    break;

                case "perfect-grid":
                    ActivateCoverageMode(perfectGridMode);
                    break;

                case "ghost-run":
                    ActivateCoverageMode(ghostRunMode);
                    break;

                case "chaos":
                    ActivateChaosMode();
                    break;

                case "mowing-art":
                    ActivateCoverageMode(mowingArtMode);
                    break;

                case "declutter":
                    ActivateDeclutterMode();
                    break;

                case "battle":
                    ShowBattleLobby();
                    break;
            }
        }

        private void ActivateCoverageMode(BaseCoverageModeController mode)
        {
            if (mode == null) return;
            _activeMode = mode;
            selectionPanel.SetActive(false);
            mode.gameObject.SetActive(true);
            mode.Begin();
        }

        private void ActivateChaosMode()
        {
            if (chaosMode == null) return;
            selectionPanel.SetActive(false);
            chaosMode.gameObject.SetActive(true);
            chaosMode.BeginAssessment();
        }

        private void ActivateDeclutterMode()
        {
            if (declutterMode == null) return;
            selectionPanel.SetActive(false);
            declutterMode.gameObject.SetActive(true);
            declutterMode.BeginSession();
        }

        private void ShowBattleLobby()
        {
            if (battleLobbyPanel == null) return;
            selectionPanel.SetActive(false);
            battleLobbyPanel.SetActive(true);

            if (lobbyStatusLabel != null)
                lobbyStatusLabel.text = "Waiting for opponent on LAN...";

            if (lobbyReadyButton != null)
                lobbyReadyButton.onClick.RemoveAllListeners();

            if (lobbyCancelButton != null)
                lobbyCancelButton.onClick.RemoveAllListeners();

            if (lobbyReadyButton != null)
                lobbyReadyButton.onClick.AddListener(() =>
                {
                    battleLobbyPanel.SetActive(false);
                    ActivateCoverageMode(battleMode);
                });

            if (lobbyCancelButton != null)
                lobbyCancelButton.onClick.AddListener(() =>
                {
                    battleLobbyPanel.SetActive(false);
                    selectionPanel.SetActive(true);
                });
        }

        public void ReturnToSelection()
        {
            if (_activeMode != null)
            {
                _activeMode.End();
                _activeMode.gameObject.SetActive(false);
                _activeMode = null;
            }

            chaosMode?.EndAssessment();
            chaosMode?.gameObject.SetActive(false);
            declutterMode?.EndSession();
            declutterMode?.gameObject.SetActive(false);

            if (battleLobbyPanel != null) battleLobbyPanel.SetActive(false);
            selectionPanel.SetActive(true);
        }
    }
}
