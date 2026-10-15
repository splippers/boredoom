using System.Collections.Generic;
using System.Text;

namespace Chorewars.HandsFree
{
    /// <summary>
    /// Turns a transcript into an intent. Deliberately a pure function of the words, with
    /// no microphone, no Android and no MonoBehaviour involved, so it can be tested by
    /// feeding it sentences rather than by shouting at a headset in a kitchen.
    ///
    /// It is a router, not a brain: anything it cannot place is handed to ORAC, who is
    /// better company than "I did not understand that".
    /// </summary>
    public static class VoiceCommandRouter
    {
        struct Rule
        {
            public ChorewarsIntent Intent;
            public string[] Phrases;
        }

        // Ordered: the specific phrases must be tested before the loose single words.
        static readonly Rule[] Rules =
        {
            R(ChorewarsIntent.Cancel, "never mind", "nevermind", "forget it", "cancel that", "that was wrong"),
            R(ChorewarsIntent.TakeNote, "take a note", "make a note", "note that", "remember that", "write this down"),

            R(ChorewarsIntent.FinishScan, "finish the scan", "finish scan", "stop scanning", "stop the scan",
                "end the scan", "done scanning", "that is the whole room", "thats the whole room", "i have finished", "im finished"),
            R(ChorewarsIntent.StartScan, "start the scan", "start scan", "begin the scan", "begin scan",
                "scan the room", "scan this room", "start scanning", "start recording"),

            R(ChorewarsIntent.ExportModel, "export the model", "export model", "export obj", "save the model", "save the room"),
            R(ChorewarsIntent.Snapshot, "take a snapshot", "take snapshot", "snapshot", "capture the room"),
            R(ChorewarsIntent.ClearMeshes, "clear the meshes", "clear meshes", "wipe the mesh", "start over"),
            R(ChorewarsIntent.BakeTexture, "bake the texture", "bake texture"),

            R(ChorewarsIntent.NextMode, "next mode", "next game", "next chore", "next one", "next"),
            R(ChorewarsIntent.PreviousMode, "previous mode", "last mode", "go back", "back"),

            R(ChorewarsIntent.MarkClean, "that is clean", "thats clean", "this is clean", "that is done",
                "this is done", "all clean", "it is clean", "satisfied with this", "i am happy with this"),

            R(ChorewarsIntent.Undo, "undo that", "take that back", "put it back", "undo"),
            R(ChorewarsIntent.Pause, "hold on", "wait a moment", "give me a second", "pause"),
            R(ChorewarsIntent.Resume, "carry on", "continue", "resume", "keep going"),

            R(ChorewarsIntent.Confirm, "yes", "yep", "confirm", "do it", "go ahead", "affirmative", "correct"),
            R(ChorewarsIntent.Cancel, "no", "nope", "negative", "do not", "dont")
        };

        static readonly string[] ModeTriggers =
        {
            "play", "switch to", "switch mode to", "mode", "select", "choose", "start", "lets do", "let us do"
        };

        static Rule R(ChorewarsIntent intent, params string[] phrases)
        {
            return new Rule { Intent = intent, Phrases = phrases };
        }

        /// <summary>
        /// Parse a transcript. <paramref name="modeNames"/> may be null; if supplied, the
        /// player can name a mode directly ("play hoover") and get a SelectMode request.
        /// </summary>
        public static bool TryParse(string transcript, IList<string> modeNames, out IntentRequest request)
        {
            request = default(IntentRequest);
            if (string.IsNullOrEmpty(transcript)) return false;

            string body = Normalize(transcript);
            if (body.Length == 0) return false;

            bool addressed = false;
            if (Contains(body, "hey orac")) { body = Remove(body, "hey orac"); addressed = true; }
            else if (Contains(body, "orac")) { body = Remove(body, "orac"); addressed = true; }

            // "ORAC" on its own: he was addressed and nothing else was said.
            if (Trim(body).Length == 0)
            {
                if (!addressed) return false;
                request = Make(ChorewarsIntent.CallOrac, transcript);
                return true;
            }

            if (TryParseMode(body, modeNames, out request, transcript)) return true;

            for (int i = 0; i < Rules.Length; i++)
            {
                var rule = Rules[i];
                if (!HasAny(body, rule.Phrases)) continue;
                request = Make(rule.Intent, transcript);
                return true;
            }

            // Addressed but unintelligible: give it to ORAC rather than failing silently.
            if (addressed)
            {
                request = Make(ChorewarsIntent.CallOrac, transcript);
                return true;
            }

            return false;
        }

