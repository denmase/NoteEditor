using JianpuEditor.Models;
using JianpuEditor.Rendering;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Rendering
{
    public sealed class BeatGroupUnderlinePlannerTests
    {
        [Fact]
        public void CollectSpans_EighthBetweenSixteenths_BreaksDeeperLevelAtTheEighth()
        {
            var notes = new System.Collections.Generic.List<JianpuNote>
            {
                ScoreTestHelper.Note(1, underlines: 2),
                ScoreTestHelper.Note(2, underlines: 1),
                ScoreTestHelper.Note(3, underlines: 2)
            };
            var group = new System.Collections.Generic.List<int> { 0, 1, 2 };

            var levelZero = BeatGroupUnderlinePlanner.CollectSpans(group, notes, 0);
            var levelOne = BeatGroupUnderlinePlanner.CollectSpans(group, notes, 1);

            Assert.Single(levelZero);
            Assert.Equal(0, levelZero[0].Start);
            Assert.Equal(2, levelZero[0].End);

            Assert.Equal(2, levelOne.Count);
            Assert.Equal(0, levelOne[0].Start);
            Assert.Equal(0, levelOne[0].End);
            Assert.Equal(2, levelOne[1].Start);
            Assert.Equal(2, levelOne[1].End);
        }

        [Fact]
        public void CollectSpans_FourConsecutiveSixteenths_IsOneContinuousSpanAtEveryLevel()
        {
            var notes = new System.Collections.Generic.List<JianpuNote>
            {
                ScoreTestHelper.Note(1, underlines: 2),
                ScoreTestHelper.Note(2, underlines: 2),
                ScoreTestHelper.Note(3, underlines: 2),
                ScoreTestHelper.Note(4, underlines: 2)
            };
            var group = new System.Collections.Generic.List<int> { 0, 1, 2, 3 };

            var levelOne = BeatGroupUnderlinePlanner.CollectSpans(group, notes, 1);

            Assert.Single(levelOne);
            Assert.Equal(0, levelOne[0].Start);
            Assert.Equal(3, levelOne[0].End);
        }

        [Fact]
        public void CollectSpans_SixteenthPairsWithEighthsBetween_BreaksAtEachEighth()
        {
            var notes = new System.Collections.Generic.List<JianpuNote>
            {
                ScoreTestHelper.Note(1, underlines: 2),
                ScoreTestHelper.Note(2, underlines: 2),
                ScoreTestHelper.Note(3, underlines: 1),
                ScoreTestHelper.Note(4, underlines: 2),
                ScoreTestHelper.Note(5, underlines: 2)
            };
            var group = new System.Collections.Generic.List<int> { 0, 1, 2, 3, 4 };

            var levelOne = BeatGroupUnderlinePlanner.CollectSpans(group, notes, 1);

            Assert.Equal(2, levelOne.Count);
            Assert.Equal(0, levelOne[0].Start);
            Assert.Equal(1, levelOne[0].End);
            Assert.Equal(3, levelOne[1].Start);
            Assert.Equal(4, levelOne[1].End);
        }

        [Fact]
        public void RenderToBitmap_EighthBetweenSixteenths_DoesNotThrow()
        {
            var measure = ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1, underlines: 2),
                ScoreTestHelper.Note(2, underlines: 1),
                ScoreTestHelper.Note(3, underlines: 2),
                ScoreTestHelper.Note(4, dashes: 1));

            var score = ScoreTestHelper.CreateScore(measure);
            using (var renderer = new JianpuRenderer())
            {
                Assert.NotNull(renderer.RenderToBitmap(score, 1280, ScoreLayoutOptions.Editor));
                Assert.NotNull(renderer.RenderToBitmap(score, 1280, ScoreLayoutOptions.Default));
            }
        }
    }
}
