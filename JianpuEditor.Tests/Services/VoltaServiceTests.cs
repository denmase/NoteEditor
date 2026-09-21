using JianpuEditor.Models;
using JianpuEditor.Services;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public class VoltaServiceTests
    {
        [Fact]
        public void TryAddVolta_ValidRange_Adds()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1)),
                ScoreTestHelper.Measure(ScoreTestHelper.Note(2)),
                ScoreTestHelper.Measure(ScoreTestHelper.Note(3)));

            var added = VoltaService.TryAddVolta(score, 1, 2, "1.", out var message);

            Assert.True(added);
            Assert.Single(score.Voltas);
            Assert.Equal(1, score.Voltas[0].StartMeasureIndex);
            Assert.Equal(2, score.Voltas[0].EndMeasureIndex);
            Assert.Equal("1.", score.Voltas[0].Label);
            Assert.Equal("Added volta bracket", message);
        }

        [Fact]
        public void TryAddVolta_OutOfRange_Fails()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));

            var added = VoltaService.TryAddVolta(score, 0, 5, "1.", out var message);

            Assert.False(added);
            Assert.Empty(score.Voltas);
            Assert.NotEmpty(message);
        }

        [Fact]
        public void TryAddVolta_OverlappingExisting_Fails()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1)),
                ScoreTestHelper.Measure(ScoreTestHelper.Note(2)),
                ScoreTestHelper.Measure(ScoreTestHelper.Note(3)));
            VoltaService.TryAddVolta(score, 0, 1, "1.", out _);

            var added = VoltaService.TryAddVolta(score, 1, 2, "2.", out var message);

            Assert.False(added);
            Assert.Single(score.Voltas);
            Assert.NotEmpty(message);
        }

        [Fact]
        public void TryRemoveVoltaCovering_MeasureInsideRange_Removes()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1)),
                ScoreTestHelper.Measure(ScoreTestHelper.Note(2)),
                ScoreTestHelper.Measure(ScoreTestHelper.Note(3)));
            VoltaService.TryAddVolta(score, 1, 2, "1.", out _);

            var removed = VoltaService.TryRemoveVoltaCovering(score, 2, out var message);

            Assert.True(removed);
            Assert.Empty(score.Voltas);
            Assert.Equal("Removed volta bracket", message);
        }

        [Fact]
        public void TryRemoveVoltaCovering_MeasureOutsideRange_Fails()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1)),
                ScoreTestHelper.Measure(ScoreTestHelper.Note(2)));
            VoltaService.TryAddVolta(score, 0, 0, "1.", out _);

            var removed = VoltaService.TryRemoveVoltaCovering(score, 1, out var message);

            Assert.False(removed);
            Assert.Single(score.Voltas);
            Assert.NotEmpty(message);
        }
    }
}
