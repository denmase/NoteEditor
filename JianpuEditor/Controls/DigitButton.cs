using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using JianpuEditor.Rendering;

namespace JianpuEditor.Controls
{
    /// <summary>
    /// A note-entry button for jianpu digits 1-7 and the rest ("0"), styled distinctly from
    /// <see cref="RibbonButton"/> -- a bordered monospace digit, matching how the same character
    /// reads on the score itself -- since these are the single most-used control in the app.
    /// </summary>
    public sealed class DigitButton : Control
    {
        private const int DigitSize = 32;
        private bool _hovered;

        public DigitButton(string digit)
        {
            Digit = digit;
            SetStyle(
                ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable,
                true);
            Cursor = Cursors.Hand;
            Margin = new Padding(1);
            Size = new Size(DigitSize, DigitSize);
            AppTheme.ThemeChanged += OnThemeChanged;
        }

        public string Digit { get; }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var theme = AppTheme.Current;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

            using (var backBrush = new SolidBrush(theme.PaperRaised))
            {
                g.FillRectangle(backBrush, bounds);
            }

            var borderColor = _hovered ? theme.Accent : theme.LineStrong;
            using (var borderPen = new Pen(borderColor))
            {
                g.DrawRectangle(borderPen, bounds);
            }

            var textColor = _hovered ? theme.Accent : theme.Ink;
            using (var brush = new SolidBrush(textColor))
            using (var font = new Font("Consolas", 13f, FontStyle.Bold))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(Digit, font, brush, bounds, format);
            }
        }

        private void OnThemeChanged()
        {
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                AppTheme.ThemeChanged -= OnThemeChanged;
            }

            base.Dispose(disposing);
        }
    }
}
