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
        public void HitTest_ClickOnBelowVoiceRow_ResolvesToThatVoicesOwnNote()
        {
            var renderer = new JianpuRenderer();
            var score = BuildSatbScore();
            var layout = renderer.GetMeasureLayouts(score, 900)[0];

            var altoRowY = layout.BlockTop + (JianpuRenderer.MelodyRowHeight + JianpuRenderer.RowGap) + JianpuRenderer.MelodyRowHeight / 2;
            var hit = renderer.HitTest(score, 900, new Point(layout.X + 10, altoRowY));

            Assert.Equal(ScoreHitType.Note, hit.HitType);
            Assert.Equal(0, hit.MeasureIndex);
            Assert.Equal(0, hit.NoteIndex);
            Assert.Equal(0, hit.VoiceIndex);
        }

        [Fact]
        public void HitTest_ClickOnRowGapBetweenVoiceRows_ResolvesAsGenericMeasureHit()
        {
            var renderer = new JianpuRenderer();
            var score = BuildSatbScore();
            var layout = renderer.GetMeasureLayouts(score, 900)[0];

            // Between the Alto row and the Tenor row -- inside RowGap, not on either row's own
            // MelodyRowHeight span -- still falls through to a generic Measure hit.
            var gapY = layout.BlockTop + 2 * (JianpuRenderer.MelodyRowHeight + JianpuRenderer.RowGap) - JianpuRenderer.RowGap / 2;
            var hit = renderer.HitTest(score, 900, new Point(layout.X + 10, gapY));

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

        private static JianpuScore BuildDescantAboveSatbScore()
        {
            var score = new JianpuScore { Title = "Descant above SATB" };
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2));
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Descant", IsAbove = true, Notes = { ScoreTestHelper.Note(5), ScoreTestHelper.Note(6) } });
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Alto", Notes = { ScoreTestHelper.Note(5), ScoreTestHelper.Note(5) } });
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Tenor", Notes = { ScoreTestHelper.Note(3), ScoreTestHelper.Note(3) } });
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Bass", Notes = { ScoreTestHelper.Note(1), ScoreTestHelper.Note(1) } });
            score.Measures.Add(measure);
            return score;
        }

        [Fact]
        public void GetMeasureLayouts_DescantAboveSatb_ReportsOneAboveRow()
        {
            var renderer = new JianpuRenderer();
            var layout = renderer.GetMeasureLayouts(BuildDescantAboveSatbScore(), 900)[0];

            Assert.Equal(1, layout.AboveVoiceRowCount);
            Assert.Equal(3, layout.BelowVoiceRowCount);
            Assert.Equal(JianpuRenderer.MelodyRowHeight + JianpuRenderer.RowGap, layout.GetAboveVoicesHeight());
        }

        [Fact]
        public void BlockTop_MeasureWithAboveVoice_ShiftsDownToMakeRoom()
        {
            var renderer = new JianpuRenderer();
            var plainScore = new JianpuScore { Title = "Plain" };
            plainScore.Measures.Add(ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));
            var plainLayout = renderer.GetMeasureLayouts(plainScore, 900)[0];

            var descantLayout = renderer.GetMeasureLayouts(BuildDescantAboveSatbScore(), 900)[0];

            // BlockTop (the melody row's own top) shifts down by exactly the above-voice headroom,
            // but the real top of drawn content (GetDrawTop) stays exactly where it always started.
            Assert.Equal(
                plainLayout.BlockTop + JianpuRenderer.MelodyRowHeight + JianpuRenderer.RowGap,
                descantLayout.BlockTop);
            Assert.Equal(plainLayout.BlockTop, descantLayout.GetDrawTop());
        }

        [Fact]
        public void GetDrawTop_NoAboveVoices_EqualsBlockTop()
        {
            var renderer = new JianpuRenderer();
            var layout = renderer.GetMeasureLayouts(BuildSatbScore(), 900)[0];

            Assert.Equal(0, layout.AboveVoiceRowCount);
            Assert.Equal(layout.BlockTop, layout.GetDrawTop());
        }

        [Fact]
        public void RenderToBitmap_DescantAboveSatb_HasInkInTheDescantRow()
        {
            var renderer = new JianpuRenderer();
            var score = BuildDescantAboveSatbScore();
            var layout = renderer.GetMeasureLayouts(score, 900)[0];

            using (var bitmap = renderer.RenderToBitmap(score, 900))
            {
                var descantRowTop = layout.GetDrawTop();
                var foundInk = false;
                for (var y = descantRowTop; y < descantRowTop + JianpuRenderer.MelodyRowHeight && !foundInk; y++)
                {
                    for (var x = layout.X; x < layout.X + layout.Width && !foundInk; x++)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        if (pixel.A > 0 && pixel.GetBrightness() < 0.5)
                        {
                            foundInk = true;
                        }
                    }
                }

                Assert.True(foundInk, "expected visible ink in the Descant row, above the melody row");
            }
        }

        [Fact]
        public void HitTest_ClickAboveMelodyRow_ResolvesToTheAboveVoicesOwnNote()
        {
            var renderer = new JianpuRenderer();
            var score = BuildDescantAboveSatbScore();
            var layout = renderer.GetMeasureLayouts(score, 900)[0];

            var descantRowY = layout.GetDrawTop() + JianpuRenderer.MelodyRowHeight / 2;
            var hit = renderer.HitTest(score, 900, new Point(layout.X + 10, descantRowY));

            Assert.Equal(ScoreHitType.Note, hit.HitType);
            Assert.Equal(0, hit.MeasureIndex);
            Assert.Equal(0, hit.NoteIndex);
            Assert.Equal(0, hit.VoiceIndex);
        }

        [Fact]
        public void HitTest_ClickInGapBetweenAboveVoiceRowAndMelody_ResolvesAsGenericMeasureHit()
        {
            var renderer = new JianpuRenderer();
            var score = BuildDescantAboveSatbScore();
            var layout = renderer.GetMeasureLayouts(score, 900)[0];

            // Between the Descant row's own bottom and the melody row's top (BlockTop) -- inside
            // RowGap, not on the Descant row's own MelodyRowHeight span -- still falls through to a
            // generic Measure hit, same as the equivalent gap between two "below" voice rows.
            var gapY = layout.GetDrawTop() + JianpuRenderer.MelodyRowHeight + JianpuRenderer.RowGap / 2;
            var hit = renderer.HitTest(score, 900, new Point(layout.X + 10, gapY));

            Assert.Equal(ScoreHitType.Measure, hit.HitType);
            Assert.Equal(0, hit.MeasureIndex);
        }

        [Fact]
        public void RenderToBitmap_SatbScore_LabelsEachExtraVoiceRowWithItsOwnRole()
        {
            var renderer = new JianpuRenderer();
            var score = BuildSatbScore();

            using (var satbBitmap = renderer.RenderToBitmap(score, 900, ScoreLayoutOptions.Editor))
            {
                var plainScore = new JianpuScore { Title = "Plain" };
                plainScore.Measures.Add(ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));
                using (var plainBitmap = renderer.RenderToBitmap(plainScore, 900, ScoreLayoutOptions.Editor))
                {
                    var satbInk = CountLabelColumnInk(satbBitmap);
                    var plainInk = CountLabelColumnInk(plainBitmap);

                    // The SATB score's label column has the same fixed "Melody"/"Dynamics"/
                    // "Secondary"/"Lyrics" labels as the plain score, plus three new ones ("Alto"/
                    // "Tenor"/"Bass") -- previously the extra voice rows had no label at all.
                    Assert.True(satbInk > plainInk, $"expected more label ink with 3 extra voice rows labeled, got satb={satbInk} plain={plainInk}");
                }
            }
        }

        private static int CountLabelColumnInk(Bitmap bitmap)
        {
            var count = 0;
            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 4; x < 70 && x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (pixel.R < 200 || pixel.G < 200 || pixel.B < 200)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
