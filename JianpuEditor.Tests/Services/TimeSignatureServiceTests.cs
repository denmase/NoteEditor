using JianpuEditor.Services;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public class TimeSignatureServiceTests
    {
        [Theory]
        [InlineData("4/4", 4.0)]
        [InlineData("3/4", 3.0)]
        [InlineData("2/4", 2.0)]
        [InlineData("6/8", 3.0)]
        [InlineData("9/8", 4.5)]
        [InlineData(" 3 / 4 ", 3.0)]
        public void TryGetQuarterBeatsPerMeasure_ParsesCommonSignatures(string input, double expectedQuarterBeats)
        {
            var ok = TimeSignatureService.TryGetQuarterBeatsPerMeasure(input, out var quarterBeats);

            Assert.True(ok);
            Assert.Equal(expectedQuarterBeats, quarterBeats, 3);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("four/four")]
        [InlineData("4")]
        [InlineData("0/4")]
        [InlineData("4/0")]
        public void TryGetQuarterBeatsPerMeasure_RejectsInvalidInput(string input)
        {
            var ok = TimeSignatureService.TryGetQuarterBeatsPerMeasure(input, out _);

            Assert.False(ok);
        }
    }
}
