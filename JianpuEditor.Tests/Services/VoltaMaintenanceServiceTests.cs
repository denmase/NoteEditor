using JianpuEditor.Models;
using JianpuEditor.Services;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public class VoltaMaintenanceServiceTests
    {
        [Fact]
        public void OnMeasureRemoved_BeforeRange_ShiftsRangeDown()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1)),
                ScoreTestHelper.Measure(ScoreTestHelper.Note(2)),
                ScoreTestHelper.Measure(ScoreTestHelper.Note(3)));
            score.Voltas.Add(new JianpuVolta { StartMeasureIndex = 1, EndMeasureIndex = 2, Label = "1." });

            VoltaMaintenanceService.OnMeasureRemoved(score, 0);

            Assert.Single(score.Voltas);
            Assert.Equal(0, score.Voltas[0].StartMeasureIndex);
            Assert.Equal(1, score.Voltas[0].EndMeasureIndex);
        }

        [Fact]
        public void OnMeasureRemoved_InsideMultiMeasureRange_ShrinksRange()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1)),
                ScoreTestHelper.Measure(ScoreTestHelper.Note(2)),
                ScoreTestHelper.Measure(ScoreTestHelper.Note(3)));
            score.Voltas.Add(new JianpuVolta { StartMeasureIndex = 0, EndMeasureIndex = 2, Label = "1." });

            VoltaMaintenanceService.OnMeasureRemoved(score, 1);

            Assert.Single(score.Voltas);
            Assert.Equal(0, score.Voltas[0].StartMeasureIndex);
            Assert.Equal(1, score.Voltas[0].EndMeasureIndex);
        }

        [Fact]
        public void OnMeasureRemoved_OnlyMeasureInSingleMeasureVolta_RemovesVolta()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1)),
                ScoreTestHelper.Measure(ScoreTestHelper.Note(2)));
            score.Voltas.Add(new JianpuVolta { StartMeasureIndex = 1, EndMeasureIndex = 1, Label = "1." });

            VoltaMaintenanceService.OnMeasureRemoved(score, 1);

            Assert.Empty(score.Voltas);
        }

        [Fact]
        public void OnMeasureRemoved_AfterRange_LeavesRangeUnchanged()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1)),
                ScoreTestHelper.Measure(ScoreTestHelper.Note(2)),
                ScoreTestHelper.Measure(ScoreTestHelper.Note(3)));
            score.Voltas.Add(new JianpuVolta { StartMeasureIndex = 0, EndMeasureIndex = 1, Label = "1." });

            VoltaMaintenanceService.OnMeasureRemoved(score, 2);

            Assert.Single(score.Voltas);
            Assert.Equal(0, score.Voltas[0].StartMeasureIndex);
            Assert.Equal(1, score.Voltas[0].EndMeasureIndex);
        }
    }
}
