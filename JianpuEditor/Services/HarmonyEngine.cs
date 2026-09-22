using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using JianpuEditor.Models;

namespace JianpuEditor.Services
{
    /// <summary>
    /// Enhanced per-measure chord suggestion engine: combines melody fit, a cadence bonus, and
    /// (optionally) Markov transition probabilities. A second, opt-in backend alongside the
    /// simpler, rule-only <see cref="HarmonySuggestionService"/> ("Legacy") -- see
    /// <see cref="SelectableHarmonySuggestionService"/>.
    /// </summary>
    internal sealed class HarmonyEngine
    {
        public const double DefaultAdventurousness = 0.3;

        /// <summary>Shift applied to the (negative) Markov log-probability so an unfamiliar chord
        /// doesn't automatically drown out an otherwise strong melody-fit score.</summary>
        private const double MarkovScoreOffset = 3.0;

        /// <summary>Adventurousness required before borrowed chords appear.</summary>
        private const double BorrowedChordThreshold = 0.2;

        /// <summary>Adventurousness required before secondary dominants appear.</summary>
        private const double SecondaryDominantThreshold = 0.4;

        private readonly MarkovChordModel _markov;
        private readonly ConcurrentDictionary<string, string> _symbolCache = new ConcurrentDictionary<string, string>();

        public HarmonyEngine(MarkovChordModel markov = null)
        {
            _markov = markov;
        }

        public List<ChordCandidate> SuggestForMeasure(
            IReadOnlyList<HarmonyMelodyNote> notes,
            int keyRootMidi,
            double adventurousness = DefaultAdventurousness,
            int maxResults = 5)
        {
            if (notes == null || notes.Count == 0)
            {
                return new List<ChordCandidate>();
            }

            var degreeWeights = CollectWeightedDegrees(notes);
            var candidates = new Dictionary<string, ChordCandidate>();

            foreach (var entry in degreeWeights)
            {
                if (!ChordVocabulary.DegreeToCandidates.TryGetValue(entry.Key, out var romans))
                {
                    continue;
                }

                foreach (var roman in romans)
                {
                    AddOrUpdate(candidates, roman, entry.Value, "melody fit");
                }
            }

            if (adventurousness > BorrowedChordThreshold)
            {
                foreach (var roman in ChordVocabulary.BorrowedChords)
                {
                    AddOrUpdate(candidates, roman, 0.5 * adventurousness, "borrowed chord");
                }
            }

            if (adventurousness > SecondaryDominantThreshold)
            {
                foreach (var roman in ChordVocabulary.SecondaryDominants)
                {
                    AddOrUpdate(candidates, roman, 0.7 * adventurousness, "secondary dominant");
                }
            }

            var lastDegree = notes[notes.Count - 1].Degree;
            var lastIsChordToneOfV7 = lastDegree == 2 || lastDegree == 4 || lastDegree == 5 || lastDegree == 7;

            foreach (var candidate in candidates.Values)
            {
                if (lastIsChordToneOfV7
                    && candidate.Roman.Degree == 5
                    && candidate.Roman.Quality == ChordQuality.Dominant7)
                {
                    candidate.Score += 2.0;
                    candidate.Reasons.Add("approaches cadence (V7)");
                }

                if (_markov != null)
                {
                    var markovScore = _markov.Score(candidate.Roman.ToString(), null, null);
                    candidate.Score += markovScore + MarkovScoreOffset;
                    candidate.Reasons.Add("common in corpus (Markov)");
                }

                candidate.ChordSymbol = RomanNumeralToChordSymbol(candidate.Roman, keyRootMidi);
            }

            return candidates.Values
                .OrderByDescending(candidate => candidate.Score)
                .Take(maxResults)
                .ToList();
        }

        /// <summary>
        /// Weighted histogram of degrees, normalized to [0,1]. Downbeats, the final note, and
        /// longer notes carry more weight.
        /// </summary>
        private static Dictionary<int, double> CollectWeightedDegrees(IReadOnlyList<HarmonyMelodyNote> notes)
        {
            var result = new Dictionary<int, double>();

            for (var i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                var weight = 1.0;

                if (i == 0)
                {
                    weight += 1.0;
                }

                if (i == notes.Count - 1)
                {
                    weight += 1.5;
                }

                weight += Math.Min(note.Duration, 4) * 0.25;

                result.TryGetValue(note.Degree, out var existing);
                result[note.Degree] = existing + weight;
            }

            var max = result.Values.Max();
            var keys = new List<int>(result.Keys);
            foreach (var key in keys)
            {
                result[key] /= max;
            }

            return result;
        }

        private static void AddOrUpdate(Dictionary<string, ChordCandidate> candidates, RomanNumeral roman, double score, string reason)
        {
            var key = roman.ToString();
            if (candidates.TryGetValue(key, out var existing))
            {
                existing.Score += score;
                existing.Reasons.Add(reason);
                return;
            }

            candidates[key] = new ChordCandidate
            {
                Roman = roman,
                Score = score,
                Reasons = new List<string> { reason }
            };
        }

        /// <summary>Converts a roman numeral into a concrete chord symbol (e.g. "V7/ii" in C -> "A7").</summary>
        public string RomanNumeralToChordSymbol(RomanNumeral roman, int keyRootMidi)
        {
            var cacheKey = roman + "|" + keyRootMidi;
            return _symbolCache.GetOrAdd(cacheKey, _ => ComputeChordSymbol(roman, keyRootMidi));
        }

        private static string ComputeChordSymbol(RomanNumeral roman, int keyRootMidi)
        {
            var semitone = ChordVocabulary.MajorScaleSemitones[(roman.Degree - 1) % 7] + roman.Accidental;

            if (roman.SecondaryOf != null)
            {
                var targetSemitone = ChordVocabulary.MajorScaleSemitones[(roman.SecondaryOf.Degree - 1) % 7] + roman.SecondaryOf.Accidental;
                semitone = (targetSemitone + 7) % 12;
            }

            var root = ((keyRootMidi + semitone) % 12 + 12) % 12;
            return ChordVocabulary.NoteNames[root] + roman.Quality.GetSuffix();
        }
    }
}
