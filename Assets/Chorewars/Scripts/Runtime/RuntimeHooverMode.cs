namespace Chorewars.Runtime
{
    /// <summary>
    /// Standard Coverage. Plain hoovering: clean as much of the floor as you can before the clock
    /// runs out. It is the reference mode — every other mode is this loop plus a twist — so it adds
    /// no mechanic of its own and only supplies its profile key and display name.
    /// </summary>
    public sealed class RuntimeHooverMode : RuntimeCoverageMode
    {
        public override string ProfileModeId => "hoover";

        public override string ModeDisplayName => "Standard Coverage";
    }
}
