using System.Drawing;
using System.Drawing.Drawing2D;

namespace JianpuEditor.Controls
{
    /// <summary>
    /// Draws every <see cref="RibbonIcon"/> as hand-built GDI+ vector shapes, matching this
    /// codebase's existing approach (JianpuRenderer draws its own glyphs rather than shipping
    /// image assets) instead of adding an icon-file dependency. Every icon is authored in a fixed
    /// 20x20 virtual coordinate space -- the same one the UI redesign pitch's SVG icons used --
    /// and <see cref="Draw"/> scales that space onto whatever pixel rectangle the caller gives it,
    /// so one definition serves every DPI/zoom level.
    /// </summary>
    public static class RibbonIconRenderer
    {
        private const float Space = 20f;

        public static void Draw(Graphics g, RibbonIcon icon, Rectangle bounds, Color color, float strokeWidth = 1.6f)
        {
            if (icon == RibbonIcon.None || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            var state = g.Save();
            var oldSmoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(bounds.X, bounds.Y);
            g.ScaleTransform(bounds.Width / Space, bounds.Height / Space);

            using (var pen = new Pen(color, strokeWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var brush = new SolidBrush(color))
            {
                DrawIcon(g, icon, pen, brush);
            }

            g.SmoothingMode = oldSmoothing;
            g.Restore(state);
        }

        private static void DrawIcon(Graphics g, RibbonIcon icon, Pen pen, Brush brush)
        {
            switch (icon)
            {
                case RibbonIcon.Play:
                    g.FillPolygon(brush, new[] { new PointF(6.5f, 4f), new PointF(16f, 10f), new PointF(6.5f, 16f) });
                    return;
                case RibbonIcon.Stop:
                    g.FillRectangle(brush, 5.5f, 5.5f, 9f, 9f);
                    return;
                case RibbonIcon.Instrument:
                    g.DrawArc(pen, 4f, 5f, 12f, 15f, 195f, 150f);
                    g.DrawEllipse(pen, 3.4f, 12.2f, 3.6f, 3.6f);
                    g.DrawEllipse(pen, 13f, 12.2f, 3.6f, 3.6f);
                    return;
                case RibbonIcon.MeasureAdd:
                    g.DrawLine(pen, 5f, 3f, 5f, 17f);
                    g.DrawLine(pen, 15f, 3f, 15f, 17f);
                    g.DrawLine(pen, 10f, 7.5f, 10f, 12.5f);
                    g.DrawLine(pen, 7.5f, 10f, 12.5f, 10f);
                    return;
                case RibbonIcon.OctaveUp:
                    g.FillEllipse(brush, 8.6f, 2.6f, 2.8f, 2.8f);
                    DrawChevron(g, pen, 10f, 8f, 4f, up: true);
                    return;
                case RibbonIcon.OctaveDown:
                    g.FillEllipse(brush, 8.6f, 14.6f, 2.8f, 2.8f);
                    DrawChevron(g, pen, 10f, 10f, 4f, up: false);
                    return;
                case RibbonIcon.TransposeUp:
                    g.DrawLine(pen, 10f, 16f, 10f, 4f);
                    DrawChevron(g, pen, 10f, 3f, 5f, up: true);
                    return;
                case RibbonIcon.TransposeDown:
                    g.DrawLine(pen, 10f, 4f, 10f, 16f);
                    DrawChevron(g, pen, 10f, 12f, 5f, up: false);
                    return;
                case RibbonIcon.Split:
                    g.DrawLine(pen, 10f, 3f, 10f, 8.5f);
                    g.DrawLine(pen, 8.8f, 11f, 6f, 16f);
                    g.DrawLine(pen, 11.2f, 11f, 14f, 16f);
                    g.FillEllipse(brush, 8.8f, 9.2f, 2.6f, 2.6f);
                    return;
                case RibbonIcon.Merge:
                    g.DrawLine(pen, 6f, 4f, 8.8f, 9.2f);
                    g.DrawLine(pen, 14f, 4f, 11.2f, 9.2f);
                    g.DrawLine(pen, 10f, 11.5f, 10f, 16f);
                    g.FillEllipse(brush, 8.8f, 9.2f, 2.6f, 2.6f);
                    return;
                case RibbonIcon.Dotted:
                    using (var thick = new Pen(pen.Color, 2.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        g.DrawLine(thick, 7.5f, 6f, 7.5f, 14f);
                    }

                    g.FillEllipse(brush, 12f, 12.5f, 3f, 3f);
                    return;
                case RibbonIcon.Extend:
                    g.DrawLine(pen, 3f, 15f, 17f, 15f);
                    g.DrawLine(pen, 13f, 5f, 13f, 11f);
                    g.DrawLine(pen, 10f, 8f, 16f, 8f);
                    return;
                case RibbonIcon.Shorten:
                    g.DrawLine(pen, 3f, 15f, 9f, 15f);
                    g.DrawLine(pen, 13f, 8f, 17f, 8f);
                    return;
                case RibbonIcon.Tie:
                    g.FillEllipse(brush, 4.4f, 12.4f, 3.2f, 3.2f);
                    g.FillEllipse(brush, 12.4f, 12.4f, 3.2f, 3.2f);
                    g.DrawBezier(pen, 4f, 9f, 7f, 4.5f, 13f, 4.5f, 16f, 9f);
                    return;
                case RibbonIcon.Grace:
                    g.FillEllipse(brush, 10.4f, 10.4f, 5.2f, 5.2f);
                    g.DrawLine(pen, 15.4f, 12f, 15.4f, 4f);
                    g.DrawLine(pen, 15.4f, 4f, 18.4f, 3f);
                    using (var faint = new SolidBrush(Color.FromArgb(140, pen.Color)))
                    {
                        g.FillEllipse(faint, 5f, 14f, 2f, 2f);
                    }

                    return;
                case RibbonIcon.Trill:
                    using (var path = new GraphicsPath())
                    {
                        path.AddBezier(3f, 12f, 4.5f, 8f, 6f, 8f, 7.5f, 12f);
                        path.AddBezier(7.5f, 12f, 9f, 16f, 10.5f, 16f, 12f, 12f);
                        path.AddBezier(12f, 12f, 13.5f, 8f, 15f, 8f, 16.5f, 12f);
                        g.DrawPath(pen, path);
                    }

                    return;
                case RibbonIcon.Turn:
                    g.DrawArc(pen, 4f, 8.6f, 8f, 6.8f, 180f, 180f);
                    g.DrawArc(pen, 12f, 8.6f, 8f, 6.8f, 0f, -180f);
                    return;
                case RibbonIcon.Mordent:
                    g.DrawLine(pen, 3f, 12f, 7f, 6f);
                    g.DrawLine(pen, 7f, 6f, 11f, 12f);
                    g.DrawLine(pen, 11f, 12f, 15f, 6f);
                    g.DrawLine(pen, 15f, 6f, 17f, 9f);
                    g.DrawLine(pen, 10f, 3f, 10f, 15f);
                    return;
                case RibbonIcon.Fermata:
                    g.DrawArc(pen, 3f, 6f, 14f, 12f, 180f, 180f);
                    g.FillEllipse(brush, 8.8f, 9.4f, 2.4f, 2.4f);
                    return;
                case RibbonIcon.BreathMark:
                    using (var breathPen = new Pen(pen.Color, pen.Width * 1.4f))
                    {
                        g.DrawLine(breathPen, 6f, 4f, 13f, 15f);
                    }

                    return;
                case RibbonIcon.Staccato:
                    g.FillEllipse(brush, 8f, 8f, 4f, 4f);
                    return;
                case RibbonIcon.Accent:
                    g.DrawLine(pen, 4f, 5f, 14f, 10f);
                    g.DrawLine(pen, 14f, 10f, 4f, 15f);
                    return;
                case RibbonIcon.Tenuto:
                    using (var tenutoPen = new Pen(pen.Color, pen.Width * 1.6f))
                    {
                        g.DrawLine(tenutoPen, 4f, 10f, 16f, 10f);
                    }

                    return;
                case RibbonIcon.Duplicate:
                    g.DrawRectangle(pen, 3f, 6f, 10f, 10f);
                    g.DrawLine(pen, 7f, 6f, 7f, 4f);
                    g.DrawLine(pen, 7f, 4f, 15f, 4f);
                    g.DrawLine(pen, 15f, 4f, 15f, 12f);
                    g.DrawLine(pen, 15f, 12f, 13f, 12f);
                    return;
                case RibbonIcon.Delete:
                    g.DrawLine(pen, 4f, 6f, 16f, 6f);
                    g.DrawLine(pen, 8f, 6f, 8f, 4f);
                    g.DrawLine(pen, 8f, 4f, 12f, 4f);
                    g.DrawLine(pen, 12f, 4f, 12f, 6f);
                    g.DrawLine(pen, 6f, 6f, 7f, 16f);
                    g.DrawLine(pen, 7f, 16f, 13f, 16f);
                    g.DrawLine(pen, 13f, 16f, 14f, 6f);
                    return;
                case RibbonIcon.Library:
                    using (var path = new GraphicsPath())
                    {
                        path.AddLine(4f, 15f, 4f, 5f);
                        path.AddArc(4f, 4f, 2f, 2f, 180f, 90f);
                        path.AddLine(5f, 4f, 9f, 4f);
                        path.AddLine(9f, 4f, 11f, 6f);
                        path.AddLine(11f, 6f, 15f, 6f);
                        path.AddArc(14f, 6f, 2f, 2f, 270f, 90f);
                        path.AddLine(16f, 7f, 16f, 14f);
                        path.AddArc(14f, 14f, 2f, 2f, 0f, 90f);
                        path.AddLine(15f, 15f, 5f, 15f);
                        path.AddArc(4f, 14f, 2f, 2f, 90f, 90f);
                        path.CloseFigure();
                        g.DrawPath(pen, path);
                    }

                    return;
                case RibbonIcon.Key:
                    g.DrawLine(pen, 7f, 4f, 7f, 15f);
                    g.DrawLine(pen, 11.5f, 4f, 11.5f, 15f);
                    g.DrawLine(pen, 4.5f, 9f, 7f, 8.2f);
                    g.DrawLine(pen, 4.5f, 12f, 7f, 11.2f);
                    g.DrawLine(pen, 11.5f, 8.2f, 14f, 9f);
                    g.DrawLine(pen, 11.5f, 11.2f, 14f, 12f);
                    return;
                case RibbonIcon.Tempo:
                    g.DrawPolygon(pen, new[] { new PointF(7f, 16f), new PointF(9.5f, 3f), new PointF(12f, 16f) });
                    g.DrawLine(pen, 8f, 12.5f, 12.5f, 6f);
                    return;
                case RibbonIcon.Measures:
                    g.DrawLine(pen, 4f, 4f, 4f, 16f);
                    g.DrawLine(pen, 10f, 4f, 10f, 16f);
                    g.DrawLine(pen, 16f, 4f, 16f, 16f);
                    return;
                case RibbonIcon.Engine:
                    g.DrawRectangle(pen, 6f, 6f, 8f, 8f);
                    g.DrawLine(pen, 9f, 6f, 9f, 3f);
                    g.DrawLine(pen, 12f, 6f, 12f, 3f);
                    g.DrawLine(pen, 9f, 17f, 9f, 14f);
                    g.DrawLine(pen, 12f, 17f, 12f, 14f);
                    g.DrawLine(pen, 3f, 9f, 6f, 9f);
                    g.DrawLine(pen, 3f, 12f, 6f, 12f);
                    g.DrawLine(pen, 17f, 9f, 14f, 9f);
                    g.DrawLine(pen, 17f, 12f, 14f, 12f);
                    return;
                case RibbonIcon.ZoomIn:
                    g.DrawEllipse(pen, 3.5f, 3.5f, 9f, 9f);
                    g.DrawLine(pen, 12.5f, 12.5f, 17f, 17f);
                    g.DrawLine(pen, 8f, 5.5f, 8f, 10.5f);
                    g.DrawLine(pen, 5.5f, 8f, 10.5f, 8f);
                    return;
                case RibbonIcon.ZoomOut:
                    g.DrawEllipse(pen, 3.5f, 3.5f, 9f, 9f);
                    g.DrawLine(pen, 12.5f, 12.5f, 17f, 17f);
                    g.DrawLine(pen, 5.5f, 8f, 10.5f, 8f);
                    return;
            }
        }

        private static void DrawChevron(Graphics g, Pen pen, float centerX, float topY, float width, bool up)
        {
            var half = width / 2f;
            var bottomY = topY + width * 0.7f;
            if (up)
            {
                g.DrawLine(pen, centerX - half, bottomY, centerX, topY);
                g.DrawLine(pen, centerX, topY, centerX + half, bottomY);
            }
            else
            {
                g.DrawLine(pen, centerX - half, topY, centerX, bottomY);
                g.DrawLine(pen, centerX, bottomY, centerX + half, topY);
            }
        }
    }
}
