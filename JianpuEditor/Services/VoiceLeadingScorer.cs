using System;
using JianpuEditor.Models;

namespace JianpuEditor.Services
{
    /// <summary>
    /// Heuristic voice-leading score between two chords for the enhanced (Markov) harmony engine:
    /// rewards small root movement, descending motion, and dominant-to-tonic resolutions.
    /// </summary>
    internal static class VoiceLeadingScorer
    {
        public static double Score(RomanNumeral from, RomanNumeral to, int keyRootMidi)
        {
            if (from == null || to == null)
            {
                return 0;
            }

            var fromRoot = GetRootMidi(from, keyRootMidi);
            var toRoot = GetRootMidi(to, keyRootMidi);

            var diff = toRoot - fromRoot;
            var absDiff = Math.Abs(diff);
            var normalized = absDiff > 6 ? 12 - absDiff : absDiff;

            double score;
            switch (normalized)
            {
                case 0:
                    score = 2.0;
                    break;
                case 1:
                    score = 1.5;
                    break;
                case 2:
                    score = 2.5;
                    break;
                case 3:
                    score = 1.5;
                    break;
                case 4:
                    score = 1.0;
                    break;
                case 5:
                    score = 2.0;
                    break;
                case 6:
                    score = 0.5;
                    break;
                default:
                    score = 0.5;
                    break;
            }

            if (diff < 0)
            {
                score += 0.3;
            }

            if (from.Quality == ChordQuality.Dominant7 && to.Degree == 1)
            {
                score += 2.0;
            }

            if (normalized == 5)
            {
                score += 0.5;
            }

            return score;
        }

        private static int GetRootMidi(RomanNumeral roman, int keyRootMidi)
        {
            var degreeIndex = (roman.Degree - 1) % 7;
            var semitone = ChordVocabulary.MajorScaleSemitones[degreeIndex] + roman.Accidental;

            if (roman.SecondaryOf != null)
            {
                var target = GetRootMidi(roman.SecondaryOf, keyRootMidi);
                semitone = ((target - keyRootMidi) + 7) % 12;
            }

            return keyRootMidi + semitone;
        }
    }
}
