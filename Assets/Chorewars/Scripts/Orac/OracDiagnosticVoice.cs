using System.Collections.Generic;
using System.Text;

namespace Chorewars.Orac
{
    /// <summary>
    /// Turns cold findings into ORAC's opinions. Works with no brain and no network, so
    /// he can be withering even when he is offline.
    /// </summary>
    public static class OracDiagnosticVoice
    {
        public static string Speak(OracFinding f, int salt)
        {
            if (f == null) return "I have nothing to say to you.";

            string name = Pretty(f.Component);
            string evidence = f.EvidenceLine();
            string fix = string.IsNullOrEmpty(f.Suggestion) ? "" : f.Suggestion;

            if (f.Verdict == OracVerdict.Working)
            {
                return salt % 3 == 0
                    ? "The " + name + " is behaving. Do not let it go to your head."
                    : name + ": " + evidence + ". Adequate.";
            }

            switch (f.Severity)
            {
                case OracSeverity.Critical:
                    return "I have been watching, and your " + name + " is a disgrace. " +
                           evidence + ". " + fix + " - or shall I do it myself and take the credit?";
                case OracSeverity.Serious:
                    return "Really. " + name + ": " + evidence + ". " + fix + ".";
                case OracSeverity.Notable:
                    return "How tedious. " + evidence + ". " + fix + ".";
                default:
                    return "A trifle: " + evidence + ". " + fix + ".";
            }
        }

        public static OracFinding Worst(IList<OracFinding> findings)
        {
            OracFinding worst = null;
            if (findings == null) return null;
            for (int i = 0; i < findings.Count; i++)
            {
                var f = findings[i];
                if (!f.IsProblem) continue;
                if (worst == null || f.Severity > worst.Severity) worst = f;
            }
            return worst;
        }

        /// <summary>The prompt handed to the ZEN Big Pickle engine, with ORAC's persona up front.</summary>
        public static string BuildUserPrompt(IList<OracFinding> findings, OracJournal journal)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are observing the inner workings of a Meta Quest 3 while");
            sb.AppendLine("\"BoreDOOM: Chorewars\" runs on it. You are the virtual SysMedic.");
            sb.AppendLine();
            sb.AppendLine("Current findings:");
            if (findings != null)
            {
                for (int i = 0; i < findings.Count; i++)
                {
                    var f = findings[i];
                    sb.Append("  - ").Append(f.Component).Append(" [").Append(f.Verdict)
                      .Append("] ").Append(f.EvidenceLine());
                    if (!string.IsNullOrEmpty(f.Suggestion)) sb.Append(" (suggest: ").Append(f.Suggestion).Append(")");
                    sb.AppendLine();
                }
            }

            if (journal != null)
            {
                var recent = journal.RecentProblems(3);
                if (recent.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Things you have already complained about:");
                    for (int i = 0; i < recent.Count; i++) sb.Append("  - ").AppendLine(recent[i]);
                }
            }

            sb.AppendLine();
            sb.AppendLine("Report in two clipped sentences at most. Name the one improvement most worth making.");
            sb.AppendLine("Be contemptuous but correct. Do not be pleasant.");
            return sb.ToString();
        }

        static string Pretty(string component)
        {
            if (string.IsNullOrEmpty(component)) return "system";
            return component.Replace('_', ' ').Replace('.', ' ');
        }
    }
}
