using System;
using System.Collections.Generic;

namespace JianpuEditor.Models
{
    public sealed class ScoreSelectionInfo
    {
        public int MeasureIndex { get; set; } = -1;

        /// <summary><see cref="ScoreNoteRef.PrimaryVoiceIndex"/> (the default) for a selection on
        /// <see cref="JianpuMeasure.MelodyNotes"/>, otherwise an index into <see
        /// cref="JianpuMeasure.ExtraVoices"/>. Applies to <see cref="NoteIndex"/> and <see
        /// cref="InsertIndex"/> alike -- a single-note or single-gap selection is always on one
        /// voice at a time (a multi-note selection instead carries its own per-entry voice via
        /// <see cref="SelectedNotes"/>).</summary>
        public int VoiceIndex { get; set; } = ScoreNoteRef.PrimaryVoiceIndex;

        public int NoteIndex { get; set; } = -1;

        public int InsertIndex { get; set; } = -1;

        public int TieIndex { get; set; } = -1;

        public int ChordMeasureIndex { get; set; } = -1;

        public int ChordMarkerIndex { get; set; } = -1;

        public IReadOnlyList<int> SelectedMeasureIndices { get; set; } = Array.Empty<int>();

        public IReadOnlyList<ScoreNoteRef> SelectedNotes { get; set; } = Array.Empty<ScoreNoteRef>();

        public bool HasNoteSelected
        {
            get { return SelectedNotes != null && SelectedNotes.Count > 0 || NoteIndex >= 0; }
        }

        public bool HasMultipleNotesSelected
        {
            get { return SelectedNotes != null && SelectedNotes.Count > 1; }
        }

        public bool HasGapSelected
        {
            get { return InsertIndex >= 0; }
        }

        public bool HasTieSelected
        {
            get { return TieIndex >= 0; }
        }

        public bool HasChordSelected
        {
            get { return ChordMarkerIndex >= 0; }
        }
    }
}
