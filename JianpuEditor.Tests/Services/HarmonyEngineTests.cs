using System.Collections.Generic;
using JianpuEditor.Models;
using JianpuEditor.Services;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public sealed class HarmonyEngineTests
    {
        private static HarmonyMelodyNote N(int degree, double duration = 1.0)
        {
            return new HarmonyMelodyNote { Degree = degree, Duration = duration };
        }

        [Fact]
        public void SuggestForMeasure_EmptyNotes_ReturnsEmpty()
        {
            var engine = new HarmonyEngine();
            var result = engine.SuggestForMeasure(new List<HarmonyMelodyNote>(), keyRootMidi: 60);
            Assert.Empty(result);
        }

        [Fact]
        public void SuggestForMeasure_MelodyOnOne_IncludesTonicFamily()
        {
            var engine = new HarmonyEngine();
            var result = engine.SuggestForMeasure(new List<HarmonyMelodyNote> { N(1), N(1), N(1) }, keyRootMidi: 60);

            Assert.NotEmpty(result);
            Assert.Contains(result, candidate => candidate.Roman.Degree == 1 || candidate.Roman.Degree == 6 || candidate.Roman.Degree == 4);
        }

        [Fact]
        public void SuggestForMeasure_LowAdventurousness_ExcludesBorrowedAndSecondaryDominantChords()
        {
            var engine = new HarmonyEngine();
            var result = engine.SuggestForMeasure(new List<HarmonyMelodyNote> { N(1) }, keyRootMidi: 60, adventurousness: 0, maxResults: 20);

            Assert.DoesNotContain(result, candidate => candidate.Roman.Accidental != 0);
            Assert.DoesNotContain(result, candidate => candidate.Roman.SecondaryOf != null);
        }

        [Fact]
        public void SuggestForMeasure_WithMarkovModel_RanksCorpusFrequentChordAboveEquallyFitCandidate()
        {
            // "I" and "vi" both fit a melody that sits on degree 1 (I contains it as root, vi
            // contains it as its third), but "I" is far more common in the training corpus.
            var markov = new MarkovChordModel();
            markov.Train(new List<IReadOnlyList<string>>
            {
                new[] { "I", "IV", "V", "I" },
                new[] { "I", "V", "I" },
                new[] { "I", "IV", "I" }
            });

            var engine = new HarmonyEngine(markov);
            var result = engine.SuggestForMeasure(new List<HarmonyMelodyNote> { N(1), N(1), N(1) }, keyRootMidi: 60, maxResults: 20);

            var iIndex = result.FindIndex(c => c.Roman.ToString() == "I");
            var viIndex = result.FindIndex(c => c.Roman.ToString() == "vi");
            Assert.True(iIndex >= 0 && viIndex >= 0);
            Assert.True(iIndex < viIndex, "Markov-trained engine should rank the corpus-frequent chord ('I') above an equally-fitting rare one ('vi').");
        }

        [Fact]
        public void ProgressionEngine_OneBar_UsesTruncatedTemplate()
        {
            var engine = new ProgressionEngine();
            var measures = new List<IReadOnlyList<HarmonyMelodyNote>>
            {
                new List<HarmonyMelodyNote> { N(1), N(2), N(3), N(4) }
            };

            var result = engine.SuggestProgression(measures, keyRootMidi: 60);
            Assert.NotEmpty(result);
            Assert.All(result, r => Assert.Single(r.Chords));
        }

        [Fact]
        public void ProgressionEngine_FourBar_ReturnsTopThreeSorted()
        {
            var engine = new ProgressionEngine();
            var measures = new List<IReadOnlyList<HarmonyMelodyNote>>
            {
                new List<HarmonyMelodyNote> { N(1), N(2), N(3), N(4) },
                new List<HarmonyMelodyNote> { N(4), N(3), N(2), N(1) },
                new List<HarmonyMelodyNote> { N(5), N(5), N(4), N(3) },
                new List<HarmonyMelodyNote> { N(1), N(2), N(1), N(1) }
            };

            var result = engine.SuggestProgression(measures, keyRootMidi: 60);
            Assert.Equal(3, result.Count);
            Assert.True(result[0].Score >= result[1].Score);
            Assert.True(result[1].Score >= result[2].Score);
        }

        [Fact]
        public void ProgressionEngine_NoMeasures_ReturnsEmpty()
        {
            var engine = new ProgressionEngine();
            var result = engine.SuggestProgression(new List<IReadOnlyList<HarmonyMelodyNote>>(), keyRootMidi: 60);
            Assert.Empty(result);
        }

        [Fact]
        public void ProgressionEngine_LongerThanFourMeasures_CoversEveryMeasureInsteadOfTruncating()
        {
            // A whole song is usually well past the 4-measure templates above; every candidate
            // progression must still cover every measure rather than silently stopping at 4.
            var engine = new ProgressionEngine();
            var measures = new List<IReadOnlyList<HarmonyMelodyNote>>();
            for (var i = 0; i < 9; i++)
            {
                measures.Add(new List<HarmonyMelodyNote> { N(1), N(2), N(3), N(4) });
            }

            var result = engine.SuggestProgression(measures, keyRootMidi: 60);

            Assert.NotEmpty(result);
            Assert.All(result, r => Assert.Equal(9, r.Chords.Count));
        }
    }
}
