using JianpuEditor.Services;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public class GeneralMidiInstrumentsTests
    {
        [Fact]
        public void Names_HasAllOneHundredTwentyEightGeneralMidiPrograms()
        {
            Assert.Equal(128, GeneralMidiInstruments.Names.Length);
        }

        [Fact]
        public void Names_FirstEntryIsAcousticGrandPiano()
        {
            Assert.Equal("Acoustic Grand Piano", GeneralMidiInstruments.Names[0]);
        }

        [Fact]
        public void GetName_ReturnsMatchingProgramName()
        {
            Assert.Equal("Violin", GeneralMidiInstruments.GetName(40));
        }

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(0, 0)]
        [InlineData(127, 127)]
        [InlineData(200, 127)]
        public void Clamp_KeepsProgramWithinValidRange(int input, int expected)
        {
            Assert.Equal(expected, GeneralMidiInstruments.Clamp(input));
        }
    }
}
