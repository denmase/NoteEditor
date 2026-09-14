using System.Drawing;
using JianpuEditor.Controls;
using Xunit;

namespace JianpuEditor.Tests.Controls
{
    public sealed class CanvasZoomTests
    {
        [Fact]
        public void DefaultScale_IsOne()
        {
            var zoom = new CanvasZoom();

            Assert.Equal(1.0, zoom.Scale);
            Assert.True(zoom.IsDefault);
        }

        [Fact]
        public void ToScreenAndToLogical_AreIdentity_AtDefaultScale()
        {
            var zoom = new CanvasZoom();

            Assert.Equal(140, zoom.ToScreen(140));
            Assert.Equal(140, zoom.ToLogical(140));
        }

        [Theory]
        [InlineData(1.5)]
        [InlineData(0.75)]
        [InlineData(2.0)]
        public void ToScreen_ThenToLogical_RoundTrips_ForTypicalCoordinates(double scale)
        {
            var zoom = new CanvasZoom { Scale = scale };

            foreach (var logical in new[] { 0, 1, 40, 133, 384, 900 })
            {
                var screen = zoom.ToScreen(logical);
                var roundTripped = zoom.ToLogical(screen);
                Assert.Equal(logical, roundTripped);
            }
        }

        [Fact]
        public void ToScreen_ScalesRectangle_InAllFourFields()
        {
            var zoom = new CanvasZoom { Scale = 1.5 };
            var logicalRect = new Rectangle(120, 80, 40, 40);

            var screenRect = zoom.ToScreen(logicalRect);

            Assert.Equal(new Rectangle(180, 120, 60, 60), screenRect);
        }

        [Fact]
        public void Scale_ClampsToMinAndMax()
        {
            var zoom = new CanvasZoom();

            zoom.Scale = 0.1;
            Assert.Equal(CanvasZoom.MinScale, zoom.Scale);

            zoom.Scale = 10.0;
            Assert.Equal(CanvasZoom.MaxScale, zoom.Scale);
        }

        [Fact]
        public void ZoomIn_ZoomOut_Reset_StepConsistently()
        {
            var zoom = new CanvasZoom();

            zoom.ZoomIn();
            var afterOneStep = zoom.Scale;
            Assert.True(afterOneStep > 1.0);

            zoom.ZoomOut();
            Assert.Equal(1.0, zoom.Scale, 3);

            zoom.ZoomIn();
            zoom.ZoomIn();
            Assert.True(zoom.Scale > afterOneStep);

            zoom.Reset();
            Assert.Equal(1.0, zoom.Scale);
            Assert.True(zoom.IsDefault);
        }

        [Fact]
        public void ZoomOut_RepeatedlyClampsAtMinScale_WithoutGoingLower()
        {
            var zoom = new CanvasZoom();

            for (var i = 0; i < 50; i++)
            {
                zoom.ZoomOut();
            }

            Assert.Equal(CanvasZoom.MinScale, zoom.Scale);
        }

        [Fact]
        public void ZoomIn_RepeatedlyClampsAtMaxScale_WithoutGoingHigher()
        {
            var zoom = new CanvasZoom();

            for (var i = 0; i < 50; i++)
            {
                zoom.ZoomIn();
            }

            Assert.Equal(CanvasZoom.MaxScale, zoom.Scale);
        }
    }
}
