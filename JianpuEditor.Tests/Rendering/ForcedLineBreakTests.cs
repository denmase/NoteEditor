using System.Collections.Generic;
using JianpuEditor.Models;
using JianpuEditor.Rendering;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Rendering
{
    /// <summary>Covers <see cref="JianpuMeasure.ForcesLineBreak"/> -- the only way to make a line
    /// wrap somewhere other than where automatic width-based wrapping would put it. Before this,
    /// there was no way at all to force a line break; wrapping was purely a function of how many
    /// measures fit the available width.</summary>
    public sealed class ForcedLineBreakTests
    {
        private static JianpuScore BuildFourMeasureScore()
        {
            var score = new JianpuScore { Title = "Line break" };
            for (var i = 0; i < 4; i++)
            {
                score.Measures.Add(ScoreTestHelper.Measure(
                    ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4)));
            }

            return score;
        }

        [Fact]
        public void GetMeasureLayouts_NoForcedBreak_AllMeasuresFitOnOneLineWhenWidthAllows()
        {
            var renderer = new JianpuRenderer();
            var layouts = renderer.GetMeasureLayouts(BuildFourMeasureScore(), 2000, ScoreLayoutOptions.Editor);

            var distinctTops = new HashSet<int>();
            foreach (var measure in layouts)
            {
                distinctTops.Add(measure.BlockTop);
            }

            Assert.Single(distinctTops);
        }

        [Fact]
        public void GetMeasureLayouts_ForcedBreakOnAMeasure_WrapsRightAfterIt()
        {
            var score = BuildFourMeasureScore();
            score.Measures[1].ForcesLineBreak = true;

            var renderer = new JianpuRenderer();
            var layouts = renderer.GetMeasureLayouts(score, 2000, ScoreLayoutOptions.Editor);

            Assert.Equal(layouts[0].BlockTop, layouts[1].BlockTop);
            Assert.NotEqual(layouts[1].BlockTop, layouts[2].BlockTop);
            Assert.Equal(layouts[2].BlockTop, layouts[3].BlockTop);
        }

        [Fact]
        public void GetMeasureLayouts_ForcedBreakOnTheLastMeasure_DoesNotCreateATrailingEmptyLine()
        {
            var score = BuildFourMeasureScore();
            score.Measures[3].ForcesLineBreak = true;

            var renderer = new JianpuRenderer();
            var layouts = renderer.GetMeasureLayouts(score, 2000, ScoreLayoutOptions.Editor);

            var distinctTops = new HashSet<int>();
            foreach (var measure in layouts)
            {
                distinctTops.Add(measure.BlockTop);
            }

            Assert.Single(distinctTops);
        }
    }
}
