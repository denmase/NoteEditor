using System;

namespace JianpuEditor.Models
{
    public readonly struct ScoreNoteRef : IEquatable<ScoreNoteRef>
    {
        /// <summary>Matches <see cref="Services.VoiceLayoutService.PrimaryVoiceIndex"/> -- the
        /// default, so every existing two-argument call site keeps referring to the primary voice
        /// (<see cref="JianpuMeasure.MelodyNotes"/>) exactly as it always has.</summary>
        public const int PrimaryVoiceIndex = -1;

        public ScoreNoteRef(int measureIndex, int noteIndex, int voiceIndex = PrimaryVoiceIndex)
        {
            MeasureIndex = measureIndex;
            NoteIndex = noteIndex;
            VoiceIndex = voiceIndex;
        }

        public int MeasureIndex { get; }

        public int NoteIndex { get; }

        /// <summary><see cref="PrimaryVoiceIndex"/> for <see cref="JianpuMeasure.MelodyNotes"/>,
        /// otherwise an index into <see cref="JianpuMeasure.ExtraVoices"/>.</summary>
        public int VoiceIndex { get; }

        public bool Equals(ScoreNoteRef other)
        {
            return MeasureIndex == other.MeasureIndex && NoteIndex == other.NoteIndex && VoiceIndex == other.VoiceIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is ScoreNoteRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = MeasureIndex * 397;
                hash = (hash ^ NoteIndex) * 397;
                return hash ^ VoiceIndex;
            }
        }

        public static bool operator ==(ScoreNoteRef left, ScoreNoteRef right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ScoreNoteRef left, ScoreNoteRef right)
        {
            return !left.Equals(right);
        }

        /// <summary>Orders by measure, then voice (primary first), then note index -- a
        /// multi-select range never spans voices (see <see
        /// cref="Models.NoteSelectionRange.Enumerate"/>), so within one measure this keeps every
        /// voice's own notes contiguous and in note order.</summary>
        public static int Compare(ScoreNoteRef left, ScoreNoteRef right)
        {
            var measureCompare = left.MeasureIndex.CompareTo(right.MeasureIndex);
            if (measureCompare != 0)
            {
                return measureCompare;
            }

            var voiceCompare = left.VoiceIndex.CompareTo(right.VoiceIndex);
            return voiceCompare != 0 ? voiceCompare : left.NoteIndex.CompareTo(right.NoteIndex);
        }
    }
}
