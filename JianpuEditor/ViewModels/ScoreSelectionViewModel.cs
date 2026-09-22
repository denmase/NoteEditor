using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using JianpuEditor.Models;

namespace JianpuEditor.ViewModels
{
    public sealed class ScoreSelectionViewModel : ObservableObject
    {
        private readonly ScoreDocumentViewModel _document;
        private int _measureIndex = -1;
        private int _voiceIndex = ScoreNoteRef.PrimaryVoiceIndex;
        private int _noteIndex = -1;
        private int _insertIndex = -1;
        private int _tieIndex = -1;
        private int _chordMeasureIndex = -1;
        private int _chordMarkerIndex = -1;
        private IReadOnlyList<int> _selectedMeasureIndices = Array.Empty<int>();
        private IReadOnlyList<ScoreNoteRef> _selectedNotes = Array.Empty<ScoreNoteRef>();

        public ScoreSelectionViewModel(ScoreDocumentViewModel document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public int MeasureIndex
        {
            get { return _measureIndex; }
            private set { SetProperty(ref _measureIndex, value); }
        }

        public int NoteIndex
        {
            get { return _noteIndex; }
            private set { SetProperty(ref _noteIndex, value); }
        }

        /// <summary><see cref="ScoreNoteRef.PrimaryVoiceIndex"/> for a selection on the primary
        /// voice (<see cref="JianpuMeasure.MelodyNotes"/>), otherwise an index into <see
        /// cref="JianpuMeasure.ExtraVoices"/>. Applies to <see cref="NoteIndex"/>/<see
        /// cref="InsertIndex"/> -- see <see cref="Models.ScoreSelectionInfo.VoiceIndex"/>.</summary>
        public int VoiceIndex
        {
            get { return _voiceIndex; }
            private set { SetProperty(ref _voiceIndex, value); }
        }

        public int InsertIndex
        {
            get { return _insertIndex; }
            private set { SetProperty(ref _insertIndex, value); }
        }

        public int TieIndex
        {
            get { return _tieIndex; }
            private set { SetProperty(ref _tieIndex, value); }
        }

        public int ChordMeasureIndex
        {
            get { return _chordMeasureIndex; }
            private set { SetProperty(ref _chordMeasureIndex, value); }
        }

        public int ChordMarkerIndex
        {
            get { return _chordMarkerIndex; }
            private set { SetProperty(ref _chordMarkerIndex, value); }
        }

        public IReadOnlyList<int> SelectedMeasureIndices
        {
            get { return _selectedMeasureIndices; }
            private set { SetProperty(ref _selectedMeasureIndices, value); }
        }

        public IReadOnlyList<ScoreNoteRef> SelectedNotes
        {
            get { return _selectedNotes; }
            private set { SetProperty(ref _selectedNotes, value); }
        }

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

        public void UpdateFrom(ScoreSelectionInfo info)
        {
            if (info == null)
            {
                return;
            }

            MeasureIndex = info.MeasureIndex;
            VoiceIndex = info.VoiceIndex;
            NoteIndex = info.NoteIndex;
            InsertIndex = info.InsertIndex;
            TieIndex = info.TieIndex;
            ChordMeasureIndex = info.ChordMeasureIndex;
            ChordMarkerIndex = info.ChordMarkerIndex;
            SelectedMeasureIndices = info.SelectedMeasureIndices ?? Array.Empty<int>();
            SelectedNotes = info.SelectedNotes ?? Array.Empty<ScoreNoteRef>();
        }

        public string BuildSelectionDescription()
        {
            if (HasTieSelected
                && _document.Score.Ties != null
                && TieIndex >= 0
                && TieIndex < _document.Score.Ties.Count)
            {
                var tie = _document.Score.Ties[TieIndex];
                return "Tie selected: measure " + (tie.StartMeasureIndex + 1) + " note " + (tie.StartNoteIndex + 1) +
                       " → measure " + (tie.EndMeasureIndex + 1) + " note " + (tie.EndNoteIndex + 1) +
                       ". Click \"Delete\" to remove it.";
            }

            if (HasChordSelected
                && ChordMeasureIndex >= 0
                && ChordMeasureIndex < _document.Score.Measures.Count)
            {
                var measure = _document.Score.Measures[ChordMeasureIndex];
                if (ChordMarkerIndex >= 0 && ChordMarkerIndex < measure.ChordMarkers.Count)
                {
                    var marker = measure.ChordMarkers[ChordMarkerIndex];
                    return "Chord marker selected: measure " + (ChordMeasureIndex + 1) + ", marker " +
                           (ChordMarkerIndex + 1) + ", beat position " + (marker.BeatPosition + 1) +
                           ". Drag \"::\" to reposition, or press Delete/\"Delete\" to remove it.";
                }
            }

            if (HasMultipleNotesSelected)
            {
                var minMeasure = SelectedNotes.Min(note => note.MeasureIndex);
                var maxMeasure = SelectedNotes.Max(note => note.MeasureIndex);
                if (minMeasure != maxMeasure)
                {
                    return "Selected " + SelectedNotes.Count + " note(s) (measures " + (minMeasure + 1) +
                           " to " + (maxMeasure + 1) + "). Use the buttons above to edit them in bulk.";
                }

                return "Selected " + SelectedNotes.Count + " note(s). Use the buttons above to edit them in bulk.";
            }

            if (SelectedMeasureIndices != null && SelectedMeasureIndices.Count > 1)
            {
                return "Selected measures " + (SelectedMeasureIndices.Min() + 1) + " to " +
                       (SelectedMeasureIndices.Max() + 1) + ". Click \"Duplicate Measure\" to copy them.";
            }

            if (HasNoteSelected)
            {
                return "Selected note " + (NoteIndex + 1) + " in measure " + (MeasureIndex + 1) + ". Use the buttons above to edit it.";
            }

            if (HasGapSelected)
            {
                return "Selected insert position " + (InsertIndex + 1) + " in measure " + (MeasureIndex + 1) + ". Use the buttons above to insert a note.";
            }

            if (MeasureIndex >= 0)
            {
                return "Now editing measure " + (MeasureIndex + 1) + ". Click an empty beat in the secondary melody to add a chord, or click the lyric line to edit the text.";
            }

            return string.Empty;
        }

        public (int fromIndex, int toIndex) GetMeasureRangeIndices()
        {
            if (SelectedMeasureIndices == null || SelectedMeasureIndices.Count == 0)
            {
                var index = Math.Max(0, MeasureIndex);
                return (index, index);
            }

            return (SelectedMeasureIndices.Min(), SelectedMeasureIndices.Max());
        }

        public bool TryGetContiguousMeasureRange(out int fromIndex, out int toIndex)
        {
            fromIndex = Math.Max(0, MeasureIndex);
            toIndex = fromIndex;
            if (SelectedMeasureIndices == null || SelectedMeasureIndices.Count <= 1)
            {
                return false;
            }

            var sorted = SelectedMeasureIndices.OrderBy(index => index).ToList();
            for (var i = 1; i < sorted.Count; i++)
            {
                if (sorted[i] - sorted[i - 1] != 1)
                {
                    return false;
                }
            }

            fromIndex = sorted[0];
            toIndex = sorted[sorted.Count - 1];
            return toIndex > fromIndex;
        }
    }
}
