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
        private int? _fixedHeight;

        /// <summary>This group's height if it sized itself purely from its own rows, ignoring any
        /// <see cref="SetFixedHeight"/> override -- what <see cref="MainForm"/> maxes across every
        /// group in the toolbar so their captions all land on one shared baseline instead of each
        /// group ending wherever its own row count happens to stop.</summary>
        public int NaturalHeight { get; private set; }

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

        /// <summary>Pins this group's height (and its caption to the bottom of it) so every group
        /// in the toolbar can share one height regardless of how many rows each holds. Pass a value
        /// no smaller than <see cref="NaturalHeight"/> -- this never clips rows short.</summary>
        public void SetFixedHeight(int height)
        {
            _fixedHeight = height;
            Relayout();
        }

        private void Relayout()
        {
            var contentWidth = 0;
            var rowsHeight = 0;
            foreach (var row in _rowPanels)
            {
                row.PerformLayout();
                contentWidth = System.Math.Max(contentWidth, row.PreferredSize.Width);
                rowsHeight += row.PreferredSize.Height;
            }

            if (_rowPanels.Count > 1)
            {
                rowsHeight += RowSpacing * (_rowPanels.Count - 1);
            }

            var width = contentWidth + SidePadding * 2;
            NaturalHeight = 4 + rowsHeight + RowSpacing + CaptionHeight + 2;
            var height = System.Math.Max(_fixedHeight ?? 0, NaturalHeight);

            // Rows are centered in the space above the caption (which always stays pinned to the
            // bottom), so a one-row group given a taller fixed height -- to match a two-row
            // neighbor -- doesn't leave its row stranded near the top with a dead gap below it.
            var contentAreaHeight = height - CaptionHeight - 2;
            var y = System.Math.Max(4, (contentAreaHeight - rowsHeight) / 2);
            foreach (var row in _rowPanels)
            {
                row.Location = new Point(SidePadding, y);
                row.Width = contentWidth;
                y += row.PreferredSize.Height + RowSpacing;
            }

            _captionLabel.Location = new Point(0, height - CaptionHeight - 2);
            _captionLabel.Size = new Size(width, CaptionHeight);

            Size = new Size(width, height);
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
