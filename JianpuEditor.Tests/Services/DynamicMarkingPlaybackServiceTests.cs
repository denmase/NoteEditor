using JianpuEditor.Services;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public sealed class DynamicMarkingPlaybackServiceTests
    {
        [Theory]
        [InlineData("pp", DynamicMarkingPlaybackService.PianissimoVelocity)]
        [InlineData("p", DynamicMarkingPlaybackService.PianoVelocity)]
        [InlineData("mp", DynamicMarkingPlaybackService.MezzoPianoVelocity)]
        [InlineData("mf", DynamicMarkingPlaybackService.MezzoForteVelocity)]
        [InlineData("f", DynamicMarkingPlaybackService.ForteVelocity)]
        [InlineData("ff", DynamicMarkingPlaybackService.FortissimoVelocity)]
        public void ResolveVelocity_MapsKnownDynamicText(string text, int expected)
        {
            Assert.Equal(expected, DynamicMarkingPlaybackService.ResolveVelocity(text, 90));
        }

        [Fact]
        public void ResolveVelocity_FallsBackForUnknownText()
        {
            Assert.Equal(90, DynamicMarkingPlaybackService.ResolveVelocity("cresc.", 90));
            Assert.Equal(90, DynamicMarkingPlaybackService.ResolveVelocity(null, 90));
        }

        [Fact]
        public void ResolveVelocity_LevelsIncreaseMonotonically()
        {
            Assert.True(DynamicMarkingPlaybackService.PianissimoVelocity < DynamicMarkingPlaybackService.PianoVelocity);
            Assert.True(DynamicMarkingPlaybackService.PianoVelocity < DynamicMarkingPlaybackService.MezzoPianoVelocity);
            Assert.True(DynamicMarkingPlaybackService.MezzoPianoVelocity < DynamicMarkingPlaybackService.MezzoForteVelocity);
            Assert.True(DynamicMarkingPlaybackService.MezzoForteVelocity < DynamicMarkingPlaybackService.ForteVelocity);
            Assert.True(DynamicMarkingPlaybackService.ForteVelocity < DynamicMarkingPlaybackService.FortissimoVelocity);
        }
    }
}
