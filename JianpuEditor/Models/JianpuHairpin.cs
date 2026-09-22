namespace JianpuEditor.Models
{
    /// <summary>A crescendo/diminuendo wedge spanning two notes, mirroring <see cref="JianpuTie"/>'s
    /// score-level start/end-note shape rather than <see cref="DynamicMarking"/>'s single-point
    /// one, since a hairpin commonly spans across measure boundaries.</summary>
    public sealed class JianpuHairpin
    {
        public int StartMeasureIndex { get; set; }

        public int StartNoteIndex { get; set; }

        public int EndMeasureIndex { get; set; }

        public int EndNoteIndex { get; set; }

        /// <summary>True for a crescendo (getting louder, drawn as "&lt;"); false for a
        /// diminuendo (getting softer, drawn as "&gt;").</summary>
        public bool IsCrescendo { get; set; }
    }
}
