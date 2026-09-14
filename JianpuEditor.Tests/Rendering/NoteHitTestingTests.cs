using System;
using System.Drawing;
using System.Linq;
using JianpuEditor.Models;
using JianpuEditor.Rendering;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Rendering
{
    /// <summary>
    /// Regression coverage for the shared draw/hit-test geometry in
    /// <see cref="JianpuRenderer.MeasureLayout"/>. Drawing and hit-testing used to compute a
    /// note's on-screen position independently (a scaled formula for drawing, a raw unscaled one
    /// for hit-testing), so a click could resolve to the wrong note wherever the two disagreed.
    /// </summary>
    public sealed class NoteHitTestingTests
    {
        [Fact]
        public void MeasureLayout_AlwaysSqueezesSlightly_UnderTheEditorsContentFitSizing()
        {
            // JianpuRenderer.CalculateMeasureWidth sizes a measure's box to exactly fit its own
            // note content in the app's normal (non-paginated) layout, then ApplyMelodyScale
            // subtracts a fixed 4px margin from that same width -- so MelodyScale ends up
            // marginally below 1.0 for any non-empty measure, not just deliberately dense ones.
            var measure = ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1),
                ScoreTestHelper.Note(2),
                ScoreTestHelper.Note(3),
                ScoreTestHelper.Note(4));

            var layout = BuildContentFitLayout(measure, x: 40, blockTop: 0);

            Assert.True(layout.MelodyScale < 1.0);
        }

        [Fact]
        public void GetNoteBounds_AgreesWithGetNoteDrawBounds_ForEveryNote()
        {
            var measure = ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1),
                ScoreTestHelper.Note(2),
                ScoreTestHelper.Note(3),
                ScoreTestHelper.Note(4));

            var layout = BuildContentFitLayout(measure, x: 40, blockTop: 0);

            for (var i = 0; i < measure.MelodyNotes.Count; i++)
            {
                layout.GetNoteDrawBounds(i, out var noteX, out var noteWidth);
                var bounds = layout.GetNoteBounds(i);

                Assert.Equal(noteX, bounds.X);
                Assert.Equal(noteWidth, bounds.Width);
            }
        }

        [Fact]
        public void GetNoteDrawBounds_LastNoteReachesMeasureRightEdge_WhenSqueezed()
        {
            var measure = ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1),
                ScoreTestHelper.Note(2),
                ScoreTestHelper.Note(3),
                ScoreTestHelper.Note(4));

            var layout = BuildContentFitLayout(measure, x: 40, blockTop: 0);
            var lastIndex = measure.MelodyNotes.Count - 1;

            layout.GetNoteDrawBounds(lastIndex, out var noteX, out var noteWidth);

            Assert.Equal(layout.X + layout.Width, noteX + noteWidth);
        }

        [Fact]
        public void HitTest_ClickAtActualLastNotePosition_ResolvesToThatNote()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(
                    ScoreTestHelper.Note(1),
                    ScoreTestHelper.Note(2),
                    ScoreTestHelper.Note(3),
                    ScoreTestHelper.Note(4)));
            var measure = score.Measures[0];
            var lastIndex = measure.MelodyNotes.Count - 1;

            // Mirror exactly how JianpuRenderer.HitTest lays out the first measure of a
            // single-line score (BuildLayout -> CalculateMeasureWidth -> ApplyMelodyScale), then
            // use the new shared GetNoteDrawBounds as the oracle for where the last note is
            // actually drawn -- the same source of truth drawing itself now uses.
            var expectedLayout = BuildContentFitLayout(measure, x: JianpuRenderer.MarginLeft, blockTop: JianpuRenderer.MarginTop);
            expectedLayout.GetNoteDrawBounds(lastIndex, out var noteX, out var noteWidth);
            Assert.True(expectedLayout.MelodyScale < 1.0, "Test setup expects the measure to be marginally squeezed, matching production sizing.");

            var clickPoint = new Point(noteX + noteWidth / 2, JianpuRenderer.MarginTop + 10);
            var renderer = new JianpuRenderer();

            var hit = renderer.HitTest(score, 900, clickPoint);

            Assert.Equal(ScoreHitType.Note, hit.HitType);
            Assert.Equal(lastIndex, hit.NoteIndex);
        }

        [Fact]
        public void GetInsertOffset_MatchesScaledContentWidth_WhenSqueezed()
        {
            var measure = ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1),
                ScoreTestHelper.Note(2),
                ScoreTestHelper.Note(3),
                ScoreTestHelper.Note(4));

            var layout = BuildContentFitLayout(measure, x: 40, blockTop: 0);
            var noteCount = measure.MelodyNotes.Count;

            // The "append" gap (after the last note) must line up with where the last note's
            // stretched-to-fill edge actually is, not the measure's raw unscaled content width.
            layout.GetNoteDrawBounds(noteCount - 1, out var lastNoteX, out var lastNoteWidth);
            Assert.Equal(lastNoteX + lastNoteWidth - layout.X, layout.GetInsertOffset(noteCount));
        }

        private static JianpuRenderer.MeasureLayout BuildContentFitLayout(JianpuMeasure measure, int x, int blockTop)
        {
            var contentWidth = measure.MelodyNotes.Sum(note => JianpuRenderer.GetNoteWidth(note));
            var layout = new JianpuRenderer.MeasureLayout
            {
                X = x,
                BlockTop = blockTop,
                Width = Math.Max(JianpuRenderer.MinMeasureWidth, contentWidth)
            };
            layout.ComputeNoteLayout(measure);
            layout.ApplyMelodyScale(layout.Width);
            return layout;
        }
    }
}
