using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Models;

namespace JianpuEditor.Services
{
    /// <summary>
    /// "Enhanced" harmony backend: converts the Jianpu-specific model into
    /// <see cref="HarmonyMelodyNote"/>, runs <see cref="HarmonyEngine"/>/<see cref="ProgressionEngine"/>
    /// (melody-fit weighting, voice leading, a cadence bonus, and optional Markov transition
    /// scoring), then reshapes the result back into the shared DTOs the UI already knows.
    /// </summary>
    /// <remarks>
    /// <see cref="HarmonySuggestion"/>/<see cref="HarmonyProgressionSuggestion"/> are sealed and
    /// have no numeric Score property, so the candidate's score is folded into Reason instead.
    /// </remarks>
    internal sealed class MarkovHarmonySuggestionService : IHarmonySuggestionService
    {
        private readonly HarmonyEngine _harmony;
        private readonly ProgressionEngine _progression;

        public MarkovHarmonySuggestionService(MarkovChordModel markov = null)
        {
            _harmony = new HarmonyEngine(markov);
            _progression = new ProgressionEngine(markov);
        }

        public IReadOnlyList<HarmonySuggestion> SuggestForMeasure(JianpuMeasure measure, string keySignature, double beatPosition)
        {
            if (measure == null)
            {
                return Array.Empty<HarmonySuggestion>();
            }

            if (!KeySignatureService.TryParseTonicPitchClass(keySignature, out var tonicPitchClass))
            {
                return Array.Empty<HarmonySuggestion>();
            }

            var notes = JianpuNoteAdapter.ToHarmonyMelodyNotes(measure.MelodyNotes);
            if (notes.Count == 0)
            {
                return Array.Empty<HarmonySuggestion>();
            }

            var candidates = _harmony.SuggestForMeasure(notes, tonicPitchClass, maxResults: 3);
            return candidates.Select(candidate => new HarmonySuggestion
            {
                RomanNumeral = candidate.Roman.ToString(),
                ChordSymbol = candidate.ChordSymbol,
                Reason = FormatReason(candidate.Score, candidate.Reasons)
            }).ToList();
        }

        public IReadOnlyList<HarmonyProgressionSuggestion> SuggestForMeasureRange(IReadOnlyList<JianpuMeasure> measures, string keySignature)
        {
            if (measures == null || measures.Count == 0)
            {
                return Array.Empty<HarmonyProgressionSuggestion>();
            }

            if (!KeySignatureService.TryParseTonicPitchClass(keySignature, out var tonicPitchClass))
            {
                return Array.Empty<HarmonyProgressionSuggestion>();
            }

            var measuresPerBar = measures
                .Select(measure => (IReadOnlyList<HarmonyMelodyNote>)JianpuNoteAdapter.ToHarmonyMelodyNotes(measure.MelodyNotes))
                .ToList();

            var ranked = _progression.SuggestProgression(measuresPerBar, tonicPitchClass, maxResults: 3);

            return ranked.Select(progression =>
            {
                var steps = progression.Chords.Select((roman, index) => new HarmonyMeasureChordSuggestion
                {
                    MeasureOffset = index,
                    BeatPosition = 0,
                    RomanNumeral = roman.ToString(),
                    ChordSymbol = _progression.RomanNumeralToChordSymbol(roman, tonicPitchClass)
                }).ToList();

                var bassDegrees = progression.Chords.Select(roman => roman.Degree);

                return new HarmonyProgressionSuggestion
                {
                    Label = string.Join(" - ", progression.Chords.Select(roman => roman.ToString())),
                    BassLineSummary = "Bass movement " + string.Join("→", bassDegrees),
                    HarmonicSummary = "Enhanced (Markov) progression",
                    Reason = FormatReason(progression.Score, progression.Reasons),
                    Steps = steps
                };
            }).ToList();
        }

        private static string FormatReason(double score, IEnumerable<string> reasons)
        {
            return string.Format(CultureInfo.InvariantCulture, "score {0:0.00}: {1}", score, string.Join(", ", reasons));
        }
    }
}
