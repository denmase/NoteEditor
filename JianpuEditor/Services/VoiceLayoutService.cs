using System;
using System.Collections.Generic;
using JianpuEditor.Models;

namespace JianpuEditor.Services
{
    /// <summary>Resolves a measure's voices into one render/playback order: every "above" voice
    /// (descant/solo) first in list order, then the measure's primary <see
    /// cref="JianpuMeasure.MelodyNotes"/>, then the rest of <see cref="JianpuMeasure.ExtraVoices"/>
    /// in list order. This is the single place that order is decided -- rendering, hit-testing,
    /// and playback scheduling all go through it instead of each re-deriving voice order
    /// themselves, matching what a real 5-voice (descant + SATB) prototype render validated needs
    /// no further special-casing per voice.</summary>
    public static class VoiceLayoutService
    {
        public static IReadOnlyList<ResolvedVoice> GetRenderOrder(JianpuMeasure measure)
        {
            var result = new List<ResolvedVoice>();
            var extraVoices = measure?.ExtraVoices;
            if (extraVoices != null)
            {
                for (var i = 0; i < extraVoices.Count; i++)
                {
                    if (extraVoices[i]?.IsAbove == true)
                    {
                        result.Add(new ResolvedVoice(extraVoices[i].Role, extraVoices[i].Notes, i));
                    }
                }
            }

            result.Add(new ResolvedVoice("Melody", measure?.MelodyNotes, PrimaryVoiceIndex));

            if (extraVoices != null)
            {
                for (var i = 0; i < extraVoices.Count; i++)
                {
                    if (extraVoices[i]?.IsAbove != true)
                    {
                        result.Add(new ResolvedVoice(extraVoices[i].Role, extraVoices[i].Notes, i));
                    }
                }
            }

            return result;
        }

        /// <summary>True once a measure has any content beyond its primary voice -- the single
        /// trigger the layout fixes (shared measure width, grid-precise dash placement) gate on,
        /// so a measure with no extra voices renders through the exact, unchanged single-voice
        /// code path.</summary>
        public static bool HasMultipleVoices(JianpuMeasure measure)
        {
            return measure?.ExtraVoices != null && measure.ExtraVoices.Count > 0;
        }

        /// <summary>The mutable note list a <paramref name="voiceIndex"/> refers to -- <see
        /// cref="JianpuMeasure.MelodyNotes"/> for <see cref="PrimaryVoiceIndex"/>, otherwise
        /// <see cref="JianpuMeasure.ExtraVoices"/>[voiceIndex].Notes. The single place note-editing
        /// (insert/delete/mutate-in-place) resolves "which voice" against, so every operation works
        /// the same way regardless of which voice is selected. Never null and never throws for an
        /// out-of-range index -- callers that received the index from a real hit/selection won't
        /// hit that path, but editing code should still not crash on stale selection state (e.g.
        /// after a voice was removed by an undo).</summary>
        public static List<JianpuNote> GetNotesList(JianpuMeasure measure, int voiceIndex)
        {
            if (measure == null)
            {
                return new List<JianpuNote>();
            }

            if (voiceIndex == PrimaryVoiceIndex)
            {
                measure.MelodyNotes ??= new List<JianpuNote>();
                return measure.MelodyNotes;
            }

            var extraVoices = measure.ExtraVoices;
            if (extraVoices != null && voiceIndex >= 0 && voiceIndex < extraVoices.Count && extraVoices[voiceIndex] != null)
            {
                var voice = extraVoices[voiceIndex];
                voice.Notes ??= new List<JianpuNote>();
                return voice.Notes;
            }

            return new List<JianpuNote>();
        }

        /// <summary>Inserts a note into the voice <paramref name="voiceIndex"/> refers to. The
        /// primary voice goes through <see cref="MelodyChordService.InsertSlot"/> (keeps its
        /// chord-slot shadow list in sync); an extra voice has no such shadow list (it doesn't
        /// support simultaneous notes/chords yet -- see ROADMAP.md), so it's a plain list insert.</summary>
        public static void InsertNote(JianpuMeasure measure, int voiceIndex, int insertIndex, JianpuNote note)
        {
            if (measure == null || note == null)
            {
                return;
            }

            if (voiceIndex == PrimaryVoiceIndex)
            {
                MelodyChordService.InsertSlot(measure, insertIndex, note);
                return;
            }

            var notes = GetNotesList(measure, voiceIndex);
            insertIndex = Math.Max(0, Math.Min(insertIndex, notes.Count));
            notes.Insert(insertIndex, CloneNote(note));
        }

        /// <summary>Removes a note from the voice <paramref name="voiceIndex"/> refers to. Mirrors
        /// <see cref="InsertNote"/>'s primary/extra-voice split.</summary>
        public static void RemoveNote(JianpuMeasure measure, int voiceIndex, int removeIndex)
        {
            if (measure == null)
            {
                return;
            }

            if (voiceIndex == PrimaryVoiceIndex)
            {
                MelodyChordService.RemoveSlot(measure, removeIndex);
                return;
            }

            var notes = GetNotesList(measure, voiceIndex);
            if (removeIndex >= 0 && removeIndex < notes.Count)
            {
                notes.RemoveAt(removeIndex);
            }
        }

        private static JianpuNote CloneNote(JianpuNote source)
        {
            return new JianpuNote
            {
                Type = source.Type,
                Pitch = source.Pitch,
                Accidental = source.Accidental,
                Octave = source.Octave,
                Underlines = source.Underlines,
                Dashes = source.Dashes,
                Dotted = source.Dotted,
                IsContinuation = source.IsContinuation
            };
        }

        /// <summary><see cref="ResolvedVoice.ExtraVoiceIndex"/> for the measure's primary voice
        /// (<see cref="JianpuMeasure.MelodyNotes"/>), distinguishing it from an index into <see
        /// cref="JianpuMeasure.ExtraVoices"/>.</summary>
        public const int PrimaryVoiceIndex = -1;

        public readonly struct ResolvedVoice
        {
            public ResolvedVoice(string role, IReadOnlyList<JianpuNote> notes, int extraVoiceIndex)
            {
                Role = role ?? string.Empty;
                Notes = notes ?? new List<JianpuNote>();
                ExtraVoiceIndex = extraVoiceIndex;
            }

            public string Role { get; }

            public IReadOnlyList<JianpuNote> Notes { get; }

            /// <summary><see cref="PrimaryVoiceIndex"/> for the primary voice, otherwise the
            /// index into <see cref="JianpuMeasure.ExtraVoices"/>.</summary>
            public int ExtraVoiceIndex { get; }

            public bool IsPrimary => ExtraVoiceIndex == PrimaryVoiceIndex;
        }
    }
}
