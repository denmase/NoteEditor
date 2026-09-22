using System.Collections.Generic;
using System.Drawing;
using JianpuEditor.Models;
using JianpuEditor.Rendering;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Rendering
{
    /// <summary>Covers the first real (non-prototype) SATB rendering pass: a measure with <see
    /// cref="JianpuMeasure.ExtraVoices"/> shares one width across every voice, stacks each "below"
    /// voice (Alto/Tenor/Bass) in its own row under the melody, and pushes Dynamics/Secondary/
    /// Lyrics rows down to make room. A measure with no extra voices must render/hit-test/measure
    /// exactly as it did before this feature existed -- <see cref="StaffRowLayoutTests"/> already
    /// locks that invariant in for the row-position helpers; the tests here extend it to the new
    /// multi-voice path.</summary>
    public sealed class SatbRenderingTests
    {
        private static JianpuScore BuildSatbScore()
        {
            var score = new JianpuScore { Title = "SATB" };
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2));
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Alto", Notes = { ScoreTestHelper.Note(5), ScoreTestHelper.Note(5) } });
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Tenor", Notes = { ScoreTestHelper.Note(3), ScoreTestHelper.Note(3) } });
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Bass", Notes = { ScoreTestHelper.Note(1), ScoreTestHelper.Note(1) } });
            score.Measures.Add(measure);
            return score;
        }

        [Fact]
        public void ComputeExtraVoiceLayouts_NoExtraVoices_LeavesBelowVoicesEmpty()
        {
            var score = new JianpuScore { Title = "Plain" };
            score.Measures.Add(ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));

            var renderer = new JianpuRenderer();
            var layout = renderer.GetMeasureLayouts(score, 900)[0];

            Assert.Equal(0, layout.BelowVoiceRowCount);
            Assert.Equal(JianpuRenderer.StaffBlockHeight, layout.GetEffectiveHeight());
        }

        [Fact]
        public void GetMeasureLayouts_SatbMeasure_ReportsOneBelowRowPerExtraVoice()
        {
            var renderer = new JianpuRenderer();
            var layout = renderer.GetMeasureLayouts(BuildSatbScore(), 900)[0];

            Assert.Equal(3, layout.BelowVoiceRowCount);
            Assert.Equal(
                JianpuRenderer.StaffBlockHeight + 3 * (JianpuRenderer.MelodyRowHeight + JianpuRenderer.RowGap),
                layout.GetEffectiveHeight());
        }

        [Fact]
        public void GetDynamicsRowTop_SatbMeasure_SitsBelowEveryVoiceRow()
        {
            var renderer = new JianpuRenderer();
            var layout = renderer.GetMeasureLayouts(BuildSatbScore(), 900)[0];

            var expected = layout.BlockTop + 4 * (JianpuRenderer.MelodyRowHeight + JianpuRenderer.RowGap);
            Assert.Equal(expected, JianpuRenderer.GetDynamicsRowTop(layout));
        }

        [Fact]
        public void MeasureScore_SatbScore_IsTallerThanAPlainScore()
        {
            var renderer = new JianpuRenderer();
            var plainScore = new JianpuScore { Title = "Plain" };
            plainScore.Measures.Add(ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));

            var plainSize = renderer.MeasureScore(plainScore, 900);
            var satbSize = renderer.MeasureScore(BuildSatbScore(), 900);

            Assert.True(satbSize.Height > plainSize.Height);
        }

        [Fact]
        public void HitTest_ClickOnBelowVoiceRow_ResolvesAsGenericMeasureHit()
        {
            var renderer = new JianpuRenderer();
            var score = BuildSatbScore();
            var layout = renderer.GetMeasureLayouts(score, 900)[0];

            var altoRowY = layout.BlockTop + (JianpuRenderer.MelodyRowHeight + JianpuRenderer.RowGap) + JianpuRenderer.MelodyRowHeight / 2;
            var hit = renderer.HitTest(score, 900, new Point(layout.X + 10, altoRowY));

            Assert.Equal(ScoreHitType.Measure, hit.HitType);
            Assert.Equal(0, hit.MeasureIndex);
        }

        [Fact]
        public void HitTest_ClickOnMelodyRow_StillResolvesToThePrimaryNote()
        {
            var renderer = new JianpuRenderer();
            var score = BuildSatbScore();
            var layout = renderer.GetMeasureLayouts(score, 900)[0];

            layout.GetNoteDrawBounds(0, out var noteX, out var noteWidth);
            var hit = renderer.HitTest(score, 900, new Point(noteX + noteWidth / 2, layout.BlockTop + 20));

            Assert.Equal(ScoreHitType.Note, hit.HitType);
            Assert.Equal(0, hit.NoteIndex);
        }

        [Fact]
        public void RenderToBitmap_SatbScore_ProducesABitmapMatchingMeasuredHeight()
        {
            var renderer = new JianpuRenderer();
            var score = BuildSatbScore();
            var size = renderer.MeasureScore(score, 900);

            using (var bitmap = renderer.RenderToBitmap(score, 900))
            {
                Assert.Equal(size.Height, bitmap.Height);
            }
        }
    }
}
