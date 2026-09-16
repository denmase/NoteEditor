using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace JianpuEditor.Views
{
    /// <summary>One tunable numeric field shown in <see cref="AudioImportSettingsDialog"/>.</summary>
    public sealed class ImportParameter
    {
        public ImportParameter(string label, decimal value, decimal minimum, decimal maximum, decimal increment, int decimalPlaces)
        {
            Label = label;
            Value = value;
            Minimum = minimum;
            Maximum = maximum;
            Increment = increment;
            DecimalPlaces = decimalPlaces;
        }

        public string Label { get; }

        public decimal Value { get; set; }

        public decimal Minimum { get; }

        public decimal Maximum { get; }

        public decimal Increment { get; }

        public int DecimalPlaces { get; }
    }

    /// <summary>
    /// A small "Advanced settings" prompt shown before each audio import so the user can tune
    /// the chosen engine's thresholds without a code change. Built generically from a list of
    /// <see cref="ImportParameter"/> rows rather than one dialog per engine, since both engines'
    /// settings are just a handful of labeled numeric ranges.
    /// </summary>
    public sealed class AudioImportSettingsDialog : Form
    {
        private readonly List<(ImportParameter Parameter, NumericUpDown Control)> _rows = new List<(ImportParameter, NumericUpDown)>();

        public AudioImportSettingsDialog(string title, IReadOnlyList<ImportParameter> parameters)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;

            const int rowHeight = 28;
            const int labelWidth = 220;
            const int fieldWidth = 100;
            const int margin = 16;
            var y = margin;

            foreach (var parameter in parameters)
            {
                var label = new Label
                {
                    Text = parameter.Label,
                    Location = new Point(margin, y + 3),
                    Size = new Size(labelWidth, 20),
                    AutoEllipsis = true
                };

                var input = new NumericUpDown
                {
                    Location = new Point(margin + labelWidth + 8, y),
                    Size = new Size(fieldWidth, 22),
                    Minimum = parameter.Minimum,
                    Maximum = parameter.Maximum,
                    Increment = parameter.Increment,
                    DecimalPlaces = parameter.DecimalPlaces,
                    Value = Clamp(parameter.Value, parameter.Minimum, parameter.Maximum)
                };

                Controls.Add(label);
                Controls.Add(input);
                _rows.Add((parameter, input));
                y += rowHeight;
            }

            var buttonY = y + margin / 2;
            var okButton = new Button
            {
                Text = "Import",
                DialogResult = DialogResult.OK,
                Location = new Point(margin + labelWidth + 8 - 160, buttonY),
                Size = new Size(75, 26)
            };
            var cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(margin + labelWidth + 8 - 80, buttonY),
                Size = new Size(75, 26)
            };

            Controls.Add(okButton);
            Controls.Add(cancelButton);
            AcceptButton = okButton;
            CancelButton = cancelButton;

            ClientSize = new Size(margin * 2 + labelWidth + 8 + fieldWidth, buttonY + 26 + margin);
        }

        /// <summary>Copies each control's current value back into its <see cref="ImportParameter"/>.
        /// Call after <see cref="Form.ShowDialog()"/> returns <see cref="DialogResult.OK"/>.</summary>
        public void ApplyValues()
        {
            foreach (var (parameter, control) in _rows)
            {
                parameter.Value = control.Value;
            }
        }

        private static decimal Clamp(decimal value, decimal min, decimal max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
