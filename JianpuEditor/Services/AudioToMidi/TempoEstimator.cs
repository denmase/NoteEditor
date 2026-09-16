using System;
using System.Collections.Generic;
using System.Linq;

namespace JianpuEditor.Services.AudioToMidi
{
    /// <summary>
    /// Estimates a tempo (BPM) from transcribed note onset times, so the temporary MIDI
    /// file audio import writes represents the song's actual rhythm instead of an
    /// arbitrary fixed tempo -- which otherwise makes every note's real-world duration
    /// land at an effectively-random position on the notation grid once re-imported.
    /// </summary>
    /// <remarks>
    /// This is an inter-onset-interval (IOI) histogram method: for every pair of onsets,
    /// the gap between them should be close to a whole number of beats at the song's
    /// real tempo. Scoring a range of candidate tempos by how many onset gaps land near
    /// a whole multiple of that tempo's beat period, and picking the best-scoring one,
    /// is a standard, well-established technique for tempo induction from a note list
    /// (the same principle "Automatic Extraction of Tempo and Beat From Expressive
    /// Performances", Dixon 2001, uses for MIDI performance data). There is no
    /// percussive/rhythm track to lean on here -- only the vocal or instrument melody
    /// itself -- so this is inherently less reliable than audio-domain beat tracking,
    /// but it's the right tool given what a STEM transcription actually provides.
    /// </remarks>
    internal static class TempoEstimator
    {
        public const double DefaultBpm = 120.0;
        private const double MinBpm = 60.0;
        private const double MaxBpm = 200.0;
        private const double MinIoiSeconds = 0.1; // faster than this is almost certainly decoder jitter, not a beat
        private const double MaxIoiSeconds = 2.0; // slower than this (a <30bpm beat) is not a useful candidate period
        // A fixed absolute tolerance, not a percentage of the beat period: a percentage-based
        // tolerance gives slow candidate tempos a wider absolute matching window for the exact
        // same onset data (8% of a slow tempo's long period is a lot more real time than 8% of
        // a fast tempo's short one), which systematically inflates slow-tempo scores regardless
        // of whether they're actually right -- in practice this was enough to make the search
        // collapse onto MinBpm on real vocal data with looser onset timing than any of this
        // class's synthetic validation used.
        private const double AbsoluteToleranceSeconds = 0.035;
        private const int MinNotesRequired = 6;
        private const double HalvingAcceptanceFraction = 0.45; // accept half tempo if it still explains at least this fraction of the full-tempo score

        /// <summary>
        /// Returns the estimated tempo in BPM, or <see cref="DefaultBpm"/> if there
        /// aren't enough notes, or none of them, to estimate one with any confidence.
        /// </summary>
        public static double EstimateBpm(IReadOnlyList<TranscribedNote> notes)
        {
            if (notes == null || notes.Count < MinNotesRequired)
            {
                return DefaultBpm;
            }

            var onsets = notes.Select(n => n.Start).OrderBy(t => t).ToList();
            var iois = CollectOnsetGaps(onsets);
            if (iois.Count == 0)
            {
                return DefaultBpm;
            }

            var coarse = SearchBestBpm(iois, MinBpm, MaxBpm, 1.0);
            var (bestBpm, bestScore) = SearchBestBpm(iois, Math.Max(MinBpm, coarse.Bpm - 3.0), Math.Min(MaxBpm, coarse.Bpm + 3.0), 0.1);

            // The highest-scoring period is very often an exact subdivision (usually a
            // half) of the tempo a listener would actually tap along to -- an inherent
            // ambiguity in inferring tempo from note timing alone, with no rhythm track
            // to anchor against. If halving still explains a healthy share of the same
            // onset gaps, prefer it; repeat while that keeps holding.
            while (bestBpm / 2.0 >= MinBpm)
            {
                var halfBpm = bestBpm / 2.0;
                var halfScore = ScoreBpm(iois, 60.0 / halfBpm);
                if (halfScore < bestScore * HalvingAcceptanceFraction)
                {
                    break;
                }

                bestBpm = halfBpm;
                bestScore = halfScore;
            }

            var matchFraction = bestScore / iois.Count;
            if (matchFraction < 0.12)
            {
                // too few onset gaps land anywhere near a consistent beat grid to trust this
                return DefaultBpm;
            }

            return Math.Round(bestBpm);
        }

        private static List<double> CollectOnsetGaps(IReadOnlyList<double> onsets)
        {
            var gaps = new List<double>();
            for (var i = 0; i < onsets.Count; i++)
            {
                for (var j = i + 1; j < onsets.Count; j++)
                {
                    var gap = onsets[j] - onsets[i];
                    if (gap > MaxIoiSeconds)
                    {
                        break; // onsets is sorted, so every later j is only further away
                    }

                    if (gap >= MinIoiSeconds)
                    {
                        gaps.Add(gap);
                    }
                }
            }

            return gaps;
        }

        private static (double Bpm, double Score) SearchBestBpm(List<double> iois, double lowBpm, double highBpm, double step)
        {
            var bestBpm = lowBpm;
            var bestScore = -1.0;
            for (var bpm = lowBpm; bpm <= highBpm + step / 2; bpm += step)
            {
                var period = 60.0 / bpm;
                var score = ScoreBpm(iois, period);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestBpm = bpm;
                }
            }

            return (bestBpm, bestScore);
        }

        private static double ScoreBpm(List<double> iois, double period)
        {
            var score = 0.0;
            foreach (var ioi in iois)
            {
                var multiple = Math.Round(ioi / period);
                if (multiple < 1)
                {
                    continue;
                }

                var residual = Math.Abs(ioi - multiple * period);
                if (residual < AbsoluteToleranceSeconds)
                {
                    score += 1.0 - (residual / AbsoluteToleranceSeconds);
                }
            }

            return score;
        }
    }
}
