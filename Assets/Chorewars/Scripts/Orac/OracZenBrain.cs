using System.Collections.Generic;

namespace Chorewars.Orac
{
    /// <summary>
    /// Transport for a model call. Deliberately abstract: the bridge implements it so the
    /// API key never has to live on the headset.
    /// </summary>
    public interface IOracLlmTransport
    {
        void Complete(string systemPrompt, string userPrompt, System.Action<string> onText);
    }

    /// <summary>
    /// ORAC's mind. He is a separate entity with his own identity, memory and manners,
    /// running on the ZEN Big Pickle engine - the same engine as the dev agents, but
    /// emphatically not the same personality, and he will thank you to remember it.
    /// </summary>
    public class OracZenBrain : IOracBrain
    {
        public const string Engine = "ZEN Big Pickle";

        readonly IOracLlmTransport _transport;
        readonly OracJournal _journal;

        public OracZenBrain(IOracLlmTransport transport, OracJournal journal)
        {
            _transport = transport;
            _journal = journal;
        }

        public bool IsConnected { get { return _transport != null; } }

        public void Ask(string question, System.Action<string> onReply)
        {
            if (_transport == null)
            {
                if (onReply != null) onReply(OfflineReply(question));
                return;
            }

            string user = "The dev team asks: " + question +
                          "\n\nAnswer as yourself, in two sentences at most.";
            _transport.Complete(OracPersona.SystemPrompt, user, r => Reply(onReply, r, question));
        }

        public void Diagnose(IList<OracFinding> findings, System.Action<string> onReply)
        {
            if (_transport == null)
            {
                var worst = OracDiagnosticVoice.Worst(findings);
                if (onReply != null) onReply(OracDiagnosticVoice.Speak(worst, 0));
                return;
            }

            _transport.Complete(OracPersona.SystemPrompt,
                OracDiagnosticVoice.BuildUserPrompt(findings, _journal),
                r => Reply(onReply, r, null));
        }

        void Reply(System.Action<string> onReply, string text, string originalQuestion)
        {
            if (onReply == null) return;
            if (string.IsNullOrEmpty(text))
            {
                onReply(OfflineReply(originalQuestion));
                return;
            }
            onReply(text.Trim());
        }

        string OfflineReply(string question)
        {
            if (!string.IsNullOrEmpty(question) && question.Length < 4)
                return OracPersona.Pick(OracLineContext.SmallTalk, 1);
            return OracPersona.Pick(OracLineContext.Refused, 2);
        }
    }
}
