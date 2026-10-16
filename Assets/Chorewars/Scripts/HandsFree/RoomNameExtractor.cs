using System.Collections.Generic;
using System.Text;

namespace Chorewars.HandsFree
{
    /// <summary>
    /// Pulls the name out of "this is the kitchen" and similar sentences.
    /// </summary>
    /// <remarks>
    /// Pure text, no microphone and no scene, so the awkward part - what a person actually says when
    /// naming a room - can be tested by feeding it sentences. The paragraph this sits under is
    /// deliberately generous: the player is likely to say "this is the kitchen", "call this the
    /// kitchen", "this room is the kitchen" or "we call this the kitchen", and all of those must land
    /// on the same name.
    ///
    /// It is not a speech recogniser and does not pretend to be one: if the sentence does not name a
    /// room then <see cref="TryExtract"/> says so and the router hands the words to ORAC, which is
    /// better company than guessing a name and quietly labelling the wrong room.
    /// </remarks>
    public static class RoomNameExtractor
    {
        /// <summary>
        /// The phrasings that introduce a name, longest first within each family so the more specific
        /// one wins ("this room is the" before "this is").
        /// </summary>
        static readonly string[] Prefixes =
        {
            "the name of this room is", "the name is",
            "this room is called", "this room is the", "this room is",
            "this is called", "this is the", "this is our", "this is my", "this is a", "this is",
            "call this room the", "call this room", "call this the", "call this", "call it the", "call it",
            "name this room the", "name this room", "name this the", "name this",
            "we call this the", "we call this", "we call it the", "we call it",
        };

        /// <summary>
        /// Words that complete a naming-shaped sentence but are not names - "this is clean" is a
        /// statement about the floor, not an attempt to rename the room.
        /// </summary>
        static readonly HashSet<string> NotNames = new HashSet<string>
        {
            "clean", "cleaned", "done", "finished", "good", "great", "fine", "nice", "better", "worse",
            "ok", "okay", "right", "wrong", "empty", "full", "busy", "messy", "dirty", "ready",
            "it", "this", "that", "one", "here", "there", "everything", "anything", "nothing",
        };

        /// <summary>
        /// Words that end a name when they turn up, so "this is the kitchen and hall" names the
        /// kitchen rather than the kitchen and the hall.
        /// </summary>
        static readonly HashSet<string> Stoppers = new HashSet<string>
        {
            "and", "but", "because", "so", "please", "thanks", "thank", "ok", "okay", "now", "then",
            "there", "here", "with", "for", "of", "from", "to", "is", "was", "are", "by", "or",
        };

        /// <summary>Strips the leading article so "the kitchen" names the room "Kitchen".</summary>
        static readonly HashSet<string> Articles = new HashSet<string>
        {
            "the", "a", "an", "our", "my", "your", "his", "her", "their",
        };

        const int MaxWords = 3;
        const int MaxWordLength = 16;
        const int MaxNameLength = 32;

        /// <summary>Extracts a room name from a raw transcript, or returns false if it names nothing.</summary>
        public static bool TryExtract(string transcript, out string name)
        {
            return TryFromNormalized(VoiceCommandRouter.Normalize(transcript), out name);
        }

        /// <summary>
        /// Extracts a room name from a body that has already been through
        /// <see cref="VoiceCommandRouter.Normalize"/> (lower-cased, punctuation to spaces, padded).
        /// The router uses this so a sentence addressed to ORAC can still name a room.
        /// </summary>
        public static bool TryFromNormalized(string paddedBody, out string name)
        {
            name = null;
            if (string.IsNullOrEmpty(paddedBody)) return false;

            for (int i = 0; i < Prefixes.Length; i++)
            {
                string needle = " " + Prefixes[i] + " ";
                int at = paddedBody.IndexOf(needle, System.StringComparison.Ordinal);
                if (at < 0) continue;

                // A prefix that matched but left nothing usable does not end the search: another
                // phrasing further along the sentence may still name the room.
                if (TryCleanName(paddedBody.Substring(at + needle.Length), out name)) return true;
            }

            return false;
        }

        static bool TryCleanName(string rest, out string name)
        {
            name = null;
            if (string.IsNullOrEmpty(rest)) return false;

            var words = new List<string>(MaxWords);
            int start = 0;
            bool sawArticle = false;

            for (int i = 0; i <= rest.Length; i++)
            {
                if (i != rest.Length && rest[i] != ' ') continue;
                if (i == start) { start = i + 1; continue; }

                string word = rest.Substring(start, i - start);
                start = i + 1;

                // An article before any other word is part of the phrasing, not the name.
                if (!sawArticle && words.Count == 0 && Articles.Contains(word))
                {
                    sawArticle = true;
                    continue;
                }

                if (Stoppers.Contains(word)) break;
                if (word.Length > MaxWordLength) return false;

                words.Add(word);
                if (words.Count == MaxWords) break;
            }

            if (words.Count == 0) return false;

            string joined = string.Join(" ", words);
            if (joined.Length > MaxNameLength) return false;
            if (words.Count == 1 && NotNames.Contains(joined)) return false;

            name = TitleCase(words);
            return true;
        }

        static string TitleCase(List<string> words)
        {
            var sb = new StringBuilder(MaxNameLength);
            for (int i = 0; i < words.Count; i++)
            {
                if (i > 0) sb.Append(' ');

                string w = words[i];
                if (w.Length == 0) continue;

                sb.Append(char.ToUpperInvariant(w[0]));
                if (w.Length > 1) sb.Append(w, 1, w.Length - 1);
            }

            return sb.ToString();
        }
    }
}
