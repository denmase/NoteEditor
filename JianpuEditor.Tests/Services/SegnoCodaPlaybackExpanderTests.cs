using System.Collections.Generic;
using JianpuEditor.Models;
using JianpuEditor.Services;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public sealed class SegnoCodaPlaybackExpanderTests
    {
        private static JianpuMeasure Measure(params JianpuOrnament[] ornaments)
        {
            return new JianpuMeasure { Ornaments = new List<JianpuOrnament>(ornaments) };
        }

        private static JianpuOrnament Ornament(OrnamentType type)
        {
            return new JianpuOrnament { Type = type, NoteIndex = 0 };
        }

        [Fact]
        public void ApplyNavigation_NoNavigationMarkers_ReturnsOrderUnchanged()
        {
            var measures = new List<JianpuMeasure> { Measure(), Measure(), Measure() };
            var order = new List<int> { 0, 1, 2 };

            var result = SegnoCodaPlaybackExpander.ApplyNavigation(measures, order);

            Assert.Equal(new[] { 0, 1, 2 }, result);
        }

        [Fact]
        public void ApplyNavigation_DaCapoWithNoFineOrCoda_ReplaysFromTheBeginningToTheEnd()
        {
            var measures = new List<JianpuMeasure>
            {
                Measure(),
                Measure(),
                Measure(Ornament(OrnamentType.DaCapo))
            };
            var order = new List<int> { 0, 1, 2 };

            var result = SegnoCodaPlaybackExpander.ApplyNavigation(measures, order);

            Assert.Equal(new[] { 0, 1, 2, 0, 1, 2 }, result);
        }

        [Fact]
        public void ApplyNavigation_DaCapoAlFine_StopsAtTheFineMeasure()
        {
            var measures = new List<JianpuMeasure>
            {
                Measure(),
                Measure(Ornament(OrnamentType.Fine)),
                Measure(),
                Measure(Ornament(OrnamentType.DaCapo))
            };
            var order = new List<int> { 0, 1, 2, 3 };

            var result = SegnoCodaPlaybackExpander.ApplyNavigation(measures, order);

            // Main pass plays 0-3, then D.C. jumps back to 0 and stops at the Fine measure (1).
            Assert.Equal(new[] { 0, 1, 2, 3, 0, 1 }, result);
        }

        [Fact]
        public void ApplyNavigation_DalSegnoAlCoda_JumpsToSegnoThenSkipsToTheSecondCoda()
        {
            var measures = new List<JianpuMeasure>
            {
                Measure(),
                Measure(Ornament(OrnamentType.Segno)),
                Measure(),
                Measure(Ornament(OrnamentType.Coda)), // jump-from point
                Measure(),
                Measure(Ornament(OrnamentType.DalSegno)),
                Measure(Ornament(OrnamentType.Coda)), // jump-to / coda section start
                Measure()
            };
            var order = new List<int> { 0, 1, 2, 3, 4, 5 };

            var result = SegnoCodaPlaybackExpander.ApplyNavigation(measures, order);

            // Main pass 0-5, then D.S. jumps to Segno (1), plays 1-2 (stopping before the
            // jump-from coda at 3), then jumps straight to the coda section (6-7).
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 1, 2, 6, 7 }, result);
        }

        [Fact]
        public void ApplyNavigation_DalSegnoTakesPrecedenceWhenItOccursAfterDaCapoInThePlayOrder()
        {
            // A malformed score with both markers -- whichever is actually reached last during
            // the main pass is the one that fires.
            var measures = new List<JianpuMeasure>
            {
                Measure(Ornament(OrnamentType.Segno)),
                Measure(Ornament(OrnamentType.DaCapo)),
                Measure(Ornament(OrnamentType.DalSegno))
            };
            var order = new List<int> { 0, 1, 2 };

            var result = SegnoCodaPlaybackExpander.ApplyNavigation(measures, order);

            // D.S. (at index 2, later in the order) wins over D.C. (index 1), jumping to Segno (0).
            Assert.Equal(new[] { 0, 1, 2, 0, 1, 2 }, result);
        }

        [Fact]
        public void ApplyNavigation_EmptyOrder_ReturnsEmpty()
        {
            var result = SegnoCodaPlaybackExpander.ApplyNavigation(new List<JianpuMeasure> { Measure() }, new List<int>());

            Assert.Empty(result);
        }
    }
}
