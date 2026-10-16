namespace Chorewars.Voice
{
    /// <summary>
    /// Decides when the player has started and stopped talking, from loudness alone.
    /// </summary>
    /// <remarks>
    /// Hands-free means no button, so something has to notice that a sentence has finished, and it has
    /// to notice while the microphone is still running. This is deliberately the simplest thing that
    /// works in a quiet room: a frame louder than a floor counts as speech, and speech is over after a
    /// stretch of quiet. Recognising words is the PC's job; all this does is decide when to stop
    /// sending and say "that was the end of it".
    ///
    /// Pure and clock-free: the caller passes the frame's duration, so a test can run a whole
    /// conversation through it without waiting in real time.
    /// </remarks>
    public sealed class EnergyVad
    {
        private readonly float _threshold;
        private readonly float _endSilenceSeconds;

        public EnergyVad(float threshold = 0.02f, float endSilenceSeconds = 0.8f)
        {
            _threshold = threshold;
            _endSilenceSeconds = endSilenceSeconds;
        }

        /// <summary>True while a sentence is in progress.</summary>
        public bool Speaking { get; private set; }

        /// <summary>How long the current stretch of quiet has lasted, in seconds.</summary>
        public float SilenceSeconds { get; private set; }

        /// <summary>Loudness of the frame most recently pushed, for a level meter.</summary>
        public float LastLevel { get; private set; }

        public void Reset()
        {
            Speaking = false;
            SilenceSeconds = 0f;
            LastLevel = 0f;
        }

        /// <summary>
        /// Sorts one frame. Returns the <see cref="VadEvent"/> the caller should act on: speech has
        /// begun, or it has ended and the utterance should be closed.
        /// </summary>
        /// <remarks>
        /// The start and the end are reported once each, on the frame they happen, not for as long as
        /// they last — otherwise a caller that streams while speech is detected would open a new
        /// utterance on every frame, and one that closes on silence would send END over and over.
        /// </remarks>
        public VadEvent Push(float level, float frameSeconds)
        {
            LastLevel = level;
            frameSeconds = frameSeconds < 0f ? 0f : frameSeconds;

            if (level >= _threshold)
            {
                SilenceSeconds = 0f;

                if (Speaking) return VadEvent.Speaking;

                Speaking = true;
                return VadEvent.Started;
            }

            if (!Speaking) return VadEvent.Idle;

            SilenceSeconds += frameSeconds;

            if (SilenceSeconds < _endSilenceSeconds) return VadEvent.Speaking;

            Speaking = false;
            return VadEvent.Ended;
        }

        /// <summary>Sorts one frame of raw PCM.</summary>
        public VadEvent Push(byte[] pcmS16le, float frameSeconds)
        {
            return Push(PcmAudio.Rms(pcmS16le), frameSeconds);
        }
    }

    public enum VadEvent
    {
        /// <summary>Quiet, and no sentence in progress.</summary>
        Idle = 0,

        /// <summary>Speech has just begun.</summary>
        Started,

        /// <summary>Speech continues.</summary>
        Speaking,

        /// <summary>Speech has just stopped for long enough; the utterance is over.</summary>
        Ended,
    }
}
