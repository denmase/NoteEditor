using JianpuEditor.Services;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public sealed class JianpuNoteAdapterTests
    {
        [Fact]
        public void ToHarmonyMelodyNotes_PlainNotes_MapsDegreeAndDuration()
        {
            var notes = new[]
            {
                ScoreTestHelper.Note(1, dashes: 1), // 2 beats
                ScoreTestHelper.Note(5)              // 1 beat
            };

            var result = JianpuNoteAdapter.ToHarmonyMelodyNotes(notes);

            Assert.Equal(2, result.Count);
            Assert.Equal(1, result[0].Degree);
            Assert.Equal(2.0, result[0].Duration);
            Assert.Equal(5, result[1].Degree);
            Assert.Equal(1.0, result[1].Duration);
        }

        [Fact]
        public void ToHarmonyMelodyNotes_ContinuationDot_ExtendsPreviousNoteInsteadOfBeingDropped()
        {
            // A quarter note held for a second beat via a continuation dot -- NOT two separate
            // notes, and NOT silence: this is the bug the ported design's single-note adapter had
            // (it discarded every rest, continuation dots included, so a held note's extra beats
            // never reached the melody-fit weighting).
            var notes = new[]
            {
                ScoreTestHelper.Note(3),
                ScoreTestHelper.ContinuationDot()
            };

            var result = JianpuNoteAdapter.ToHarmonyMelodyNotes(notes);

            Assert.Single(result);
            Assert.Equal(3, result[0].Degree);
            Assert.Equal(2.0, result[0].Duration);
        }

        [Fact]
        public void ToHarmonyMelodyNotes_TrueRest_IsSkippedAndDoesNotExtendAnything()
        {
            var notes = new[]
            {
                ScoreTestHelper.Note(3),
                ScoreTestHelper.Rest(),
                ScoreTestHelper.Note(5)
            };

            var result = JianpuNoteAdapter.ToHarmonyMelodyNotes(notes);

            Assert.Equal(2, result.Count);
            Assert.Equal(1.0, result[0].Duration);
            Assert.Equal(5, result[1].Degree);
        }

        [Fact]
        public void ToHarmonyMelodyNotes_LeadingContinuationDot_IsIgnoredWhenNothingPrecedesIt()
        {
            var notes = new[] { ScoreTestHelper.ContinuationDot() };

            var result = JianpuNoteAdapter.ToHarmonyMelodyNotes(notes);

            Assert.Empty(result);
        }

        [Fact]
        public void ToHarmonyMelodyNotes_MultipleContinuationDots_EachExtendTheSameNote()
        {
            var notes = new[]
            {
                ScoreTestHelper.Note(1),
                ScoreTestHelper.ContinuationDot(),
                ScoreTestHelper.ContinuationDot()
            };

            var result = JianpuNoteAdapter.ToHarmonyMelodyNotes(notes);

            Assert.Single(result);
            Assert.Equal(3.0, result[0].Duration);
        }
    }
}
