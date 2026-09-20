using JianpuEditor.Models;
using JianpuEditor.Services;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public sealed class DynamicMarkingServiceTests
    {
        [Fact]
        public void TrySetForNote_AddsMarking()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2));

            var added = DynamicMarkingService.TrySetForNote(measure, 1, "mf");

            Assert.True(added);
            Assert.Single(measure.Dynamics);
            Assert.Equal("mf", measure.Dynamics[0].Text);
            Assert.Equal(1, DynamicMarkingService.ResolveNoteIndex(measure, measure.Dynamics[0]));
        }

        [Fact]
        public void TrySetForNote_ReplacesExistingMarkingOnSameNote()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1));
            DynamicMarkingService.TrySetForNote(measure, 0, "p");

            DynamicMarkingService.TrySetForNote(measure, 0, "ff");

            Assert.Single(measure.Dynamics);
            Assert.Equal("ff", measure.Dynamics[0].Text);
        }

        [Fact]
        public void TryRemoveForNote_RemovesMarking()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1));
            DynamicMarkingService.TrySetForNote(measure, 0, "f");

            var removed = DynamicMarkingService.TryRemoveForNote(measure, 0);

            Assert.True(removed);
            Assert.Empty(measure.Dynamics);
        }

        [Fact]
        public void GetMarkingForNote_ReturnsNullWhenNoneSet()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1));

            Assert.Null(DynamicMarkingService.GetMarkingForNote(measure, 0));
        }

        [Fact]
        public void OnNoteRemoved_ShiftsLaterMarkingIndices()
        {
            var measure = ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1),
                ScoreTestHelper.Note(2),
                ScoreTestHelper.Note(3));
            DynamicMarkingService.TrySetForNote(measure, 2, "f");

            DynamicMarkingService.OnNoteRemoved(measure, 0);

            Assert.Single(measure.Dynamics);
            Assert.Equal(1, measure.Dynamics[0].NoteIndex);
        }

        [Fact]
        public void OnNoteRemoved_DropsMarkingOnTheRemovedNote()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2));
            DynamicMarkingService.TrySetForNote(measure, 0, "f");

            DynamicMarkingService.OnNoteRemoved(measure, 0);

            Assert.Empty(measure.Dynamics);
        }

        [Fact]
        public void NormalizeMeasure_DropsMarkingsWithEmptyText()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1));
            measure.Dynamics.Add(new DynamicMarking { Text = string.Empty, NoteIndex = 0 });

            DynamicMarkingService.NormalizeMeasure(measure);

            Assert.Empty(measure.Dynamics);
        }
    }
}
