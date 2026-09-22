using System;
using System.Collections.Generic;
using System.IO;

namespace JianpuEditor.Services
{
    /// <summary>Builds a <see cref="MarkovChordModel"/> from a plain-text corpus of chord progressions.</summary>
    internal static class ChordTransitionTrainer
    {
        private static readonly char[] ChordSeparators = { ' ' };

        public static MarkovChordModel TrainFromFile(string path)
        {
            return TrainFromLines(File.ReadLines(path));
        }

        public static MarkovChordModel TrainFromLines(IEnumerable<string> lines)
        {
            var progressions = new List<IReadOnlyList<string>>();

            foreach (var rawLine in lines)
            {
                var trimmed = rawLine.TrimStart('﻿').Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var chords = trimmed.Split(ChordSeparators, StringSplitOptions.RemoveEmptyEntries);
                if (chords.Length >= 2)
                {
                    progressions.Add(chords);
                }
            }

            var model = new MarkovChordModel();
            model.Train(progressions);
            return model;
        }
    }
}
