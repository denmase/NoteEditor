using System.Collections.Generic;

namespace JianpuEditor.Models
{
    public class JianpuMeasure
    {
        public const int MaxChordMarkers = 4;

        public List<JianpuNote> MelodyNotes { get; set; } = new List<JianpuNote>();

        /// <summary>Independent voices beyond <see cref="MelodyNotes"/> -- SATB's Alto/Tenor/
        /// Bass, and/or a descant/solo line. Empty by default, so an existing score with no
        /// extra voices renders/plays exactly as it did before this field existed.</summary>
        public List<JianpuVoice> ExtraVoices { get; set; } = new List<JianpuVoice>();

        public List<JianpuChord> Chords { get; set; } = new List<JianpuChord>();

        public List<ChordMarker> ChordMarkers { get; set; } = new List<ChordMarker>();

        public List<LyricSyllable> LyricSyllables { get; set; } = new List<LyricSyllable>();

        public List<JianpuOrnament> Ornaments { get; set; } = new List<JianpuOrnament>();

        public List<DynamicMarking> Dynamics { get; set; } = new List<DynamicMarking>();

        public string LyricText { get; set; } = string.Empty;

        /// <summary>The bar line drawn at this measure's right edge.</summary>
        public BarLineType BarLineType { get; set; } = BarLineType.Single;

        /// <summary>Whether a repeat-start bar line is drawn at this measure's left edge.</summary>
        public bool IsRepeatStart { get; set; }
    }
}