        static bool TryParseMode(string body, IList<string> modeNames, out IntentRequest request, string transcript)
        {
            request = default(IntentRequest);
            if (modeNames == null || modeNames.Count == 0) return false;
            if (!HasAny(body, ModeTriggers)) return false;

            for (int i = 0; i < modeNames.Count; i++)
            {
                string name = modeNames[i];
                if (string.IsNullOrEmpty(name)) continue;
                if (!Contains(body, name)) continue;
                request = new IntentRequest
                {
                    Intent = ChorewarsIntent.SelectMode,
                    Source = IntentSource.Voice,
                    Argument = name,
                    Confidence = 0.9f,
                    Transcript = transcript
                };
                return true;
            }

            return false;
        }

        static IntentRequest Make(ChorewarsIntent intent, string transcript)
        {
            return new IntentRequest
            {
                Intent = intent,
                Source = IntentSource.Voice,
                Confidence = 1f,
                Transcript = transcript
            };
        }

        /// <summary>A short confirmation of what was understood, for the panel.</summary>
        public static string Describe(ChorewarsIntent intent, string argument)
        {
            switch (intent)
            {
                case ChorewarsIntent.StartScan: return "start the scan";
                case ChorewarsIntent.FinishScan: return "finish the scan";
                case ChorewarsIntent.ExportModel: return "export the room";
                case ChorewarsIntent.Snapshot: return "take a snapshot";
                case ChorewarsIntent.ClearMeshes: return "clear the meshes";
                case ChorewarsIntent.BakeTexture: return "bake the texture";
                case ChorewarsIntent.NextMode: return "next mode";
                case ChorewarsIntent.PreviousMode: return "previous mode";
                case ChorewarsIntent.SelectMode: return "switch to " + argument;
                case ChorewarsIntent.MarkClean: return "mark this clean";
                case ChorewarsIntent.Undo: return "undo";
                case ChorewarsIntent.Pause: return "pause";
                case ChorewarsIntent.Resume: return "resume";
                case ChorewarsIntent.Confirm: return "yes";
                case ChorewarsIntent.Cancel: return "no";
                case ChorewarsIntent.CallOrac: return "ask ORAC";
                case ChorewarsIntent.TakeNote: return "take a note";
                default: return intent.ToString();
            }
        }

        // --- text handling -------------------------------------------------------------

        /// <summary>Lower-case, punctuation to spaces, single-spaced, padded with one space each end.</summary>
        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return " ";
            var sb = new StringBuilder(s.Length + 2);
            bool pendingSpace = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = char.ToLowerInvariant(s[i]);

                // Apostrophes close a word rather than separating one: "that's" must
                // normalise to "thats" so it can match the phrase tables below.
                if (c == '\'' || c == '\u2019') continue;

                bool alnum = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                if (alnum)
                {
                    if (pendingSpace && sb.Length > 0) sb.Append(' ');
                    pendingSpace = false;
                    sb.Append(c);
                }
                else
                {
                    pendingSpace = true;
                }
            }

            if (sb.Length == 0) return " ";
            return " " + sb + " ";
        }

        static string Trim(string padded)
        {
            if (string.IsNullOrEmpty(padded)) return "";
            return padded.Trim();
        }

        static bool Contains(string paddedBody, string phrase)
        {
            return paddedBody.IndexOf(" " + Normalize(phrase).Trim() + " ", System.StringComparison.Ordinal) >= 0;
        }

        static bool HasAny(string paddedBody, string[] phrases)
        {
            for (int i = 0; i < phrases.Length; i++)
                if (Contains(paddedBody, phrases[i])) return true;
            return false;
        }

        static string Remove(string paddedBody, string phrase)
        {
            string needle = " " + Normalize(phrase).Trim() + " ";
            int at = paddedBody.IndexOf(needle, System.StringComparison.Ordinal);
            if (at < 0) return paddedBody;
            return paddedBody.Substring(0, at) + " " + paddedBody.Substring(at + needle.Length);
        }
    }
}
