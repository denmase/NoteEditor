using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using JianpuEditor.Rendering;

namespace JianpuEditor.Controls
{
    /// <summary>
    /// A toolbar command button: an icon, an optional caption below it, and hover/active/disabled
    /// states drawn from <see cref="AppTheme"/> so it re-themes without any external wiring. Used
    /// in place of a plain <see cref="Button"/> for every command in the ribbon toolbar.
    /// </summary>
    public sealed class RibbonButton : Control
    {
        private const int LabeledWidth = 56;
        private const int LabeledHeight = 46;
        private const int CompactSize = 32;

        private readonly ToolTip _toolTip = new ToolTip();
        private bool _hovered;
        private bool _isActive;

        public RibbonButton(RibbonIcon icon, string caption, bool compact = false)
        {
            Icon = icon;
            Caption = caption ?? string.Empty;
            Compact = compact;
            SetStyle(
                ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable,
                true);
            Cursor = Cursors.Hand;
            Margin = new Padding(1);
            Size = compact ? new Size(CompactSize, CompactSize) : new Size(LabeledWidth, LabeledHeight);
            if (!string.IsNullOrEmpty(caption))
            {
                _toolTip.SetToolTip(this, caption);
            }

            AppTheme.ThemeChanged += OnThemeChanged;
        }

        public RibbonIcon Icon { get; }

        public string Caption { get; }

        public bool Compact { get; }

        /// <summary>True while the command this button represents is toggled on (e.g. tie-entry
        /// mode) -- paints a solid accent fill instead of the usual hover/idle look.</summary>
        public bool IsActive
        {
            get { return _isActive; }
            set
            {
                if (_isActive == value)
                {
                    return;
                }

                _isActive = value;
                Invalidate();
            }
        }

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

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var theme = AppTheme.Current;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

            var background = IsActive ? theme.Accent : _hovered && Enabled ? theme.AccentWash : theme.PaperRaised;
            using (var brush = new SolidBrush(background))
            {
                g.FillRectangle(brush, bounds);
            }

            var foreground = !Enabled
                ? theme.InkFaint
                : IsActive ? theme.PaperRaised : theme.Ink;

            var iconSize = Compact ? Math.Min(Width, Height) - 8 : 18;
            var iconRect = new Rectangle((Width - iconSize) / 2, Compact ? (Height - iconSize) / 2 : 5, iconSize, iconSize);
            RibbonIconRenderer.Draw(g, Icon, iconRect, foreground);

            if (!Compact && !string.IsNullOrEmpty(Caption))
            {
                var labelColor = !Enabled ? theme.InkFaint : IsActive ? theme.PaperRaised : theme.InkSoft;
                var labelRect = new Rectangle(1, iconRect.Bottom + 2, Width - 2, Height - iconRect.Bottom - 3);
                using (var labelBrush = new SolidBrush(labelColor))
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisCharacter })
                using (var font = new Font("Segoe UI", 7f))
                {
                    g.DrawString(Caption, font, labelBrush, labelRect, format);
                }
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
                _toolTip.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
