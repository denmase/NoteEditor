using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using JianpuEditor.Rendering;

namespace JianpuEditor.Controls
{
    /// <summary>
    /// Replaces the single status <see cref="Label"/> with a segmented bar reporting score state
    /// the app already tracks but didn't surface: key signature, tempo, current measure, audio
    /// engine, and a real zoom control -- alongside the existing free-text status message.
    /// </summary>
    public sealed class ScoreStatusBar : Panel
    {
        private const int SegmentHeight = 26;

        private readonly Label _messageLabel;
        private readonly IconSegment _keySegment;
        private readonly IconSegment _tempoSegment;
        private readonly IconSegment _measureSegment;
        private readonly IconSegment _engineSegment;
        private readonly ZoomSegment _zoomSegment;

        public ScoreStatusBar()
        {
            Height = SegmentHeight;
            Dock = DockStyle.Fill;
            MinimumSize = new Size(0, SegmentHeight);

            _messageLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 8, 0),
                Font = new Font("Segoe UI", 8.5f)
            };
            Controls.Add(_messageLabel);

            _zoomSegment = new ZoomSegment();
            _zoomSegment.Dock = DockStyle.Right;
            Controls.Add(_zoomSegment);

            _engineSegment = new IconSegment(RibbonIcon.Engine) { Cursor = Cursors.Hand };
            _engineSegment.Dock = DockStyle.Right;
            Controls.Add(_engineSegment);

            _measureSegment = new IconSegment(RibbonIcon.Measures);
            _measureSegment.Dock = DockStyle.Right;
            Controls.Add(_measureSegment);

            _tempoSegment = new IconSegment(RibbonIcon.Tempo);
            _tempoSegment.Dock = DockStyle.Right;
            Controls.Add(_tempoSegment);

            _keySegment = new IconSegment(RibbonIcon.Key);
            _keySegment.Dock = DockStyle.Right;
            Controls.Add(_keySegment);

