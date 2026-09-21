using System.Collections.Generic;
using System.Linq;
using JianpuEditor.Models;

namespace JianpuEditor.Services
{
    /// <summary>Expands a score's measures into the actual sequence of measure indices heard
    /// during playback/export, resolving repeat bar lines (<see cref="JianpuMeasure.IsRepeatStart"/>
    /// / <see cref="BarLineType.RepeatEnd"/>) and volta brackets (<see cref="JianpuVolta"/>).
    /// <see cref="ScoreMidiSchedule"/> previously assumed one straight linear pass through
    /// <c>Measures</c>, which rendered repeat marks and voltas as pure decoration with no effect
    /// on what was actually heard or exported.</summary>
    public static class RepeatPlaybackExpander
    {
        /// <summary>Returns the sequence of measure indices to actually schedule, in play order
        /// (a repeated measure's index appears more than once). Standard notation semantics: a
        /// <see cref="BarLineType.RepeatEnd"/> plays its enclosing section exactly twice (jumping
        /// back once to the nearest preceding <see cref="JianpuMeasure.IsRepeatStart"/>, or the
        /// start of the score if none is marked -- the common shorthand for "repeat from the
        /// beginning"); a measure covered by a volta bracket is only included on the pass whose
        /// number matches the bracket's <see cref="JianpuVolta.Label"/> (parsed as a leading
        /// integer -- the only label values the UI itself ever creates are "1." and "2.") and is
        /// skipped on every other pass. Repeated sections that don't overlap each other each get
        /// their own fresh 1st-ending pass; nested repeats aren't standard notation and aren't
        /// specially handled here.</summary>
        public static List<int> Expand(IReadOnlyList<JianpuMeasure> measures, IReadOnlyList<JianpuVolta> voltas)
        {
            var order = new List<int>();
            if (measures == null || measures.Count == 0)
            {
                return order;
            }

            var voltaList = voltas ?? new List<JianpuVolta>();
            var repeatEndsUsed = new HashSet<int>();
            var startsSeen = new HashSet<int>();
            var lastRepeatStart = 0;
            var pass = 1;
            var i = 0;

            // Each RepeatEnd measure can only ever trigger its one jump-back (tracked via
            // repeatEndsUsed), so the walk below visits at most (measures.Count) extra measures
            // per RepeatEnd in the whole score -- bounding total steps well below this guard for
            // any real score, while still catching a pathological/malformed input rather than
            // hanging forever.
            var iterationGuard = measures.Count * measures.Count + 16;
            var iterations = 0;

            while (i >= 0 && i < measures.Count)
            {
                if (++iterations > iterationGuard)
                {
                    return LinearFallback(measures.Count);
                }

                if (measures[i].IsRepeatStart && !startsSeen.Contains(i))
                {
                    startsSeen.Add(i);
                    lastRepeatStart = i;
                    pass = 1;
                }

                var volta = FindVoltaCovering(voltaList, i);
                if (volta != null && ParsePassNumber(volta.Label) != pass)
                {
                    i++;
                    continue;
                }

                order.Add(i);

                if (measures[i].BarLineType == BarLineType.RepeatEnd && !repeatEndsUsed.Contains(i))
                {
                    repeatEndsUsed.Add(i);
                    pass = 2;
                    i = lastRepeatStart;
                    continue;
                }

                i++;
            }

            return order;
        }

        private static List<int> LinearFallback(int measureCount)
        {
            var order = new List<int>(measureCount);
            for (var i = 0; i < measureCount; i++)
            {
                order.Add(i);
            }

            return order;
        }

        private static JianpuVolta FindVoltaCovering(IReadOnlyList<JianpuVolta> voltas, int measureIndex)
        {
            for (var i = 0; i < voltas.Count; i++)
            {
                if (measureIndex >= voltas[i].StartMeasureIndex && measureIndex <= voltas[i].EndMeasureIndex)
                {
                    return voltas[i];
                }
            }

            return null;
        }

        private static int ParsePassNumber(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return 1;
            }

            var digits = new string(label.TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digits, out var pass) && pass > 0 ? pass : 1;
        }
    }
}
