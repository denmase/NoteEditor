namespace JianpuEditor.Models
{
    /// <summary>
    /// A single melody note reduced to what the enhanced (Markov) harmony engine needs: its
    /// diatonic scale degree and duration in beats. Built from a measure's <see cref="JianpuNote"/>
    /// sequence by <see cref="Services.JianpuNoteAdapter"/>, which folds continuation-dot rests
    /// into the duration of the note they extend rather than dropping them.
    /// </summary>
    public sealed class HarmonyMelodyNote
    {
        /// <summary>Diatonic scale degree (1..7).</summary>
        public int Degree { get; set; }

        /// <summary>Duration in beats.</summary>
        public double Duration { get; set; } = 1.0;
    }
}
