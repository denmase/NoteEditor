using System.Collections.Generic;
using System.Linq;
using JianpuEditor.Models;

namespace JianpuEditor.Services
{
    /// <summary>Applies D.C./D.S. navigation (<see cref="OrnamentType.DaCapo"/> /
    /// <see cref="OrnamentType.DalSegno"/>) on top of an already repeat/volta-expanded play order
    /// (see <see cref="RepeatPlaybackExpander"/>): once the main pass reaches the measure carrying
    /// the D.C./D.S. marker, playback jumps back to the beginning (D.C.) or the
    /// <see cref="OrnamentType.Segno"/> mark (D.S.), then plays straight through -- without
    /// re-applying repeat/volta expansion a second time, the common simplification real playback
    /// software makes -- until either an <see cref="OrnamentType.Fine"/> marker (stop there) or a
    /// pair of <see cref="OrnamentType.Coda"/> markers (jump from the first to the second) ends the
    /// piece; with neither, it just plays to the end. All navigation is resolved at measure
    /// granularity, matching how repeats/voltas already work.</summary>
    public static class SegnoCodaPlaybackExpander
    {
        public static List<int> ApplyNavigation(IReadOnlyList<JianpuMeasure> measures, List<int> mainOrder)
        {
            if (measures == null || measures.Count == 0 || mainOrder == null || mainOrder.Count == 0)
            {
                return mainOrder ?? new List<int>();
            }

            var daCapoMeasure = FindFirstMeasureWithOrnament(measures, OrnamentType.DaCapo);
            var dalSegnoMeasure = FindFirstMeasureWithOrnament(measures, OrnamentType.DalSegno);

            int triggerMeasure;
            int targetMeasure;
            if (dalSegnoMeasure.HasValue
                && (!daCapoMeasure.HasValue
                    || IndexOfLast(mainOrder, dalSegnoMeasure.Value) > IndexOfLast(mainOrder, daCapoMeasure.Value)))
            {
                triggerMeasure = dalSegnoMeasure.Value;
                targetMeasure = FindFirstMeasureWithOrnament(measures, OrnamentType.Segno) ?? 0;
            }
            else if (daCapoMeasure.HasValue)
            {
                triggerMeasure = daCapoMeasure.Value;
                targetMeasure = 0;
            }
            else
            {
                return mainOrder;
            }

            var triggerPosition = IndexOfLast(mainOrder, triggerMeasure);
            if (triggerPosition < 0)
            {
                return mainOrder;
            }

            var result = new List<int>(mainOrder.Take(triggerPosition + 1));
            var fineMeasure = FindFirstMeasureWithOrnament(measures, OrnamentType.Fine);
            var codaMeasures = FindAllMeasuresWithOrnament(measures, OrnamentType.Coda);

            if (fineMeasure.HasValue && fineMeasure.Value >= targetMeasure)
            {
                for (var m = targetMeasure; m <= fineMeasure.Value; m++)
                {
                    result.Add(m);
                }
            }
            else if (codaMeasures.Count >= 2)
            {
                var jumpFrom = codaMeasures[0];
                var jumpTo = codaMeasures[1];
                for (var m = targetMeasure; m < jumpFrom && m < measures.Count; m++)
                {
                    result.Add(m);
                }

                for (var m = jumpTo; m < measures.Count; m++)
                {
                    result.Add(m);
                }
            }
            else
            {
                for (var m = targetMeasure; m < measures.Count; m++)
                {
                    result.Add(m);
                }
            }

            return result;
        }

        private static int? FindFirstMeasureWithOrnament(IReadOnlyList<JianpuMeasure> measures, OrnamentType type)
        {
            for (var i = 0; i < measures.Count; i++)
            {
                if (measures[i]?.Ornaments != null && measures[i].Ornaments.Any(o => o != null && o.Type == type))
                {
                    return i;
                }
            }

            return null;
        }

        private static List<int> FindAllMeasuresWithOrnament(IReadOnlyList<JianpuMeasure> measures, OrnamentType type)
        {
            var result = new List<int>();
            for (var i = 0; i < measures.Count; i++)
            {
                if (measures[i]?.Ornaments != null && measures[i].Ornaments.Any(o => o != null && o.Type == type))
                {
                    result.Add(i);
                }
            }

            return result;
        }

        private static int IndexOfLast(List<int> order, int measureIndex)
        {
            for (var i = order.Count - 1; i >= 0; i--)
            {
                if (order[i] == measureIndex)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
