namespace JianpuEditor.Models
{
    public sealed class LyricSyllable
    {
        public string Text { get; set; } = string.Empty;

        /// <summary>Index of the melody note within the measure (0 = first note). -1 means aligned by beat position only.</summary>
        public int NoteIndex { get; set; } = -1;

        /// <summary>Quarter-beat position within the measure (0 = beat 1). Synced by the normalization logic when NoteIndex is valid.</summary>
        public double BeatPosition { get; set; }
    }
}
