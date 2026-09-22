using System;
using System.Collections.Generic;
using JianpuEditor.Models;
using JianpuEditor.Rendering;

namespace JianpuEditor.Services
{
    /// <summary>
    /// Converts a measure's <see cref="JianpuNote"/> sequence into <see cref="HarmonyMelodyNote"/>
    /// for the enhanced (Markov) harmony engine.
    /// </summary>
    /// <remarks>
    /// A continuation-dot rest (<see cref="NoteType.Rest"/> with <see cref="JianpuNote.IsContinuation"/>
    /// set) is not silence -- it extends the duration of whatever note is currently sounding, the
    /// same convention <see cref="ScoreMidiSchedule"/> uses for playback. It is folded into the
    /// previous entry's <see cref="HarmonyMelodyNote.Duration"/> here rather than being dropped
    /// (which would silently shrink that note's weight in <see cref="HarmonyEngine"/>'s melody-fit
    /// scoring) or treated as a new, pitchless note.
    /// </remarks>
    internal static class JianpuNoteAdapter
    {
        public static List<HarmonyMelodyNote> ToHarmonyMelodyNotes(IEnumerable<JianpuNote> notes)
        {
            var result = new List<HarmonyMelodyNote>();
            if (notes == null)
            {
                return result;
            }

            foreach (var note in notes)
            {
                if (note == null)
                {
                    continue;
                }

                var duration = JianpuRenderer.GetDurationUnits(note);

                if (note.Type == NoteType.Rest)
                {
                    if (note.IsContinuation && result.Count > 0)
                    {
                        result[result.Count - 1].Duration += duration;
                    }

                    continue;
                }

                if (!JianpuPitchCodec.IsValidMelodyPitch(note))
                {
                    continue;
                }

                var degree = GetDiatonicDegree(note);
                if (degree < 1 || degree > 7)
                {
                    continue;
                }

                result.Add(new HarmonyMelodyNote { Degree = degree, Duration = duration });
            }

            return result;
        }

        /// <summary>Mirrors <see cref="HarmonySuggestionService"/>'s own degree extraction so both
        /// harmony backends agree on which scale degree a chromatic (.5) pitch belongs to.</summary>
        private static int GetDiatonicDegree(JianpuNote note)
        {
            if (note.Accidental == AccidentalKind.Sharp)
            {
                return (int)Math.Floor(note.Pitch + 0.001);
            }

            if (note.Accidental == AccidentalKind.Flat)
            {
                return (int)Math.Ceiling(note.Pitch - 0.001);
            }

            return (int)Math.Round(note.Pitch);
        }
    }
}
