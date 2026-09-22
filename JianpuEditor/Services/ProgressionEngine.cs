using System.Collections.Generic;
using System.Linq;
using JianpuEditor.Models;

namespace JianpuEditor.Services
{
    /// <summary>
    /// Enhanced multi-measure progression engine for the "Markov" harmony backend: matches melody
    /// profiles against a small template library, then scores each template with voice leading,
    /// a cadence bonus, and (optionally) a Markov transition model.
    /// </summary>
    internal sealed class ProgressionEngine
    {
        private readonly MarkovChordModel _markov;
        private readonly HarmonyEngine _harmony;

        public ProgressionEngine(MarkovChordModel markov = null)
        {
            _markov = markov;
            _harmony = new HarmonyEngine(markov);
        }

        private static readonly Dictionary<int, List<RomanNumeral[]>> Templates = new Dictionary<int, List<RomanNumeral[]>>
        {
            [1] = new List<RomanNumeral[]>
            {
                new[] { RN(1) },
                new[] { RN(4) },
                new[] { RN(5) },
                new[] { RN(6, ChordQuality.Minor) }
            },
            [2] = new List<RomanNumeral[]>
            {
                new[] { RN(1), RN(5) },
                new[] { RN(1), RN(4) },
                new[] { RN(4), RN(5) },
                new[] { RN(6, ChordQuality.Minor), RN(5) },
                new[] { RN(1, ChordQuality.Major7), RN(4, ChordQuality.Major7) },
                new[] { RN(2, ChordQuality.Minor7), RN(5, ChordQuality.Dominant7) },
                new[] { RN(1), RN(6, ChordQuality.Minor) }
            },
            [3] = new List<RomanNumeral[]>
            {
                new[] { RN(1), RN(4), RN(5) },
                new[] { RN(1), RN(6, ChordQuality.Minor), RN(4) },
                new[] { RN(2, ChordQuality.Minor7), RN(5, ChordQuality.Dominant7), RN(1) },
                new[] { RN(1), RN(4, ChordQuality.Major7), RN(5, ChordQuality.Dominant7) }
            },
            [4] = new List<RomanNumeral[]>
            {
                new[] { RN(1), RN(4), RN(5), RN(1) },
                new[] { RN(1), RN(6, ChordQuality.Minor), RN(4), RN(5) },
                new[]
                {
                    RN(2, ChordQuality.Minor7), RN(5, ChordQuality.Dominant7),
                    RN(1, ChordQuality.Major7), RN(6, ChordQuality.Minor7)
                },
                new[]
                {
                    RN(1), RN(6, ChordQuality.Minor),
                    RN(2, ChordQuality.Minor7), RN(5, ChordQuality.Dominant7)
                }
            }
        };

        private static RomanNumeral RN(int degree, ChordQuality quality = ChordQuality.Major)
        {
            return new RomanNumeral { Degree = degree, Quality = quality };
        }

        /// <summary>
        /// Extends a base template to cover a longer range (e.g. a whole song, well beyond the
        /// 4-measure templates above) by padding it with an alternating IV/V filler and forcing a
        /// closing V-I cadence -- the same "repeat and cadence" shape
        /// <see cref="HarmonyProgressionService"/>'s own ExpandTemplate already uses for the
        /// Legacy engine at this length, so both backends behave predictably past 4 measures. A
        /// no-op (aside from truncating) when the template is already long enough.
        /// </summary>
        private static RomanNumeral[] ExtendTemplate(RomanNumeral[] template, int targetLength)
        {
            if (template.Length >= targetLength)
            {
                return template.Take(targetLength).ToArray();
            }

            var extended = new List<RomanNumeral>(template);
            while (extended.Count < targetLength)
            {
                if (extended.Count == targetLength - 1)
                {
                    extended.Add(RN(1));
                }
                else if (extended.Count == targetLength - 2)
                {
                    extended.Add(RN(5));
                    extended.Add(RN(1));
                }
                else
                {
                    extended.Add(extended.Count % 2 == 0 ? RN(4) : RN(5));
                }
            }

            return extended.ToArray();
        }

        /// <summary>Result wrapper carrying both the candidate progression and its score.</summary>
        public sealed class RankedProgression
        {
            public List<RomanNumeral> Chords { get; set; } = new List<RomanNumeral>();

            public double Score { get; set; }

            public List<string> Reasons { get; set; } = new List<string>();
        }

