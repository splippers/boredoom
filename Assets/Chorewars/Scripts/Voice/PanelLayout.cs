using UnityEngine;

namespace Chorewars.Voice
{
    /// <summary>
    /// Where everything on the instruction panel goes, as named regions in the panel's own space (metres,
    /// +y up, centre of the panel at the origin).
    /// </summary>
    /// <remarks>
    /// Every region sits inside <see cref="Background"/> and no two regions overlap, so nothing on the panel
    /// is drawn on top of anything else. The test <c>PanelLayoutTests</c> fails if a change breaks that.
    /// Text is placed against a region's left edge and is kept short enough to stay within it. The one
    /// deliberate stack is a button's own word, which sits a centimetre in front of its face.
    /// </remarks>
    public static class PanelLayout
    {
        public static readonly Rect Background = new Rect(-0.46f, -0.35f, 0.92f, 0.61f);

        // Top to bottom.
        public static readonly Rect HeaderDot = new Rect(0.10f, 0.20f, 0.05f, 0.05f);
        public static readonly Rect HeaderText = new Rect(0.16f, 0.20f, 0.26f, 0.05f);
        public static readonly Rect Body = new Rect(-0.42f, -0.01f, 0.84f, 0.20f);
        public static readonly Rect WaveMarker = new Rect(-0.42f, -0.105f, 0.015f, 0.085f);
        public static readonly Rect WaveBars = new Rect(-0.40f, -0.105f, 0.82f, 0.085f);
        public static readonly Rect RepeatButton = new Rect(-0.38f, -0.23f, 0.36f, 0.12f);
        public static readonly Rect TalkButton = new Rect(0.02f, -0.23f, 0.36f, 0.12f);
        public static readonly Rect RepeatProgress = new Rect(-0.38f, -0.255f, 0.36f, 0.02f);
        public static readonly Rect TalkProgress = new Rect(0.02f, -0.255f, 0.36f, 0.02f);
        public static readonly Rect Thinking = new Rect(-0.42f, -0.295f, 0.84f, 0.03f);
        public static readonly Rect Status = new Rect(-0.42f, -0.34f, 0.84f, 0.035f);

        /// <summary>Every region except the background, for checking that none of them collide.</summary>
        public static Rect[] Regions => new[]
        {
            HeaderDot, HeaderText, Body, WaveMarker, WaveBars,
            RepeatButton, TalkButton, RepeatProgress, TalkProgress, Thinking, Status,
        };

        public static string[] Names => new[]
        {
            nameof(HeaderDot), nameof(HeaderText), nameof(Body), nameof(WaveMarker), nameof(WaveBars),
            nameof(RepeatButton), nameof(TalkButton), nameof(RepeatProgress), nameof(TalkProgress),
            nameof(Thinking), nameof(Status),
        };

        public static Vector2 Left(Rect r, float inset = 0f) => new Vector2(r.xMin + inset, r.center.y);
        public static Vector2 TopLeft(Rect r) => new Vector2(r.xMin, r.yMax);
    }
}
