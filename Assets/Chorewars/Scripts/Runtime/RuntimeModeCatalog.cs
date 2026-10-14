using System;
using System.Collections.Generic;
using UnityEngine;
using Chorewars.Core;

namespace Chorewars.Runtime
{
    /// <summary>
    /// The seven shipped chore modes, and how to build each one at runtime.
    /// </summary>
    /// <remarks>
    /// This is the runtime counterpart of the Inspector-authored <c>ChoreModeRegistry</c> and
    /// <c>ChoreSelectionUI</c>, which need scene wiring and prefabs this project does not ship. A
    /// descriptor carries everything the bootstrapper needs to stand a mode up: the id, the label to
    /// show on the menu, a one-line pitch, the score export prefix and a factory that adds the
    /// component. The modes are listed in the order they should appear on the menu.
    /// </remarks>
    public static class RuntimeModeCatalog
    {
        public sealed class ModeInfo
        {
            public string Id;
            public string DisplayName;
            public string Blurb;
            public string ExportPrefix;
            public Func<RuntimeCoverageMode> Build;
        }

        private static List<ModeInfo> _modes;

        /// <summary>All shipped modes, menu order. Built once and cached.</summary>
        public static IReadOnlyList<ModeInfo> Modes => _modes ??= Build();

        private static List<ModeInfo> Build()
        {
            // Modes are added here as their mechanic actually lands, so the menu never offers a mode
            // that silently behaves like Standard Coverage.
            return new List<ModeInfo>
            {
                new ModeInfo
                {
                    Id = "hoover",
                    DisplayName = "Standard Coverage",
                    Blurb = "Clean as much floor as you can before the clock runs out.",
                    ExportPrefix = "hoover",
                    Build = () => New<RuntimeHooverMode>("RuntimeHooverMode")
                },
                new ModeInfo
                {
                    Id = "perfect-grid",
                    DisplayName = "Perfect Grid",
                    Blurb = "Work in tidy stripes — a cleared stripe lights up.",
                    ExportPrefix = "perfect-grid",
                    Build = () => New<RuntimePerfectGridMode>("RuntimePerfectGridMode")
                }
            };
        }

        private static RuntimeCoverageMode New<T>(string name) where T : RuntimeCoverageMode
        {
            var go = new GameObject(name);
            return go.AddComponent<T>();
        }

        public static bool TryGet(string id, out ModeInfo info)
        {
            foreach (var mode in Modes)
            {
                if (mode.Id == id)
                {
                    info = mode;
                    return true;
                }
            }

            info = null;
            return false;
        }

        /// <summary>
        /// The chore definition the scoring/summary read from. Kept here so every mode gets a
        /// consistent, non-null definition with the right id and name.
        /// </summary>
        public static ChoreDefinition CreateChore(ModeInfo info, float targetCoveragePercent)
        {
            var chore = ScriptableObject.CreateInstance<ChoreDefinition>();
            chore.choreId = info.Id;
            chore.displayName = info.DisplayName;
            chore.type = ChoreType.Hoovering;
            chore.targetCoveragePercent = targetCoveragePercent;
            return chore;
        }
    }
}
