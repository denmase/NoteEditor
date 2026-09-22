using System.Collections.Generic;
using System.IO;
using JianpuEditor.Services;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public sealed class MarkovChordModelTests
    {
        private static MarkovChordModel BuildTrained()
        {
            var model = new MarkovChordModel();
            model.Train(new List<IReadOnlyList<string>>
            {
                new[] { "I", "vi", "IV", "V" },
                new[] { "I", "vi", "IV", "V" },
                new[] { "I", "V", "vi", "IV" },
                new[] { "ii", "V", "I", "vi" },
                new[] { "ii", "V", "I", "vi" }
            });
            return model;
        }

        [Fact]
        public void Probability_HighForCommonTransition()
        {
            var model = BuildTrained();
            Assert.True(model.Probability("IV", "vi") > 0.9);
        }

        [Fact]
        public void Probability_LowForRareTransition()
        {
            // "IV" never follows itself in the training data (only via smoothing), while "vi" ->
            // "IV" is the most common transition seen -- the unseen transition should score
            // noticeably lower than the well-attested one.
            var model = BuildTrained();
            Assert.True(model.Probability("IV", "IV") < model.Probability("IV", "vi"));
        }

        [Fact]
        public void Score_PrefersCadenceFromV()
        {
            var model = BuildTrained();
            var toI = model.Score("I", "V", "ii");
            var toIV = model.Score("IV", "V", "ii");
            Assert.True(toI > toIV);
        }

        [Fact]
        public void SaveAndLoad_Roundtrip_Order1()
        {
            var model = BuildTrained();
            var path = Path.GetTempFileName();
            model.Save(path);
            var loaded = MarkovChordModel.Load(path);
            Assert.Equal(model.Probability("IV", "vi"), loaded.Probability("IV", "vi"), 3);
        }

        [Fact]
        public void SaveAndLoad_Roundtrip_Order2()
        {
            var model = BuildTrained();
            var path = Path.GetTempFileName();
            model.Save(path);
            var loaded = MarkovChordModel.Load(path);
            Assert.True(loaded.TotalTransitions2 > 0);
            Assert.Equal(model.Probability("I", "V", "ii"), loaded.Probability("I", "V", "ii"), 3);
        }

        [Fact]
        public void Load_IgnoresMalformedLines()
        {
            var path = Path.GetTempFileName();
            File.WriteAllLines(path, new[]
            {
                "﻿1|I|V|3",
                "1|V|notanumber",
                "2|ii|V|I|2",
                "garbage",
                "2|ii|V|I|NaN"
            });
            var loaded = MarkovChordModel.Load(path);
            Assert.True(loaded.TotalTransitions1 > 0);
            Assert.True(loaded.TotalTransitions2 > 0);
        }

        [Fact]
        public void ChordTransitionTrainer_TrainFromLines_IgnoresCommentsAndBlankLines()
        {
            var model = ChordTransitionTrainer.TrainFromLines(new[]
            {
                "# a comment",
                "",
                "I IV V I",
                "I V"
            });

            Assert.True(model.TotalTransitions1 > 0);
            Assert.True(model.Probability("IV", "I") > 0);
        }
    }
}
