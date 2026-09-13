namespace JianpuEditor.Models
{
    public sealed class ChordMarker
    {
        public string Text { get; set; } = string.Empty;

        /// <summary>Quarter-beat position within the measure (0 = beat 1), aligned with the melody's quarter notes.</summary>
        public double BeatPosition { get; set; }
    }
}
