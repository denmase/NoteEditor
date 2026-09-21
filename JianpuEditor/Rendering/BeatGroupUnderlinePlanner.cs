using System.Collections.Generic;
using JianpuEditor.Models;

namespace JianpuEditor.Rendering
{
    /// <summary>Groups a measure's notes into quarter-beat clusters and, within each cluster,
    /// works out where a given underline (beam) level actually reaches -- broken up so a shorter
    /// note (fewer underlines) interrupting the group, like an eighth note sandwiched between two
    /// sixteenths, breaks the deeper underline level instead of letting it span across the shorter
    /// note's own region.</summary>
    public static class BeatGroupUnderlinePlanner
    {
        public readonly struct UnderlineSpan
        {
            public UnderlineSpan(int start, int end)
            {
                Start = start;
                End = end;
            }

            public int Start { get; }

            public int End { get; }
        }

        public static List<List<int>> GroupNotesByQuarterBeat(List<JianpuNote> notes)
        {
            var groups = new List<List<int>>();
            var current = new List<int>();
            var sum = 0.0;

            for (var i = 0; i < notes.Count; i++)
            {
                var duration = JianpuRenderer.GetDurationUnits(notes[i]);
                if (duration <= 0)
                {
                    duration = 1;
                }

                if (current.Count > 0 && sum + duration > 1.0001)
                {
                    groups.Add(current);
                    current = new List<int>();
                    sum = 0;
                }

                current.Add(i);
                sum += duration;

                if (sum >= 0.9999)
                {
                    groups.Add(current);
                    current = new List<int>();
                    sum = 0;
                }
            }

            if (current.Count > 0)
            {
                groups.Add(current);
            }

            return groups;
        }

        public static List<UnderlineSpan> CollectSpans(List<int> group, List<JianpuNote> notes, int underlineIndex)
        {
            var spans = new List<UnderlineSpan>();
            var spanStart = -1;
            var spanEnd = -1;
            foreach (var noteIndex in group)
            {
                if (notes[noteIndex].Underlines <= underlineIndex)
                {
                    if (spanStart >= 0)
                    {
                        spans.Add(new UnderlineSpan(spanStart, spanEnd));
                        spanStart = -1;
                        spanEnd = -1;
                    }

                    continue;
                }

                if (spanStart < 0)
                {
                    spanStart = noteIndex;
                }

                spanEnd = noteIndex;
            }

            if (spanStart >= 0)
            {
                spans.Add(new UnderlineSpan(spanStart, spanEnd));
            }

            return spans;
        }
    }
}
