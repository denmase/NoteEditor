namespace JianpuEditor.Models
{
    /// <summary>A dynamics marking (e.g. "mf", "f") anchored to a melody note, rendered in its own
    /// row below the melody and applied as a velocity level to that note and every one after it
    /// until the next marking or the end of the score.</summary>
    public sealed class DynamicMarking
    {
        public string Text { get; set; } = string.Empty;

        /// <summary>Index of the melody note this marking is anchored to (0 = first note). -1 means anchored by beat position only.</summary>
        public int NoteIndex { get; set; } = -1;

        /// <summary>Quarter-beat position within the measure (0 = beat 1). Synced by the normalization logic when NoteIndex is valid.</summary>
        public double BeatPosition { get; set; }
    }
}
