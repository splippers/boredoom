using System;

namespace Chorewars.Voice
{
    /// <summary>
    /// What the PC says Claude is doing, so the headset can show it. Wire text: <c>kind</c> or <c>kind TAB detail</c>.
    /// </summary>
    /// <remarks>
    /// <c>transcribing</c> (turning your speech into words), <c>thinking TAB what-you-said</c>, <c>working TAB what</c>
    /// (a longer job, such as a build), and <c>idle</c> (nothing to show). Anything else is treated as idle, so a
    /// newer PC cannot make an older headset show something nonsensical.
    /// </remarks>
    public struct ClaudeStatus
    {
        public enum Kind { Idle, Transcribing, Thinking, Working }

        public Kind kind;
        public string detail;

        public bool Busy => kind != Kind.Idle;

        public static ClaudeStatus Parse(string wire)
        {
            if (string.IsNullOrWhiteSpace(wire)) return new ClaudeStatus { kind = Kind.Idle, detail = string.Empty };

            int tab = wire.IndexOf('\t');
            string head = (tab < 0 ? wire : wire.Substring(0, tab)).Trim().ToLowerInvariant();
            string detail = tab < 0 ? string.Empty : wire.Substring(tab + 1).Trim();

            switch (head)
            {
                case "transcribing": return new ClaudeStatus { kind = Kind.Transcribing, detail = detail };
                case "thinking": return new ClaudeStatus { kind = Kind.Thinking, detail = detail };
                case "working": return new ClaudeStatus { kind = Kind.Working, detail = detail };
                default: return new ClaudeStatus { kind = Kind.Idle, detail = string.Empty };
            }
        }

        /// <summary>The line to show, with <paramref name="dots"/> (1 to 3) animating it. Empty when idle.</summary>
        public string Describe(int dots, int maxDetail = 40)
        {
            if (!Busy) return string.Empty;

            var trail = new string('.', Math.Max(1, Math.Min(3, dots)));
            string what;
            switch (kind)
            {
                case Kind.Transcribing: what = "Listening to what you said"; break;
                case Kind.Working: what = "Claudia is working" + (string.IsNullOrEmpty(detail) ? string.Empty : ": " + Clip(detail, maxDetail)); break;
                default: what = "Claudia is thinking"; break;
            }

            string line = what + trail;
            if (kind == Kind.Thinking && !string.IsNullOrEmpty(detail)) line += "   You: " + Clip(detail, maxDetail);
            return line;
        }

        static string Clip(string text, int max)
            => text.Length <= max ? text : text.Substring(0, Math.Max(0, max - 3)) + "...";
    }
}
