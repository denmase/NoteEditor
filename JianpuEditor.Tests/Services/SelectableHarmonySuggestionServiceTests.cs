using System.Collections.Generic;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Models;
using JianpuEditor.Services;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public sealed class SelectableHarmonySuggestionServiceTests
    {
        private sealed class FakeHarmonyService : IHarmonySuggestionService
        {
            public string Tag;

            public IReadOnlyList<HarmonySuggestion> SuggestForMeasure(JianpuMeasure measure, string keySignature, double beatPosition)
            {
                return new[] { new HarmonySuggestion { RomanNumeral = Tag } };
            }

            public IReadOnlyList<HarmonyProgressionSuggestion> SuggestForMeasureRange(IReadOnlyList<JianpuMeasure> measures, string keySignature)
            {
                return new[] { new HarmonyProgressionSuggestion { Label = Tag } };
            }
        }

        [Fact]
        public void DefaultsToLegacy()
        {
            var service = new SelectableHarmonySuggestionService(
                new FakeHarmonyService { Tag = "legacy" },
                new FakeHarmonyService { Tag = "markov" });

            Assert.Equal(HarmonySuggestionEngineKind.Legacy, service.ActiveKind);

            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1));
            var result = service.SuggestForMeasure(measure, "1=C", 0);
            Assert.Equal("legacy", result[0].RomanNumeral);
        }

        [Fact]
        public void SwitchingActiveKind_DelegatesToMarkov()
        {
            var service = new SelectableHarmonySuggestionService(
                new FakeHarmonyService { Tag = "legacy" },
                new FakeHarmonyService { Tag = "markov" })
            {
                ActiveKind = HarmonySuggestionEngineKind.Markov
            };

            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1));
            var result = service.SuggestForMeasure(measure, "1=C", 0);
            Assert.Equal("markov", result[0].RomanNumeral);

            var progressionResult = service.SuggestForMeasureRange(new[] { measure }, "1=C");
            Assert.Equal("markov", progressionResult[0].Label);
        }

        [Fact]
        public void ImplementsEngineOptionsCapability()
        {
            var service = new SelectableHarmonySuggestionService(
                new FakeHarmonyService { Tag = "legacy" },
                new FakeHarmonyService { Tag = "markov" });

            Assert.IsAssignableFrom<IHarmonySuggestionEngineOptions>(service);
        }

        [Fact]
        public void MarkovHarmonySuggestionService_SuggestForMeasure_ReturnsMelodyFitCandidates()
        {
            var service = new MarkovHarmonySuggestionService();
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(1), ScoreTestHelper.Note(1));

            var result = service.SuggestForMeasure(measure, "1=C", 0);

            Assert.NotEmpty(result);
            Assert.Contains(result, s => s.RomanNumeral == "I");
        }

        [Fact]
        public void MarkovHarmonySuggestionService_SuggestForMeasure_EmptyMeasure_ReturnsEmpty()
        {
            var service = new MarkovHarmonySuggestionService();
            var measure = ScoreTestHelper.Measure();

            var result = service.SuggestForMeasure(measure, "1=C", 0);

            Assert.Empty(result);
        }

        [Fact]
        public void MarkovHarmonySuggestionService_SuggestForMeasure_InvalidKeySignature_ReturnsEmpty()
        {
            var service = new MarkovHarmonySuggestionService();
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1));

            var result = service.SuggestForMeasure(measure, "not a key", 0);

            Assert.Empty(result);
        }

        [Fact]
        public void MarkovHarmonySuggestionService_SuggestForMeasureRange_ReturnsProgressionSuggestions()
        {
            var service = new MarkovHarmonySuggestionService();
            var measures = new[]
            {
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4)),
                ScoreTestHelper.Measure(ScoreTestHelper.Note(5), ScoreTestHelper.Note(5), ScoreTestHelper.Note(4), ScoreTestHelper.Note(3))
            };

            var result = service.SuggestForMeasureRange(measures, "1=C");

            Assert.NotEmpty(result);
            Assert.All(result, r => Assert.Equal(2, r.Steps.Count));
        }

        [Fact]
        public void MarkovHarmonySuggestionService_SuggestForMeasureRange_WholeSong_CoversEveryMeasure()
        {
            var service = new MarkovHarmonySuggestionService();
            var measures = new List<JianpuMeasure>();
            for (var i = 0; i < 10; i++)
            {
                measures.Add(ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4)));
            }

            var result = service.SuggestForMeasureRange(measures, "1=C");

            Assert.NotEmpty(result);
            Assert.All(result, r => Assert.Equal(10, r.Steps.Count));
            Assert.All(result, r => Assert.Equal(9, r.Steps[r.Steps.Count - 1].MeasureOffset));
        }
    }
}
