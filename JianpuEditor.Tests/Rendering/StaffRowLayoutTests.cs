using JianpuEditor.Rendering;
using Xunit;

namespace JianpuEditor.Tests.Rendering
{
    /// <summary>Locks in the row-stacking invariant added for the dynamics row: each row's top must
    /// sit exactly one RowGap below the row above it's bottom edge, and the sum of every row plus
    /// its gaps must equal StaffBlockHeight -- otherwise rows would overlap or StaffBlockHeight
    /// (used everywhere: block bounds, hit-testing, PDF pagination content height, block stacking)
    /// would silently drift out of sync with what's actually drawn.</summary>
    public sealed class StaffRowLayoutTests
    {
        [Fact]
        public void RowTops_StackWithoutOverlapOrGapDrift()
        {
            var measure = new JianpuRenderer.MeasureLayout { BlockTop = 100, X = 0, Width = 200 };

            var melodyBottom = measure.BlockTop + JianpuRenderer.MelodyRowHeight;
            var dynamicsTop = JianpuRenderer.GetDynamicsRowTop(measure);
            var dynamicsBottom = dynamicsTop + JianpuRenderer.DynamicsRowHeight;
            var secondaryTop = JianpuRenderer.GetSecondaryRowTop(measure);
            var secondaryBottom = secondaryTop + JianpuRenderer.SecondaryRowHeight;
            var lyricTop = JianpuRenderer.GetLyricRowTop(measure);
            var lyricBottom = lyricTop + JianpuRenderer.TextRowHeight;

            Assert.Equal(melodyBottom + JianpuRenderer.RowGap, dynamicsTop);
            Assert.Equal(dynamicsBottom + JianpuRenderer.RowGap, secondaryTop);
            Assert.Equal(secondaryBottom + JianpuRenderer.RowGap, lyricTop);
            Assert.Equal(measure.BlockTop + JianpuRenderer.StaffBlockHeight, lyricBottom);
        }
    }
}
