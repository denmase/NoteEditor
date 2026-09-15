using System;
using System.Collections.Generic;
using JianpuEditor.Models;
using JianpuEditor.Services;

namespace JianpuEditor.Rendering
{
    public sealed class PlaybackMeasureSegment
    {
        public double StartBeat { get; set; }

        public double DurationBeat { get; set; }

        public int X { get; set; }

        public int Width { get; set; }

        public int BlockTop { get; set; }
    }

    public sealed class PlaybackMarkerPosition
    {
        public int X { get; set; }

        public int Top { get; set; }

        public int Bottom { get; set; }

        public bool IsVisible { get; set; }
    }

    public static class PlaybackLayout
    {
        public static IReadOnlyList<PlaybackMeasureSegment> BuildSegments(JianpuScore score, int width)
        {
            var renderer = new JianpuRenderer();
            return renderer.BuildPlaybackSegments(score, width);
        }

        public static PlaybackMarkerPosition GetMarkerPosition(
            IReadOnlyList<PlaybackMeasureSegment> segments,
            double quarterBeat)
        {
            var marker = new PlaybackMarkerPosition();
            if (segments == null || segments.Count == 0)
            {
                return marker;
            }

            var beat = Math.Max(0, quarterBeat);
            foreach (var segment in segments)
            {
                var endBeat = segment.StartBeat + segment.DurationBeat;
                if (beat < segment.StartBeat - 0.0001 || beat > endBeat + 0.0001)
                {
                    continue;
                }

                var fraction = segment.DurationBeat <= 0
                    ? 0
                    : (beat - segment.StartBeat) / segment.DurationBeat;
                fraction = Math.Max(0, Math.Min(1, fraction));
                marker.X = segment.X + (int)Math.Round(segment.Width * fraction);
                marker.Top = segment.BlockTop;
                marker.Bottom = segment.BlockTop + JianpuRenderer.StaffBlockHeight;
                marker.IsVisible = true;
                return marker;
            }

            var last = segments[segments.Count - 1];
            marker.X = last.X + last.Width;
            marker.Top = last.BlockTop;
            marker.Bottom = last.BlockTop + JianpuRenderer.StaffBlockHeight;
            marker.IsVisible = true;
            return marker;
        }

        /// <summary>
        /// Maps a screen position to a beat. Every line/system of a multi-line score repeats
        /// roughly the same X range, so matching on X alone -- as this used to -- always resolved
        /// to whichever row happened to come first in <paramref name="segments"/> (the top line),
        /// regardless of which line the pointer was actually over. <paramref name="y"/>
        /// disambiguates: segments are matched against the row it falls in (or the nearest row, if
        /// dragging above/below the score entirely) before X is used to find the beat within it.
        /// </summary>
        public static double MapXToBeat(IReadOnlyList<PlaybackMeasureSegment> segments, int x, int y)
        {
            if (segments == null || segments.Count == 0)
            {
                return 0;
            }

            var rowTop = FindNearestRowBlockTop(segments, y);
            PlaybackMeasureSegment first = null;
            PlaybackMeasureSegment last = null;
            foreach (var segment in segments)
            {
                if (segment.BlockTop != rowTop)
                {
                    continue;
                }

                if (first == null)
                {
                    first = segment;
                }

                last = segment;

                if (x < segment.X || x > segment.X + segment.Width)
                {
                    continue;
                }

                var fraction = segment.Width <= 0 ? 0 : (double)(x - segment.X) / segment.Width;
                return segment.StartBeat + segment.DurationBeat * Math.Max(0, Math.Min(1, fraction));
            }

            if (first != null && x < first.X)
            {
                return first.StartBeat;
            }

            return last != null ? last.StartBeat + last.DurationBeat : 0;
        }

        private static int FindNearestRowBlockTop(IReadOnlyList<PlaybackMeasureSegment> segments, int y)
        {
            var bestTop = segments[0].BlockTop;
            var bestDistance = int.MaxValue;
            foreach (var segment in segments)
            {
                if (y >= segment.BlockTop && y <= segment.BlockTop + JianpuRenderer.StaffBlockHeight)
                {
                    return segment.BlockTop;
                }

                var distance = y < segment.BlockTop
                    ? segment.BlockTop - y
                    : y - (segment.BlockTop + JianpuRenderer.StaffBlockHeight);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTop = segment.BlockTop;
                }
            }

            return bestTop;
        }

        public static double GetTotalBeats(IReadOnlyList<PlaybackMeasureSegment> segments)
        {
            if (segments == null || segments.Count == 0)
            {
                return 0;
            }

            var last = segments[segments.Count - 1];
            return last.StartBeat + last.DurationBeat;
        }
    }
}
