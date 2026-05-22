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
            new() { modeId = "chaos", displayName = "Chaos Assessm