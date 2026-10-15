using System.Collections.Generic;
using UnityEngine;

namespace Chorewars.HandsFree
{
    /// <summary>
    /// The one place requests arrive. Layers feed in - the automatic director, voice, gaze,
    /// nodding - and it decides which of them the player actually meant, because the layers
    /// will disagree. A spoken command always outranks a guess; a deliberate nod beats a
    /// dwell; and an automatic "the room looks finished" is the weakest voice in the room.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public class HandsFreeInput : MonoBehaviour
    {
        public bool Enabled = true;

        /// <summary>How long a higher-ranked source keeps suppressing the lower ones.</summary>
        public float SourceLockout = 0.6f;

        public NodShakeDetector Nod;

        /// <summary>Raised for every accepted request, in rank order.</summary>
        public event System.Action<IntentRequest> Intent;

        IntentSource _lastSource = IntentSource.Unknown;
        float _lastAt = -99f;

        public IntentRequest LastRequest { get; private set; }
        public string LastAction { get; private set; }
        public string LastTranscript { get; private set; }

        void Awake()
        {
            if (Nod == null) Nod = GetComponent<NodShakeDetector>();
        }

        void OnEnable()
        {
            if (Nod != null) Nod.Gesture += OnHeadGesture;
        }

        void OnDisable()
        {
            if (Nod != null) Nod.Gesture -= OnHeadGesture;
        }

        void OnHeadGesture(HeadGesture gesture)
        {
            if (gesture == HeadGesture.Nod)
            {
                Submit(new IntentRequest
                {
                    Intent = ChorewarsIntent.Confirm,
                    Source = IntentSource.Nod,
                    Confidence = 0.7f
                });
            }
            else if (gesture == HeadGesture.Shake)
            {
                Submit(new IntentRequest
                {
                    Intent = ChorewarsIntent.Cancel,
                    Source = IntentSource.Nod,
                    Confidence = 0.7f
                });
            }
        }

        /// <summary>Hand a transcript to the router and, if it parses, submit it.</summary>
        public bool SubmitHeard(string transcript, IList<string> modeNames)
        {
            LastTranscript = transcript;

            IntentRequest request;
            if (!VoiceCommandRouter.TryParse(transcript, modeNames, out request)) return false;
            return Submit(request);
        }

        public bool Submit(IntentRequest request)
        {
            if (!Enabled) return false;
            if (request.Source == IntentSource.Unknown) return false;
            if (request.Intent == ChorewarsIntent.None) return false;

            float now = Time.unscaledTime;

            // A weaker source must stay out of the way of a stronger one that has just spoken.
            if (Rank(request.Source) < Rank(_lastSource) && now - _lastAt < SourceLockout) return false;

            _lastSource = request.Source;
            _lastAt = now;
            LastRequest = request;
            LastAction = request.ToString();

            if (Intent != null) Intent(request);
            return true;
        }

        public static int Rank(IntentSource source)
        {
            switch (source)
            {
                case IntentSource.Auto: return 0;
                case IntentSource.Gaze: return 1;
                case IntentSource.Nod: return 2;
                case IntentSource.Voice: return 3;
                case IntentSource.Controller: return 4;
                default: return -1;
            }
        }
    }
}
