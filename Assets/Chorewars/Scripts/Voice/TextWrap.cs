using System.Collections.Generic;
using System.Text;

namespace Chorewars.Voice
{
    /// <summary>
    /// Breaks an instruction into lines for a panel. Unity's TextMesh does not wrap, so this does.
    /// </summary>
    public static class TextWrap
    {
        /// <summary>
        /// Greedy word wrap. A word longer than a line is split rather than overflowing. At most
        /// <paramref name="maxLines"/> lines come back; if the text is longer, the last line ends in "...".
        /// </summary>
        public static List<string> Lines(string text, int width, int maxLines)
        {
            var lines = new List<string>();
            if (string.IsNullOrWhiteSpace(text) || width < 4 || maxLines < 1) return lines;

            var current = new StringBuilder();
            bool truncated = false;

            foreach (var rawWord in text.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries))
            {
                var word = rawWord;

                while (word.Length > width)
                {
                    if (current.Length > 0) { Push(lines, current); }
                    if (lines.Count >= maxLines) { truncated = true; break; }
                    lines.Add(word.Substring(0, width));
                    word = word.Substring(width);
                }
                if (truncated) break;

                if (current.Length > 0 && current.Length + 1 + word.Length > width) Push(lines, current);
                if (lines.Count >= maxLines) { truncated = true; break; }

                if (current.Length > 0) current.Append(' ');
                current.Append(word);
            }

            if (!truncated && current.Length > 0)
            {
                if (lines.Count >= maxLines) truncated = true;
                else Push(lines, current);
            }

            if (truncated && lines.Count > 0)
            {
                var last = lines[lines.Count - 1];
                if (last.Length > width - 3) last = last.Substring(0, width - 3);
                lines[lines.Count - 1] = last.TrimEnd() + "...";
            }

            return lines;
        }

        static void Push(List<string> lines, StringBuilder current)
        {
            lines.Add(current.ToString());
            current.Length = 0;
        }
    }
}
