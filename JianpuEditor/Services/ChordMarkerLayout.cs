using System;
using System.Collections.Generic;
using System.Drawing;
using JianpuEditor.Models;
using JianpuEditor.Rendering;

namespace JianpuEditor.Services
{
    public sealed class ChordMarkerBounds
    {
        public int MeasureIndex { get; set; } = -1;

        public int MarkerIndex { get; set; } = -1;

        public Rectangle TextBoxBounds { get; set; } = Rectangle.Empty;

        public Rectangle DragHandleBounds { get; set; } = Rectangle.Empty;

        public Rectangle DeleteBounds { get; set; } = Rectangle.Empty;

        public float AnchorX { get; set; }
    }

    public static class ChordMarkerLayout
    {
        public const int TextBoxWidth = 72;
        public const int TextBoxHeight = 24;
        public const int DragHandleWidth = 14;
        public const int DeleteButtonWidth = 18;

        public static float GetBeatAnchorX(JianpuRenderer.MeasureLayout layout, JianpuMeasure measure, double beatPosition)
        {
            var notes = measure.MelodyNotes;
            var duration = ScoreMidiSchedule.GetMeasureDurationUnits(measure);
            if (duration <= 0)
            {
                duration = ScoreMidiSchedule.DefaultMeasureBeats;
            }

            beatPosition = ChordMarkerService.SnapBeatPosition(beatPosition, duration);
            if (beatPosition <= 0.0001)
            {
                return layout.X;
            }

            if (notes == null || notes.Count == 0)
            {
                return layout.X + (float)(beatPosition / duration * layout.Width);
            }

            var noteCount = notes.Count;
            var elapsed = 0.0;

            for (var i = 0; i < noteCount; i++)
            {
                var noteDuration = JianpuRenderer.GetDurationUnits(notes[i]);
                if (noteDuration <= 0)
                {
                    noteDuration = 1;
                }

                if (elapsed >= beatPosition - 0.0001)
                {
                    layout.GetNoteDrawBounds(i, out var startNoteX, out _);
                    return startNoteX;
                }

                if (elapsed + noteDuration > beatPosition + 0.0001)
                {
                    layout.GetNoteDrawBounds(i, out var noteX, out var noteWidth);
                    var fraction = (float)((beatPosition - elapsed) / noteDuration);
                    return noteX + noteWidth * fraction;
                }

                elapsed += noteDuration;
            }

            return layout.X + layout.Width;
        }

        public static ChordMarkerBounds GetMarkerBounds(
            JianpuRenderer.MeasureLayout layout,
            JianpuMeasure measure,
            int measureIndex,
            int markerIndex,
            ChordMarker marker)
        {
            var rowTop = JianpuRenderer.GetSecondaryRowTop(layout) + 4;
            var anchorX = GetBeatAnchorX(layout, measure, marker.BeatPosition);
            var left = (int)Math.Round(anchorX);
            var top = rowTop;

            return new ChordMarkerBounds
            {
                MeasureIndex = measureIndex,
                MarkerIndex = markerIndex,
                AnchorX = anchorX,
                TextBoxBounds = new Rectangle(left, top, TextBoxWidth, TextBoxHeight),
                DeleteBounds = new Rectangle(left + TextBoxWidth, top, DeleteButtonWidth, TextBoxHeight),
                DragHandleBounds = new Rectangle(
                    left + TextBoxWidth + DeleteButtonWidth,
                    top,
                    DragHandleWidth,
                    TextBoxHeight)
            };
        }

        public static List<ChordMarkerBounds> BuildBounds(
            JianpuScore score,
            IReadOnlyList<JianpuRenderer.MeasureLayout> measureLayouts)
        {
            var result = new List<ChordMarkerBounds>();
            if (score?.Measures == null || measureLayouts == null)
            {
                return result;
            }

            foreach (var layout in measureLayouts)
            {
                if (layout.MeasureIndex < 0 || layout.MeasureIndex >= score.Measures.Count)
                {
                    continue;
                }

                var measure = score.Measures[layout.MeasureIndex];
                ChordMarkerService.NormalizeMeasure(measure);
                for (var i = 0; i < measure.ChordMarkers.Count; i++)
                {
                    result.Add(GetMarkerBounds(layout, measure, layout.MeasureIndex, i, measure.ChordMarkers[i]));
                }
            }

            return result;
        }

        public static Rectangle GetSecondaryRowBounds(JianpuRenderer.MeasureLayout layout)
        {
            var top = JianpuRenderer.GetSecondaryRowTop(layout);
            return new Rectangle(layout.X, top, layout.Width, JianpuRenderer.SecondaryRowHeight);
        }
    }
}
