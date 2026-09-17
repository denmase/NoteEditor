using System;
using System.Collections.Generic;
using System.Linq;

namespace JianpuEditor.Services.AudioToMidi
{
    /// <summary>
    /// Estimates a time signature (numerator only -- the denominator is always reported
    /// as 4, see remarks) from transcribed note onsets and an already-estimated tempo,
    /// so the temporary MIDI file audio import writes carries a real time signature
    /// instead of always defaulting to 4/4.
    /// </summary>
    /// <remarks>
    /// This is a simplified, beat-grid-constrained relative of Inner Metric Analysis
    /// (de Haas &amp; Volk, "Meter Detection in Symbolic Music Using Inner Metric
    /// Analysis", ISMIR 2016): IMA scores every possible periodic grid of onsets by
    /// repeat-count and sums overlapping grids into a metric weight profile, with high
    /// weight marking likely strong beats. The same literature (see the ISMIR 2014
    /// "Estimating Musical Time Information from Performed MIDI Files") also documents
    /// that a note's *length*, not just how often something starts there, is a real
    /// indicator of metric strength -- downbeats tend to carry the longer, held notes.
    ///
    /// Since the tempo is already known here (<see cref="TempoEstimator"/> runs first),
    /// this doesn't need IMA's full search over arbitrary periods: it only needs to
    /// decide how many beats group into one measure. Each note's onset is snapped to
    /// the nearest beat on the known tempo's grid (onsets that land nowhere near a beat
    /// are syncopation/noise and excluded from this specific signal). For each
    /// candidate grouping (2, 3, or 4 beats per measure), every beat position's
    /// "salience" -- onset count plus total held duration in beats -- is accumulated by
    /// its phase within the hypothesized measure, and the candidate whose best phase
    /// stands out most from the others (highest peak-to-mean ratio) wins.
    ///
    /// Duple (2) vs quadruple (4) is a well-known hard case in this literature: a true
    /// 4/4 piece often also carries a real secondary accent on beat 3, so grouping by 2
    /// pools two genuinely strong positions (beats 1 and 3) together and can look
    /// deceptively strong. 4 is preferred unless 2 wins by a clear margin, matching both
    /// the underlying ambiguity and standard notation convention (most contemporary
    /// pop/rock in a duple feel is still notated 4/4, not 2/4). Compound meters (6/8 and
    /// similar) need beat-subdivision analysis this doesn't attempt and are out of scope
    /// -- they fall back to the 4/4 default along with any other low-confidence case.
    /// </remarks>
    internal static class TimeSignatureEstimator
    {
        public const int DefaultNumerator = 4;
        private const int MinNotesRequired = 12; // need several full candidate measures' worth to say anything
        private const double BeatSnapToleranceFraction = 0.18; // of one beat period
        private const double TwoOverFourPreferenceMargin = 1.15; // 2 must beat 4's score by this factor to win
        private const double MinPeakinessToTrust = 1.25; // best phase must stand out this much over the mean

        private static readonly int[] Candidates = { 2, 3, 4 };

        /// <summary>Returns the estimated (numerator, denominator) pair, or (4, 4) if
        /// there aren't enough notes, or no candidate grouping stands out with any
        /// confidence.</summary>
        public static (int Numerator, int Denominator) EstimateTimeSignature(IReadOnlyList<TranscribedNote> notes, double bpm)
        {
            if (notes == null || notes.Count < MinNotesRequired || bpm <= 0)
            {
                return (DefaultNumerator, 4);
            }

            var beatPeriod = 60.0 / bpm;
            var beatIndices = SnapToBeatGrid(notes, beatPeriod);
            if (beatIndices.Count < MinNotesRequired)
            {
                return (DefaultNumerator, 4);
            }

            var scores = new Dictionary<int, double>();
            foreach (var candidate in Candidates)
            {
                scores[candidate] = ScoreGrouping(beatIndices, candidate);
            }

            if (scores[2] < scores[4] * TwoOverFourPreferenceMargin)
            {
                scores[2] = -1.0;
            }

            var best = Candidates.OrderByDescending(c => scores[c]).First();
            if (scores[best] < MinPeakinessToTrust)
            {
                return (DefaultNumerator, 4);
            }

            return (best, 4);
        }

        private static List<(int BeatIndex, double DurationBeats)> SnapToBeatGrid(
            IReadOnlyList<TranscribedNote> notes, double beatPeriod)
        {
            var tolerance = beatPeriod * BeatSnapToleranceFraction;
            var result = new List<(int, double)>();
            foreach (var note in notes)
            {
                var beatPosition = note.Start / beatPeriod;
                var nearestBeat = (int)Math.Round(beatPosition);
                var residualSeconds = Math.Abs(beatPosition - nearestBeat) * beatPeriod;
                if (residualSeconds > tolerance)
                {
                    continue; // syncopated/off-grid onset -- not useful for finding the downbeat phase
                }

                var durationBeats = Math.Max(0, (note.End - note.Start) / beatPeriod);
                result.Add((nearestBeat, durationBeats));
            }

            return result;
        }

        /// <summary>Peak-to-mean ratio of per-phase salience under this grouping --
        /// higher means one beat position within the measure clearly stands out as the
        /// downbeat, which is the signature a real, consistent meter leaves behind.</summary>
        private static double ScoreGrouping(List<(int BeatIndex, double DurationBeats)> beatIndices, int beatsPerMeasure)
        {
            var salience = new double[beatsPerMeasure];
            foreach (var (beatIndex, durationBeats) in beatIndices)
            {
                var phase = ((beatIndex % beatsPerMeasure) + beatsPerMeasure) % beatsPerMeasure;
                salience[phase] += 1.0 + durationBeats;
            }

            var mean = salience.Average();
            if (mean <= 0)
            {
                return 0.0;
            }

            return salience.Max() / mean;
        }
    }
}
