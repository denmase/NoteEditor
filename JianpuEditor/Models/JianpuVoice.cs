using System.Collections.Generic;

namespace JianpuEditor.Models
{
    /// <summary>One independent voice beyond a measure's primary <see cref="JianpuMeasure.
    /// MelodyNotes"/> (SATB's Alto/Tenor/Bass, or a descant/solo line). Voices render in the
    /// order they're listed in <see cref="JianpuMeasure.ExtraVoices"/>, split only by <see
    /// cref="IsAbove"/>: every above-voice first (in list order), then <c>MelodyNotes</c>, then
    /// the rest of <c>ExtraVoices</c> in list order -- the render loop needs no other
    /// special-casing per voice, validated with a real 5-voice (descant + SATB) prototype
    /// render before this model was added.</summary>
    public sealed class JianpuVoice
    {
        /// <summary>Display label for this voice's row (e.g. "Alto", "Descant") -- shown in the
        /// row-label gutter the way "Melody"/"Secondary"/"Lyrics" already are.</summary>
        public string Role { get; set; } = string.Empty;

        public List<JianpuNote> Notes { get; set; } = new List<JianpuNote>();

        /// <summary>True for a descant/solo line that renders above the measure's primary voice
        /// (real notation convention); false for Alto/Tenor/Bass-style voices that render below
        /// it.</summary>
        public bool IsAbove { get; set; }
    }
}
