using System.Drawing;
using JianpuEditor.Controls;
using JianpuEditor.Models;
using Xunit;

namespace JianpuEditor.Tests.Controls
{
    public sealed class ScoreCanvasPlaybackMarkerTests
    {
        [Fact]
        public void SeekPlaybackToLogicalLocation_MovesMarkerAwayFromFirstBar()
        {
            // Regression test: dragging the playback marker can only *start* by grabbing its
            // current (narrow) on-screen position, which is always the first bar right after a
            // load/new/reset -- so without another way in, the marker feels permanently pinned
            // there. This exercises the same seek path the new "Move playback marker here"
            // context-menu item uses, confirming it moves the marker to wherever was clicked
            // regardless of where the marker started.
            using var canvas = new ScoreCanvas();
            canvas.Score = BuildEightMeasureScore();
            canvas.CreateControl();

            canvas.SetPlaybackPosition(0, showHead: true, ensureVisible: false);
            Assert.Equal(0, canvas.PlaybackPositionQuarter, 3);

            double? seekedBeat = null;
            canvas.PlaybackSeeked += beat => seekedBeat = beat;

            var lateMeasure = canvas.GetPlaybackSegments()[4];
            var clickLocation = new Point(lateMeasure.X + lateMeasure.Width / 2, lateMeasure.BlockTop + 10);

            canvas.SeekPlaybackToLogicalLocation(clickLocation);

            var expectedBeat = lateMeasure.StartBeat + lateMeasure.DurationBeat / 2;
            Assert.True(seekedBeat.HasValue);
            // 1 decimal place, not 2: MapXToBeat now resolves against each note's own real
            // (integer-pixel-rounded) drawn position via MeasureLayout.XToBeat instead of assuming
            // beats are spaced perfectly evenly across the measure's width, so a geometric
            // pixel-middle click lands a hair off the arithmetic beat-middle -- by design, since it's
            // now tracking where the glyphs actually are.
            Assert.Equal(expectedBeat, seekedBeat!.Value, 1);
            Assert.Equal(expectedBeat, canvas.PlaybackPositionQuarter, 1);
            Assert.True(canvas.PlaybackPositionQuarter > 1.0);
        }

        private static JianpuScore BuildEightMeasureScore()
        {
            var score = new JianpuScore
            {
                Title = "Probe",
                KeySignature = "1=C",
                TimeSignature = "4/4",
                Bpm = 120
            };

            for (var m = 0; m < 8; m++)
            {
                score.Measures.Add(new JianpuMeasure
                {
                    MelodyNotes = new System.Collections.Generic.List<JianpuNote>
                    {
                        new JianpuNote { Type = NoteType.Note, Pitch = 1 },
                        new JianpuNote { Type = NoteType.Note, Pitch = 2 },
                        new JianpuNote { Type = NoteType.Note, Pitch = 3 },
                        new JianpuNote { Type = NoteType.Note, Pitch = 4 }
                    },
                    LyricText = " "
                });
            }

            return score;
        }
    }
}