            AppTheme.ThemeChanged += OnThemeChanged;
            ApplyThemeColors();
        }

        public event Action ZoomInClicked
        {
            add { _zoomSegment.ZoomInClicked += value; }
            remove { _zoomSegment.ZoomInClicked -= value; }
        }

        public event Action ZoomOutClicked
        {
            add { _zoomSegment.ZoomOutClicked += value; }
            remove { _zoomSegment.ZoomOutClicked -= value; }
        }

        /// <summary>Raised when the engine segment is clicked (mirrors the old toolbar engine
        /// label, which opened Edit -&gt; Audio Engine... on click).</summary>
        public event EventHandler EngineClicked
        {
            add { _engineSegment.Click += value; }
            remove { _engineSegment.Click -= value; }
        }

        public void SetMessage(string message)
        {
            _messageLabel.Text = message ?? string.Empty;
        }

        public void SetKeySignature(string key)
        {
            _keySegment.Text = key ?? string.Empty;
        }

        public void SetTempo(string tempo)
        {
            _tempoSegment.Text = tempo ?? string.Empty;
        }

        /// <summary><paramref name="beatsWarning"/> is appended when the current measure's
        /// note content doesn't add up to what the score's time signature implies, e.g.
        /// "5.00/4.00 beats" -- null or empty shows the plain measure count.</summary>
        public void SetMeasure(int current, int total, string beatsWarning = null)
        {
            if (total <= 0)
            {
                _measureSegment.Text = string.Empty;
                return;
            }

            var text = "Measure " + current + " / " + total;
            if (!string.IsNullOrEmpty(beatsWarning))
            {
                text += "  ⚠ " + beatsWarning;
            }

            _measureSegment.Text = text;
        }

        public void SetEngine(string engineName)
        {
            _engineSegment.Text = engineName ?? string.Empty;
        }

        public void SetZoomPercent(int percent)
        {
            _zoomSegment.Percent = percent;
        }

        private void ApplyThemeColors()
        {
            var theme = AppTheme.Current;
            BackColor = theme.PaperRaised;
            _messageLabel.ForeColor = theme.InkSoft;
            _messageLabel.BackColor = theme.PaperRaised;
        }

        private void OnThemeChanged()
        {
            ApplyThemeColors();
            Invalidate(true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(AppTheme.Current.Line))
            {
                e.Graphics.DrawLine(pen, 0, 0, Width, 0);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                AppTheme.ThemeChanged -= OnThemeChanged;
            }

            base.Dispose(disposing);
        }

        /// <summary>One "icon + text" segment with a left divider, e.g. "🔑 C major".</summary>
        private sealed class IconSegment : Control
        {
            private readonly RibbonIcon _icon;
            private string _text = string.Empty;

            public IconSegment(RibbonIcon icon)
            {
                _icon = icon;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                Width = 1;
                Height = SegmentHeight;
                AppTheme.ThemeChanged += OnThemeChanged;
            }

            public new string Text
            {
                get { return _text; }
                set
                {
                    _text = value ?? string.Empty;
                    Visible = _text.Length > 0;
                    using (var g = CreateGraphics())
                    using (var font = new Font("Consolas", 8f))
                    {
                        var textWidth = _text.Length == 0 ? 0 : (int)g.MeasureString(_text, font).Width;
                        Width = _text.Length == 0 ? 0 : 18 + textWidth + 20;
                    }

                    Invalidate();
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var theme = AppTheme.Current;

                using (var pen = new Pen(theme.Line))
                {
                    g.DrawLine(pen, 0, 4, 0, Height - 4);
                }

                if (_text.Length == 0)
                {
                    return;
                }

                RibbonIconRenderer.Draw(g, _icon, new Rectangle(10, (Height - 13) / 2, 13, 13), theme.InkFaint, 1.4f);
                using (var brush = new SolidBrush(theme.InkSoft))
                using (var font = new Font("Consolas", 8f))
                using (var format = new StringFormat { LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(_text, font, brush, new RectangleF(28, 0, Width - 28, Height), format);
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

        /// <summary>The "- 100% +" zoom control segment.</summary>
        private sealed class ZoomSegment : Control
        {
            private const int ButtonSize = 16;
            private int _percent = 100;
            private bool _zoomInHovered;
            private bool _zoomOutHovered;

            public ZoomSegment()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                Width = 96;
                Height = SegmentHeight;
                AppTheme.ThemeChanged += OnThemeChanged;
            }

            public event Action ZoomInClicked;

            public event Action ZoomOutClicked;

            public int Percent
            {
                get { return _percent; }
                set
                {
                    _percent = value;
                    Invalidate();
                }
            }

            private Rectangle ZoomOutBounds
            {
                get { return new Rectangle(14, (Height - ButtonSize) / 2, ButtonSize, ButtonSize); }
            }

            private Rectangle ZoomInBounds
            {
                get { return new Rectangle(Width - ButtonSize - 8, (Height - ButtonSize) / 2, ButtonSize, ButtonSize); }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                var overIn = ZoomInBounds.Contains(e.Location);
                var overOut = ZoomOutBounds.Contains(e.Location);
                if (overIn != _zoomInHovered || overOut != _zoomOutHovered)
                {
                    _zoomInHovered = overIn;
                    _zoomOutHovered = overOut;
                    Cursor = overIn || overOut ? Cursors.Hand : Cursors.Default;
                    Invalidate();
                }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _zoomInHovered = false;
                _zoomOutHovered = false;
                Invalidate();
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                if (ZoomInBounds.Contains(e.Location))
                {
                    ZoomInClicked?.Invoke();
                }
                else if (ZoomOutBounds.Contains(e.Location))
                {
                    ZoomOutClicked?.Invoke();
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var theme = AppTheme.Current;

                using (var pen = new Pen(theme.Line))
                {
                    g.DrawLine(pen, 0, 4, 0, Height - 4);
                }

                DrawZoomButton(g, ZoomOutBounds, RibbonIcon.ZoomOut, _zoomOutHovered, theme);
                DrawZoomButton(g, ZoomInBounds, RibbonIcon.ZoomIn, _zoomInHovered, theme);

                using (var brush = new SolidBrush(theme.InkSoft))
                using (var font = new Font("Consolas", 8f))
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    var textRect = new RectangleF(ZoomOutBounds.Right, 0, ZoomInBounds.Left - ZoomOutBounds.Right, Height);
                    g.DrawString(_percent + "%", font, brush, textRect, format);
                }
            }

            private static void DrawZoomButton(Graphics g, Rectangle bounds, RibbonIcon icon, bool hovered, Theme theme)
            {
                if (hovered)
                {
                    using (var brush = new SolidBrush(theme.AccentWash))
                    {
                        g.FillEllipse(brush, bounds);
                    }
                }

                var inset = Rectangle.Inflate(bounds, -2, -2);
                RibbonIconRenderer.Draw(g, icon, inset, hovered ? theme.Accent : theme.InkFaint, 1.4f);
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
}
