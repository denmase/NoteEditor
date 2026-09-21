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

        [Fact]
        public void CollectSpans_ContinuationDotBetweenNotes_ParticipatesInBothBeamLevels()
        {
            // "5 . 4" from the real notasi angka spec (Dot_Beat.md): 5 is an eighth (depth1 only),
            // the continuation dot and 4 are sixteenths sharing a deeper beam (depth2) -- exactly the
            // nested pattern validated in the scratchpad prototype before this feature was built.
            // Proves BeatGroupUnderlinePlanner needs zero changes to handle a continuation dot: it
            // only cares about each slot's own Underlines, regardless of NoteType.
            var notes = new System.Collections.Generic.List<JianpuNote>
            {
                ScoreTestHelper.Note(5, underlines: 1),
                ScoreTestHelper.ContinuationDot(underlines: 2),
                ScoreTestHelper.Note(4, underlines: 2)
            };
            var group = new System.Collections.Generic.List<int> { 0, 1, 2 };

            var shallow = BeatGroupUnderlinePlanner.CollectSpans(group, notes, 0);
            var deep = BeatGroupUnderlinePlanner.CollectSpans(group, notes, 1);

            Assert.Single(shallow);
            Assert.Equal(0, shallow[0].Start);
            Assert.Equal(2, shallow[0].End);

            Assert.Single(deep);
            Assert.Equal(1, deep[0].Start);
            Assert.Equal(2, deep[0].End);
        }

        [Fact]
        public void GroupNotesByQuarterBeat_ContinuationDotCountsAsItsOwnBeatGridSlot()
        {
            var notes = new System.Collections.Generic.List<JianpuNote>
            {
                ScoreTestHelper.Note(5, underlines: 1),
                ScoreTestHelper.ContinuationDot(underlines: 1),
                ScoreTestHelper.Note(6, underlines: 1),
                ScoreTestHelper.Note(7, underlines: 1)
            };

            var groups = BeatGroupUnderlinePlanner.GroupNotesByQuarterBeat(notes);

            Assert.Equal(2, groups.Count);
            Assert.Equal(new[] { 0, 1 }, groups[0]);
            Assert.Equal(new[] { 2, 3 }, groups[1]);
        }

        [Fact]
        public void RenderToBitmap_ContinuationDotBeamedWithNote_DoesNotThrow()
        {
            var measure = ScoreTestHelper.Measure(
                ScoreTestHelper.Note(3, underlines: 0),
                ScoreTestHelper.ContinuationDot(underlines: 1),
                ScoreTestHelper.Note(4, underlines: 1));

            var score = ScoreTestHelper.CreateScore(measure);
            using (var renderer = new JianpuRenderer())
            {
                Assert.NotNull(renderer.RenderToBitmap(score, 1280, ScoreLayoutOptions.Editor));
                Assert.NotNull(renderer.RenderToBitmap(score, 1280, ScoreLayoutOptions.Default));
            }
        }
    }
}
