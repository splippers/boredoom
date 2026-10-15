namespace Chorewars.HandsFree
{
    /// <summary>
    /// Everything the player can ask for without using their hands. The chore loop itself
    /// needs none of these; they exist for the exceptions.
    /// </summary>
    public enum ChorewarsIntent
    {
        None = 0,

        StartScan,
        FinishScan,
        ExportModel,
        Snapshot,
        ClearMeshes,
        BakeTexture,

        NextMode,
        PreviousMode,
        SelectMode,

        MarkClean,
        Undo,
        Pause,
        Resume,

        Confirm,
        Cancel,
        CallOrac,
        TakeNote
    }

    /// <summary>
    /// Where a request came from. Ranked, because the layers disagree: an automatic
    /// "the room stopped growing" must never override a player who has just spoken.
    /// </summary>
    public enum IntentSource
    {
        Unknown = 0,
        Auto = 1,
        Gaze = 2,
        Nod = 3,
        Voice = 4,
        Controller = 5
    }

    public struct IntentRequest
    {
        public ChorewarsIntent Intent;
        public IntentSource Source;
        public string Argument;
        public float Confidence;
        public string Transcript;

        public override string ToString()
        {
            var text = Source + ":" + Intent;
            if (!string.IsNullOrEmpty(Argument)) text += "(" + Argument + ")";
            return text;
        }
    }
}