        public List<RankedProgression> SuggestProgression(
            IReadOnlyList<IReadOnlyList<HarmonyMelodyNote>> measuresPerBar,
            int keyRootMidi,
            int maxResults = 3)
        {
            var count = measuresPerBar.Count;
            if (count == 0)
            {
                return new List<RankedProgression>();
            }

            List<RomanNumeral[]> templates;
            if (!Templates.TryGetValue(count, out templates))
            {
                var best = Templates.Keys.Where(k => k <= count).OrderByDescending(k => k).FirstOrDefault();
                if (best == 0)
                {
                    return new List<RankedProgression>();
                }

                templates = Templates[best].Select(t => ExtendTemplate(t, count)).ToList();
            }

            var profiles = measuresPerBar.Select(MeasureProfile.FromNotes).ToList();
            var results = new List<RankedProgression>();

            foreach (var template in templates)
            {
                double score = 0;
                var reasons = new List<string>();

                for (var i = 0; i < template.Length && i < profiles.Count; i++)
                {
                    score += ScoreMeasure(profiles[i], template[i]);
                }

                for (var i = 0; i < template.Length - 1; i++)
                {
                    score += VoiceLeadingScorer.Score(template[i], template[i + 1], keyRootMidi);
                }

                var last = template[template.Length - 1];
                if (last.Degree == 1 && last.Quality == ChordQuality.Major)
                {
                    score += 2.0;
                    reasons.Add("tonic cadence");
                }

                if (last.Quality == ChordQuality.Dominant7)
                {
                    score += 1.0;
                    reasons.Add("half cadence (V7)");
                }

                if (_markov != null)
                {
                    var markovScore = 0.0;
                    string previous1 = null;
                    string previous2 = null;

                    for (var i = 0; i < template.Length; i++)
                    {
                        var chord = template[i].ToString();
                        markovScore += _markov.Score(chord, previous1, previous2);
                        previous2 = previous1;
                        previous1 = chord;
                    }

                    score += markovScore;
                    reasons.Add("common progression pattern (Markov)");
                }

                results.Add(new RankedProgression
                {
                    Chords = template.ToList(),
                    Score = score,
                    Reasons = reasons
                });
            }

            return results.OrderByDescending(r => r.Score).Take(maxResults).ToList();
        }

        public string RomanNumeralToChordSymbol(RomanNumeral roman, int keyRootMidi)
        {
            return _harmony.RomanNumeralToChordSymbol(roman, keyRootMidi);
        }

        private static double ScoreMeasure(MeasureProfile profile, RomanNumeral roman)
        {
            var score = 0.0;
            if (roman.Degree == profile.BassDegree)
            {
                score += 3.0;
            }

            if (ContainsDegree(roman, profile.PrimaryDegree))
            {
                score += 2.0;
            }

            if (ContainsDegree(roman, profile.EndDegree))
            {
                score += 1.0;
            }

            if (ContainsDegree(roman, profile.BassDegree))
            {
                score += 1.5;
            }

            foreach (var entry in profile.DegreeHistogram)
            {
                if (ContainsDegree(roman, entry.Key))
                {
                    score += entry.Value * 0.5;
                }
            }

            return score;
        }

        private static bool ContainsDegree(RomanNumeral roman, int degree)
        {
            var intervals = roman.Quality.GetIntervals();
            var rootSemitone = ChordVocabulary.MajorScaleSemitones[(roman.Degree - 1) % 7] + roman.Accidental;

            foreach (var interval in intervals)
            {
                var noteSemitone = ((rootSemitone + interval) % 12 + 12) % 12;
                for (var d = 0; d < 7; d++)
                {
                    if (ChordVocabulary.MajorScaleSemitones[d] == noteSemitone && (d + 1) == degree)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Compact summary of a measure's melody, used to match it against a template.</summary>
        private sealed class MeasureProfile
        {
            public int PrimaryDegree { get; set; }

            public int EndDegree { get; set; }

            public int BassDegree { get; set; }

            public Dictionary<int, double> DegreeHistogram { get; set; } = new Dictionary<int, double>();

            public static MeasureProfile FromNotes(IReadOnlyList<HarmonyMelodyNote> notes)
            {
                if (notes == null || notes.Count == 0)
                {
                    return new MeasureProfile();
                }

                var histogram = new Dictionary<int, double>();
                var bass = notes[0].Degree;

                foreach (var note in notes)
                {
                    histogram.TryGetValue(note.Degree, out var existing);
                    histogram[note.Degree] = existing + note.Duration;
                    if (note.Degree < bass)
                    {
                        bass = note.Degree;
                    }
                }

                var max = histogram.Values.Max();
                var keys = new List<int>(histogram.Keys);
                foreach (var key in keys)
                {
                    histogram[key] /= max;
                }

                return new MeasureProfile
                {
                    PrimaryDegree = notes[0].Degree,
                    EndDegree = notes[notes.Count - 1].Degree,
                    BassDegree = bass,
                    DegreeHistogram = histogram
                };
            }
        }
    }
}
