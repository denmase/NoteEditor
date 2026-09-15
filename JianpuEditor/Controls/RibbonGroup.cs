using System.Drawing;
using System.Windows.Forms;
using JianpuEditor.Rendering;

namespace JianpuEditor.Controls
{
    /// <summary>
    /// One labeled, right-divided section of the ribbon toolbar (Playback, Notes, Modify, ...).
    /// Buttons are added a row at a time via <see cref="AddRow"/>, so a group can hold either one
    /// wide row (Playback's Play/Stop/Instruments) or several short rows of compact buttons
    /// (Modify's two rows of five). Sizes itself explicitly rather than relying on
    /// <c>Panel.AutoSize</c>, which does not compute a preferred size from docked/positioned
    /// children the way <c>FlowLayoutPanel</c> does.
    /// </summary>
    public sealed class RibbonGroup : Panel
    {
        private const int CaptionHeight = 14;
        private const int RowSpacing = 3;
        private const int SidePadding = 10;

        private readonly Label _captionLabel;
        private readonly System.Collections.Generic.List<FlowLayoutPanel> _rowPanels
            = new System.Collections.Generic.List<FlowLayoutPanel>();

        public RibbonGroup(string caption)
        {
            Margin = new Padding(0);

            _captionLabel = new Label
            {
                Text = caption.ToUpperInvariant(),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 6.5f)
            };
            Controls.Add(_captionLabel);

            AppTheme.ThemeChanged += OnThemeChanged;
            ApplyThemeColors();
        }

        public void AddRow(params Control[] controls)
        {
            var row = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            foreach (var control in controls)
            {
                row.Controls.Add(control);
            }

            Controls.Add(row);
            _rowPanels.Add(row);
            Relayout();
        }

        private void Relayout()
        {
            var contentWidth = 0;
            var y = 4;
            foreach (var row in _rowPanels)
            {
                row.Location = new Point(SidePadding, y);
                row.PerformLayout();
                contentWidth = System.Math.Max(contentWidth, row.PreferredSize.Width);
                y += row.PreferredSize.Height + RowSpacing;
            }

            var width = contentWidth + SidePadding * 2;
            _captionLabel.Location = new Point(0, y);
            _captionLabel.Size = new Size(width, CaptionHeight);
            foreach (var row in _rowPanels)
            {
                row.Width = contentWidth;
            }

            Size = new Size(width, y + CaptionHeight + 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(AppTheme.Current.Line))
            {
                e.Graphics.DrawLine(pen, Width - 1, 0, Width - 1, Height);
            }
        }

        private void ApplyThemeColors()
        {
            var theme = AppTheme.Current;
            BackColor = theme.PaperRaised;
            _captionLabel.ForeColor = theme.InkFaint;
            _captionLabel.BackColor = Color.Transparent;
        }

        private void OnThemeChanged()
        {
            ApplyThemeColors();
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
