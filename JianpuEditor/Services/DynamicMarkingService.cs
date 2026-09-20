using System;
using System.Collections.Generic;
using System.Linq;
using JianpuEditor.Models;
using JianpuEditor.Rendering;

namespace JianpuEditor.Services
{
    /// <summary>Mirrors the note-index/beat-position resolution pattern <see cref="OrnamentService"/>
    /// uses, but with "at most one marking per note" toggle/replace semantics instead of ornaments'
    /// additive "several types can coexist on one note" semantics -- a note can't be both piano and
    /// forte at the same instant.</summary>
    public static class DynamicMarkingService
    {
        public static void NormalizeMeasure(JianpuMeasure measure)
        {
            if (measure == null)
            {
                return;
            }

            if (measure.Dynamics == null)
            {
                measure.Dynamics = new List<DynamicMarking>();
            }

            TrimAndSort(measure);
        }

        public static int ResolveNoteIndex(JianpuMeasure measure, DynamicMarking marking)
        {
            if (measure == null || marking == null)
            {
                return -1;
            }

            if (marking.NoteIndex >= 0 && marking.NoteIndex < (measure.MelodyNotes?.Count ?? 0))
            {
                return marking.NoteIndex;
            }

            return FindNoteIndexAtBeat(measure, marking.BeatPosition);
        }

        public static DynamicMarking GetMarkingForNote(JianpuMeasure measure, int noteIndex)
        {
            NormalizeMeasure(measure);
            return measure.Dynamics.FirstOrDefault(marking => ResolveNoteIndex(measure, marking) == noteIndex);
        }

        public static bool TrySetForNote(JianpuMeasure measure, int noteIndex, string text)
        {
            if (measure == null || string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (noteIndex < 0 || noteIndex >= (measure.MelodyNotes?.Count ?? 0))
            {
                return false;
            }

            NormalizeMeasure(measure);
            var beatPosition = LyricSyllableService.GetNoteBeatPosition(measure, noteIndex);
            var existingIndex = measure.Dynamics.FindIndex(marking => ResolveNoteIndex(measure, marking) == noteIndex);
            var updated = new DynamicMarking
            {
                Text = text,
                NoteIndex = noteIndex,
                BeatPosition = beatPosition
            };

            if (existingIndex >= 0)
            {
                measure.Dynamics[existingIndex] = updated;
            }
            else
            {
                measure.Dynamics.Add(updated);
            }

            TrimAndSort(measure);
            return true;
        }

        public static bool TryRemoveForNote(JianpuMeasure measure, int noteIndex)
        {
            if (measure?.Dynamics == null)
            {
                return false;
            }

            var removed = measure.Dynamics.RemoveAll(marking => ResolveNoteIndex(measure, marking) == noteIndex);
            if (removed <= 0)
            {
                return false;
            }

            TrimAndSort(measure);
            return true;
        }

        public static void OnNoteRemoved(JianpuMeasure measure, int removedNoteIndex)
        {
            if (measure?.Dynamics == null || measure.Dynamics.Count == 0)
            {
                return;
            }

            measure.Dynamics.RemoveAll(marking => marking.NoteIndex == removedNoteIndex);
            foreach (var marking in measure.Dynamics)
            {
                if (marking.NoteIndex > removedNoteIndex)
                {
                    marking.NoteIndex--;
                }
            }

            TrimAndSort(measure);
        }

        public static List<DynamicMarking> CloneMarkings(IReadOnlyList<DynamicMarking> markings)
        {
            var clone = new List<DynamicMarking>();
            if (markings == null)
            {
                return clone;
            }

            foreach (var marking in markings)
            {
                if (marking == null)
                {
                    continue;
                }

                clone.Add(new DynamicMarking
                {
                    Text = marking.Text,
                    NoteIndex = marking.NoteIndex,
                    BeatPosition = marking.BeatPosition
                });
            }

            return clone;
        }

        private static void TrimAndSort(JianpuMeasure measure)
        {
            var noteCount = measure.MelodyNotes?.Count ?? 0;
            var normalized = new List<DynamicMarking>();
            foreach (var marking in measure.Dynamics)
            {
                if (marking == null || string.IsNullOrWhiteSpace(marking.Text))
                {
                    continue;
                }

                var clone = new DynamicMarking
                {
                    Text = marking.Text,
                    NoteIndex = marking.NoteIndex,
                    BeatPosition = marking.BeatPosition
                };

                if (clone.NoteIndex >= 0 && clone.NoteIndex < noteCount)
                {
                    clone.BeatPosition = LyricSyllableService.GetNoteBeatPosition(measure, clone.NoteIndex);
                }
                else
                {
                    clone.NoteIndex = -1;
                }

                normalized.Add(clone);
            }

            measure.Dynamics.Clear();
            foreach (var marking in normalized.OrderBy(marking => marking.BeatPosition))
            {
                measure.Dynamics.Add(marking);
            }
        }

        private static int FindNoteIndexAtBeat(JianpuMeasure measure, double beatPosition)
        {
            var notes = measure?.MelodyNotes;
            if (notes == null || notes.Count == 0)
            {
                return -1;
            }

            var beat = 0.0;
            for (var i = 0; i < notes.Count; i++)
            {
                if (Math.Abs(beat - beatPosition) < 0.001)
                {
                    return i;
                }

                beat += JianpuRenderer.GetDurationUnits(notes[i]);
            }

            return -1;
        }
    }
}
