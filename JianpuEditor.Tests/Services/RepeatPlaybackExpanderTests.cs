using System.Collections.Generic;
using JianpuEditor.Models;
using JianpuEditor.Services;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public sealed class RepeatPlaybackExpanderTests
    {
        private static JianpuMeasure Measure(BarLineType barLineType = BarLineType.Single, bool isRepeatStart = false)
        {
            return new JianpuMeasure { BarLineType = barLineType, IsRepeatStart = isRepeatStart };
        }

        [Fact]
        public void Expand_NoRepeatsOrVoltas_IsAStraightLinearPass()
        {
            var measures = new List<JianpuMeasure> { Measure(), Measure(), Measure() };

            var order = RepeatPlaybackExpander.Expand(measures, new List<JianpuVolta>());

            Assert.Equal(new[] { 0, 1, 2 }, order);
        }

        [Fact]
        public void Expand_RepeatEndWithNoExplicitStart_JumpsBackToTheBeginning()
        {
            var measures = new List<JianpuMeasure> { Measure(), Measure(), Measure(BarLineType.RepeatEnd) };

            var order = RepeatPlaybackExpander.Expand(measures, new List<JianpuVolta>());

            Assert.Equal(new[] { 0, 1, 2, 0, 1, 2 }, order);
        }

        [Fact]
        public void Expand_RepeatEndWithExplicitStart_JumpsBackToTheMarkedMeasureOnly()
        {
            // Measure 0 (an intro) plays once; the repeat only covers measures 1-3.
            var measures = new List<JianpuMeasure>
            {
                Measure(),
                Measure(isRepeatStart: true),
                Measure(),
                Measure(BarLineType.RepeatEnd)
            };

            var order = RepeatPlaybackExpander.Expand(measures, new List<JianpuVolta>());

            Assert.Equal(new[] { 0, 1, 2, 3, 1, 2, 3 }, order);
        }

        [Fact]
        public void Expand_FirstAndSecondEndings_EachOnlyPlayOnTheirOwnPass()
        {
            // A B |1st ending, RepeatEnd| ... A B |2nd ending| D
            var measures = new List<JianpuMeasure>
            {
                Measure(),
                Measure(),
                Measure(BarLineType.RepeatEnd),
                Measure(),
                Measure()
            };
            var voltas = new List<JianpuVolta>
            {
                new JianpuVolta { StartMeasureIndex = 2, EndMeasureIndex = 2, Label = "1." },
                new JianpuVolta { StartMeasureIndex = 3, EndMeasureIndex = 3, Label = "2." }
            };

            var order = RepeatPlaybackExpander.Expand(measures, voltas);

            Assert.Equal(new[] { 0, 1, 2, 0, 1, 3, 4 }, order);
        }

        [Fact]
        public void Expand_TwoSeparateRepeatedSections_EachGetsItsOwnFreshFirstPass()
        {
            var measures = new List<JianpuMeasure>
            {
                Measure(),
                Measure(BarLineType.RepeatEnd),
                Measure(),
                Measure(BarLineType.RepeatEnd)
            };

            var order = RepeatPlaybackExpander.Expand(measures, new List<JianpuVolta>());

            Assert.Equal(new[] { 0, 1, 0, 1, 2, 3, 2, 3 }, order);
        }

        [Fact]
        public void Expand_EmptyMeasures_ReturnsEmptyOrder()
        {
            var order = RepeatPlaybackExpander.Expand(new List<JianpuMeasure>(), new List<JianpuVolta>());

            Assert.Empty(order);
        }

        [Fact]
        public void Expand_VoltaWithUnparseableLabel_DefaultsToAlwaysPlayingOnPassOne()
        {
            var measures = new List<JianpuMeasure> { Measure(), Measure(BarLineType.RepeatEnd) };
            var voltas = new List<JianpuVolta>
            {
                new JianpuVolta { StartMeasureIndex = 1, EndMeasureIndex = 1, Label = "Ending" }
            };

            var order = RepeatPlaybackExpander.Expand(measures, voltas);

            // "Ending" has no leading digit, so it defaults to pass 1 and only plays on the first pass.
            Assert.Equal(new[] { 0, 1, 0 }, order);
        }
    }
}
