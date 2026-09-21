using JianpuEditor.Services;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public class HairpinServiceTests
    {
        [Fact]
        public void TryAddHairpin_ValidRange_AddsHairpin()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1),
                ScoreTestHelper.Note(2),
                ScoreTestHelper.Note(3)));

            var added = HairpinService.TryAddHairpin(score, 0, 0, 0, 2, true, out var message);

            Assert.True(added);
            Assert.Single(score.Hairpins);
            Assert.True(score.Hairpins[0].IsCrescendo);
            Assert.Contains("crescendo", message);
        }

        [Fact]
        public void TryAddHairpin_EndBeforeStart_Rejected()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1),
                ScoreTestHelper.Note(2)));

            var added = HairpinService.TryAddHairpin(score, 0, 1, 0, 0, false, out var message);

            Assert.False(added);
            Assert.Empty(score.Hairpins);
            Assert.False(string.IsNullOrEmpty(message));
        }

        [Fact]
        public void TryAddHairpin_SameStartAndEnd_Rejected()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));

            var added = HairpinService.TryAddHairpin(score, 0, 0, 0, 0, true, out _);

            Assert.False(added);
        }

        [Fact]
        public void TryAddHairpin_OutOfRangeNote_Rejected()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));

            var added = HairpinService.TryAddHairpin(score, 0, 0, 0, 5, true, out _);

            Assert.False(added);
        }

        [Fact]
        public void TryRemoveHairpinCovering_NoteInsideSpan_RemovesHairpin()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1),
                ScoreTestHelper.Note(2),
                ScoreTestHelper.Note(3)));
            HairpinService.TryAddHairpin(score, 0, 0, 0, 2, true, out _);

            var removed = HairpinService.TryRemoveHairpinCovering(score, 0, 1, out var message);

            Assert.True(removed);
            Assert.Empty(score.Hairpins);
            Assert.Equal("Removed hairpin", message);
        }

        [Fact]
        public void TryRemoveHairpinCovering_NoteOutsideSpan_NoOp()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2)),
                ScoreTestHelper.Measure(ScoreTestHelper.Note(3)));
            HairpinService.TryAddHairpin(score, 0, 0, 0, 1, true, out _);

            var removed = HairpinService.TryRemoveHairpinCovering(score, 1, 0, out _);

            Assert.False(removed);
            Assert.Single(score.Hairpins);
        }
    }
}
