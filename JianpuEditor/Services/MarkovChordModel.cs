using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JianpuEditor.Services
{
    /// <summary>
    /// Order-1 and order-2 Markov model over chord transitions, for the enhanced harmony engine.
    /// States are roman-numeral strings (e.g. "I", "V7", "vi"), trained from a plain-text corpus
    /// of chord progressions by <see cref="ChordTransitionTrainer"/>.
    /// </summary>
    internal sealed class MarkovChordModel
    {
        private readonly Dictionary<string, Dictionary<string, int>> _order1 = new Dictionary<string, Dictionary<string, int>>();
        private readonly Dictionary<string, Dictionary<string, int>> _order2 = new Dictionary<string, Dictionary<string, int>>();
        private readonly Dictionary<string, int> _unigram = new Dictionary<string, int>();

        /// <summary>Scales the log-probability contribution when folded into a chord candidate's score.</summary>
        public double Weight { get; set; } = 1.5;

        public int TotalTransitions1 { get; private set; }

        public int TotalTransitions2 { get; private set; }

        public void Train(IEnumerable<IReadOnlyList<string>> progressions)
        {
            foreach (var progression in progressions)
            {
                if (progression == null || progression.Count == 0)
                {
                    continue;
                }

                foreach (var chord in progression)
                {
                    IncrementCount(_unigram, chord);
                }

                for (var i = 0; i < progression.Count - 1; i++)
                {
                    var from = progression[i];
                    var to = progression[i + 1];
                    if (!_order1.TryGetValue(from, out var dist))
                    {
                        dist = new Dictionary<string, int>();
                        _order1[from] = dist;
                    }

                    IncrementCount(dist, to);
                    TotalTransitions1++;
                }

                for (var i = 0; i < progression.Count - 2; i++)
                {
                    var key = progression[i] + "|" + progression[i + 1];
                    var to = progression[i + 2];
                    if (!_order2.TryGetValue(key, out var dist))
                    {
                        dist = new Dictionary<string, int>();
                        _order2[key] = dist;
                    }

                    IncrementCount(dist, to);
                    TotalTransitions2++;
                }
            }
        }

        public double Probability(string candidate, string previous1)
        {
            if (previous1 != null && _order1.TryGetValue(previous1, out var dist1))
            {
                var total = dist1.Values.Sum();
                if (dist1.TryGetValue(candidate, out var count))
                {
                    return (double)count / total;
                }

                return 1.0 / (total + Math.Max(1, _unigram.Count));
            }

            var unigramTotal = _unigram.Values.Sum();
            if (unigramTotal == 0)
            {
                return 0;
            }

            _unigram.TryGetValue(candidate, out var unigramCount);
            return unigramCount / (double)unigramTotal;
        }

        public double Probability(string candidate, string previous1, string previous2)
        {
            var p1 = Probability(candidate, previous1);
            if (previous1 == null || previous2 == null)
            {
                return p1;
            }

            var key = previous2 + "|" + previous1;
            if (!_order2.TryGetValue(key, out var dist2))
            {
                return p1;
            }

            var total = dist2.Values.Sum();
            double p2;
            if (dist2.TryGetValue(candidate, out var count))
            {
                p2 = (double)count / total;
            }
            else
            {
                p2 = 1.0 / (total + Math.Max(1, _unigram.Count));
            }

            return (0.7 * p2) + (0.3 * p1);
        }

        public double Score(string candidate, string previous1, string previous2 = null)
        {
            var probability = Probability(candidate, previous1, previous2);
            if (probability <= 0)
            {
                return -10.0;
            }

            return Math.Log(probability) * Weight;
        }

        public void Save(string path)
        {
            using (var writer = new StreamWriter(path))
            {
                foreach (var fromEntry in _order1)
                {
                    foreach (var toEntry in fromEntry.Value)
                    {
                        writer.WriteLine("1|" + fromEntry.Key + "|" + toEntry.Key + "|" + toEntry.Value);
                    }
                }

                foreach (var keyEntry in _order2)
                {
                    var parts = keyEntry.Key.Split('|');
                    if (parts.Length != 2)
                    {
                        continue;
                    }

                    foreach (var toEntry in keyEntry.Value)
                    {
                        writer.WriteLine("2|" + parts[0] + "|" + parts[1] + "|" + toEntry.Key + "|" + toEntry.Value);
                    }
                }
            }
        }

        public static MarkovChordModel Load(string path)
        {
            var model = new MarkovChordModel();
            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine.TrimStart('﻿');
                var parts = line.Split('|');
                if (parts.Length < 4)
                {
                    continue;
                }

                if (parts[0] == "1" && parts.Length == 4)
                {
                    if (!int.TryParse(parts[3], out var count))
                    {
                        continue;
                    }

                    if (!model._order1.TryGetValue(parts[1], out var dist))
                    {
                        dist = new Dictionary<string, int>();
                        model._order1[parts[1]] = dist;
                    }

                    dist[parts[2]] = count;
                    model.TotalTransitions1 += count;
                }
                else if (parts[0] == "2" && parts.Length == 5)
                {
                    if (!int.TryParse(parts[4], out var count))
                    {
                        continue;
                    }

                    var key = parts[1] + "|" + parts[2];
                    if (!model._order2.TryGetValue(key, out var dist))
                    {
                        dist = new Dictionary<string, int>();
                        model._order2[key] = dist;
                    }

                    dist[parts[3]] = count;
                    model.TotalTransitions2 += count;
                }
            }

            return model;
        }

        private static void IncrementCount(Dictionary<string, int> counts, string key)
        {
            counts.TryGetValue(key, out var current);
            counts[key] = current + 1;
        }
    }
}
