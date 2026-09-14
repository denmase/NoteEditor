using System;
using System.Drawing;

namespace JianpuEditor.Controls
{
    /// <summary>
    /// Converts between "logical" score-layout coordinates -- what JianpuRenderer/HitTest/
    /// PlaybackLayout/ChordMarkerLayout all work in, unaffected by zoom -- and "screen" pixels on
    /// the actual WinForms canvas surface. ScoreCanvas renders the score once at logical
    /// resolution and blits it through a Graphics.ScaleTransform, so this class exists to keep
    /// that one multiply/divide-by-scale relationship in a single, independently testable place
    /// instead of scattered across every mouse handler and overlay-positioning call site.
    /// </summary>
    public sealed class CanvasZoom
    {
        public const double MinScale = 0.5;
        public const double MaxScale = 2.5;
        public const double DefaultScale = 1.0;
        private const double Step = 0.25;

        private double _scale = DefaultScale;

        public double Scale
        {
            get { return _scale; }
            set { _scale = Clamp(value); }
        }

        public bool IsDefault
        {
            get { return Math.Abs(_scale - DefaultScale) < 0.001; }
        }

        public void ZoomIn()
        {
            Scale = _scale + Step;
        }

        public void ZoomOut()
        {
            Scale = _scale - Step;
        }

        public void Reset()
        {
            Scale = DefaultScale;
        }

        public int ToScreen(int logical)
        {
            return (int)Math.Round(logical * _scale);
        }

        public int ToLogical(int screen)
        {
            return (int)Math.Round(screen / _scale);
        }

        public Point ToLogical(Point screenPoint)
        {
            return new Point(ToLogical(screenPoint.X), ToLogical(screenPoint.Y));
        }

        public Point ToScreen(Point logicalPoint)
        {
            return new Point(ToScreen(logicalPoint.X), ToScreen(logicalPoint.Y));
        }

        public Size ToScreen(Size logicalSize)
        {
            return new Size(ToScreen(logicalSize.Width), ToScreen(logicalSize.Height));
        }

        public Rectangle ToScreen(Rectangle logicalRect)
        {
            return new Rectangle(
                ToScreen(logicalRect.X),
                ToScreen(logicalRect.Y),
                ToScreen(logicalRect.Width),
                ToScreen(logicalRect.Height));
        }

        private static double Clamp(double value)
        {
            return Math.Max(MinScale, Math.Min(MaxScale, value));
        }
    }
}
