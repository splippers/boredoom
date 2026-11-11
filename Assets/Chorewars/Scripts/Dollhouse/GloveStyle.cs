using System;
using UnityEngine;

namespace Chorewars.Dollhouse
{
    public enum GloveColour { Pink, Yellow, SkyBlue, Black }

    /// <summary>The player's chosen glove colour (marigold yellow by default), remembered between runs.</summary>
    public static class GloveStyle
    {
        const string Key = "glove.colour";
        static bool _loaded;
        static GloveColour _current = GloveColour.Yellow;

        public static event Action Changed;
        public static readonly GloveColour[] All = { GloveColour.Pink, GloveColour.Yellow, GloveColour.SkyBlue, GloveColour.Black };

        public static GloveColour Current
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;
                    try { var v = PlayerPrefs.GetInt(Key, (int)GloveColour.Yellow); if (Enum.IsDefined(typeof(GloveColour), v)) _current = (GloveColour)v; }
                    catch { /* keep the default */ }
                }
                return _current;
            }
        }

        public static void Set(GloveColour colour)
        {
            _loaded = true;
            if (_current == colour) { Changed?.Invoke(); return; }
            _current = colour;
            try { PlayerPrefs.SetInt(Key, (int)colour); PlayerPrefs.Save(); } catch { /* not remembered */ }
            Changed?.Invoke();
        }

        public static Color Tint(GloveColour colour)
        {
            switch (colour)
            {
                case GloveColour.Pink: return new Color(1f, 0.42f, 0.72f);
                case GloveColour.SkyBlue: return new Color(0.42f, 0.74f, 1f);
                case GloveColour.Black: return new Color(0.07f, 0.07f, 0.08f);
                default: return new Color(1f, 0.78f, 0.08f); // marigold
            }
        }

        public static string Label(GloveColour colour)
        {
            switch (colour)
            {
                case GloveColour.SkyBlue: return "SKY BLUE";
                case GloveColour.Pink: return "PINK";
                case GloveColour.Black: return "BLACK";
                default: return "YELLOW";
            }
        }
    }
}
