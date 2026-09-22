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
