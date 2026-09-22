using JianpuEditor.Models;
using JianpuEditor.Services;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public sealed class VoiceLayoutServiceTests
    {
        [Fact]
        public void GetRenderOrder_NoExtraVoices_ReturnsOnlyThePrimaryVoice()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1));

            var order = VoiceLayoutService.GetRenderOrder(measure);

            Assert.Single(order);
            Assert.True(order[0].IsPrimary);
            Assert.Same(measure.MelodyNotes, order[0].Notes);
        }

        [Fact]
        public void HasMultipleVoices_NoExtraVoices_IsFalse()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1));

            Assert.False(VoiceLayoutService.HasMultipleVoices(measure));
        }

        [Fact]
        public void GetRenderOrder_SatbVoices_RendersBelowThePrimaryInListOrder()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1));
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Alto", Notes = { ScoreTestHelper.Note(5) } });
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Tenor", Notes = { ScoreTestHelper.Note(3) } });
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Bass", Notes = { ScoreTestHelper.Note(1) } });

            var order = VoiceLayoutService.GetRenderOrder(measure);

            Assert.Equal(4, order.Count);
            Assert.True(order[0].IsPrimary);
            Assert.Equal("Alto", order[1].Role);
            Assert.Equal("Tenor", order[2].Role);
            Assert.Equal("Bass", order[3].Role);
            Assert.True(VoiceLayoutService.HasMultipleVoices(measure));
        }

        [Fact]
        public void GetRenderOrder_DescantAboveSatb_RendersDescantFirst()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1));
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Descant", Notes = { ScoreTestHelper.Note(5, octave: 1) }, IsAbove = true });
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Alto", Notes = { ScoreTestHelper.Note(5) } });

            var order = VoiceLayoutService.GetRenderOrder(measure);

            Assert.Equal(3, order.Count);
            Assert.Equal("Descant", order[0].Role);
            Assert.True(order[1].IsPrimary);
            Assert.Equal("Alto", order[2].Role);
        }

        [Fact]
        public void GetRenderOrder_ExtraVoiceIndex_MatchesPositionInExtraVoicesList()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1));
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Descant", IsAbove = true });
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Alto" });
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Bass" });

            var order = VoiceLayoutService.GetRenderOrder(measure);

            Assert.Equal(0, order[0].ExtraVoiceIndex); // Descant
            Assert.Equal(VoiceLayoutService.PrimaryVoiceIndex, order[1].ExtraVoiceIndex); // primary
            Assert.Equal(1, order[2].ExtraVoiceIndex); // Alto
            Assert.Equal(2, order[3].ExtraVoiceIndex); // Bass
        }
    }
}
