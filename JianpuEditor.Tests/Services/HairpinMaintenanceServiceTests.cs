using JianpuEditor.Models;
using JianpuEditor.Services;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public class HairpinMaintenanceServiceTests
    {
        [Fact]
        public void OnNoteRemoved_RemovesHairpinReferencingDeletedNote()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1),
                ScoreTestHelper.Note(2),
                ScoreTestHelper.Note(3)));
            score.Hairpins.Add(new JianpuHairpin { StartMeasureIndex = 0, StartNoteIndex = 0, EndMeasureIndex = 0, EndNoteIndex = 2 });

            HairpinMaintenanceService.OnNoteRemoved(score, 0, 0);

            Assert.Empty(score.Hairpins);
        }

        [Fact]
        public void OnNoteRemoved_ShiftsLaterNoteIndicesInSameMeasure()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1),
                ScoreTestHelper.Note(2),
                ScoreTestHelper.Note(3),
                ScoreTestHelper.Note(4)));
            score.Hairpins.Add(new JianpuHairpin { StartMeasureIndex = 0, StartNoteIndex = 0, EndMeasureIndex = 0, EndNoteIndex = 3 });

            HairpinMaintenanceService.OnNoteRemoved(score, 0, 1);

            Assert.Single(score.Hairpins);
            Assert.Equal(2, score.Hairpins[0].EndNoteIndex);
        }

        [Fact]
        public void OnMeasureRemoved_RemovesHairpinsTouchingMeasureAndShiftsLaterMeasures()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1)),
                ScoreTestHelper.Measure(ScoreTestHelper.Note(2)),
                ScoreTestHelper.Measure(ScoreTestHelper.Note(3)));
            score.Hairpins.Add(new JianpuHairpin { StartMeasureIndex = 0, StartNoteIndex = 0, EndMeasureIndex = 1, EndNoteIndex = 0 });
            score.Hairpins.Add(new JianpuHairpin { StartMeasureIndex = 2, StartNoteIndex = 0, EndMeasureIndex = 2, EndNoteIndex = 0 });

            HairpinMaintenanceService.OnMeasureRemoved(score, 1);

            Assert.Single(score.Hairpins);
            Assert.Equal(1, score.Hairpins[0].StartMeasureIndex);
            Assert.Equal(1, score.Hairpins[0].EndMeasureIndex);
        }

        [Fact]
        public void OnMelodyNoteCountChanged_ShiftsIndicesAtOrAfterInsertPoint()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1),
                ScoreTestHelper.Note(2)));
            score.Hairpins.Add(new JianpuHairpin { StartMeasureIndex = 0, StartNoteIndex = 0, EndMeasureIndex = 0, EndNoteIndex = 1 });

            HairpinMaintenanceService.OnMelodyNoteCountChanged(score, 0, 1, 2);

            Assert.Equal(0, score.Hairpins[0].StartNoteIndex);
            Assert.Equal(3, score.Hairpins[0].EndNoteIndex);
        }
    }
}
