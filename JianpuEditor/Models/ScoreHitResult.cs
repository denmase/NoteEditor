namespace JianpuEditor.Models
{
    public enum ScoreHitType
    {
        None,
        Note,
        Gap,
        Tie,
        ChordMarker,
        ChordDragHandle,
        ChordDelete,
        ChordAddSlot,
        ChordRow,
        LyricText,
        Measure,
        ScoreHeader
    }

    public sealed class ScoreHitResult
    {
        public ScoreHitType HitType { get; set; } = ScoreHitType.None;

        public int MeasureIndex { get; set; } = -1;

        /// <summary><see cref="ScoreNoteRef.PrimaryVoiceIndex"/> for <see
        /// cref="Models.JianpuMeasure.MelodyNotes"/> (the default -- every existing hit result that
        /// never set this keeps meaning the primary voice), otherwise an index into <see
        /// cref="Models.JianpuMeasure.ExtraVoices"/>. Set on <see cref="ScoreHitType.Note"/> and
        /// <see cref="ScoreHitType.Gap"/> hits.</summary>
        public int VoiceIndex { get; set; } = ScoreNoteRef.PrimaryVoiceIndex;

        public int NoteIndex { get; set; } = -1;

        /// <summary>Insertion index in MelodyNotes when HitType is Gap.</summary>
        public int InsertIndex { get; set; } = -1;

        /// <summary>Index in Score.Ties when HitType is Tie.</summary>
        public int TieIndex { get; set; } = -1;

        /// <summary>Index in Measure.ChordMarkers when HitType is chord-related.</summary>
        public int ChordMarkerIndex { get; set; } = -1;

        public IntRect Bounds { get; set; } = IntRect.Empty;

        public ScoreHeaderField HeaderField { get; set; } = ScoreHeaderField.None;
    }
}
