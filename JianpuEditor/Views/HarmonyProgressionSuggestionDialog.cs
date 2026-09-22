using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using JianpuEditor.Models;

namespace JianpuEditor.Views
{
    public sealed class HarmonyProgressionSuggestionDialog : Form
    {
        private readonly Func<HarmonySuggestionEngineKind, IReadOnlyList<HarmonyProgressionSuggestion>> _reloadSuggestions;
        private readonly ListBox _suggestionList;
        private IReadOnlyList<HarmonyProgressionSuggestion> _suggestions;

        public HarmonyProgressionSuggestionDialog(
            int fromMeasureNumber,
            int toMeasureNumber,
            string keySignature,
            IReadOnlyList<HarmonyProgressionSuggestion> suggestions,
            bool supportsEngineSelection = false,
            HarmonySuggestionEngineKind initialEngineKind = HarmonySuggestionEngineKind.Legacy,
            Func<HarmonySuggestionEngineKind, IReadOnlyList<HarmonyProgressionSuggestion>> reloadSuggestions = null)
        {
            _suggestions = suggestions ?? Array.Empty<HarmonyProgressionSuggestion>();
            _reloadSuggestions = reloadSuggestions;

            Text = "Chord Progression Suggestions";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;

            var y = 16;
            var contextLabel = new Label
            {
                Location = new Point(16, y),
                Size = new Size(488, 36),
                Text = "Measures " + fromMeasureNumber + "–" + toMeasureNumber + ", key " + (keySignature ?? "1=C")
            };
            Controls.Add(contextLabel);
            y += 36;

            ComboBox engineCombo = null;
            if (supportsEngineSelection && _reloadSuggestions != null)
            {
                var engineLabel = new Label
                {
                    Location = new Point(16, y + 3),
                    Size = new Size(50, 20),
                    Text = "Engine:"
                };
                Controls.Add(engineLabel);

                engineCombo = new ComboBox
                {
                    Location = new Point(70, y),
                    Size = new Size(220, 22),
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                engineCombo.Items.Add("Legacy (diatonic)");
                engineCombo.Items.Add("Enhanced (Markov)");
                engineCombo.SelectedIndex = initialEngineKind == HarmonySuggestionEngineKind.Markov ? 1 : 0;
                engineCombo.SelectedIndexChanged += (s, e) =>
                {
                    var kind = engineCombo.SelectedIndex == 1 ? HarmonySuggestionEngineKind.Markov : HarmonySuggestionEngineKind.Legacy;
                    _suggestions = _reloadSuggestions(kind) ?? Array.Empty<HarmonyProgressionSuggestion>();
                    PopulateSuggestionList();
                };
                Controls.Add(engineCombo);
                y += 26;
            }

            var hintLabel = new Label
            {
                Location = new Point(16, y),
                Size = new Size(488, 32),
                ForeColor = Color.DimGray,
                Text = "Based on the melody's bass line and harmonic movement, suggests 1–3 continuous chord progressions."
            };
            Controls.Add(hintLabel);
            y += 38;

            _suggestionList = new ListBox
            {
                Location = new Point(16, y),
                Size = new Size(488, 196),
                IntegralHeight = false
            };
            _suggestionList.DoubleClick += (s, e) =>
            {
                if (_suggestionList.SelectedIndex >= 0)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };
            Controls.Add(_suggestionList);
            y += 196 + 16;

            PopulateSuggestionList();

            var applyButton = new Button
            {
                Text = "Apply to Measures",
                DialogResult = DialogResult.OK,
                Location = new Point(300, y),
                Width = 110
            };
            var cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(424, y),
                Width = 80
            };
            Controls.Add(applyButton);
            Controls.Add(cancelButton);
            AcceptButton = applyButton;
            CancelButton = cancelButton;

            ClientSize = new Size(520, y + 16 + applyButton.Height);
        }

        private void PopulateSuggestionList()
        {
            _suggestionList.Items.Clear();
            foreach (var suggestion in _suggestions)
            {
                var symbols = string.Join(" - ", suggestion.Steps.Select(step => step.ChordSymbol));
                _suggestionList.Items.Add(
                    suggestion.Label + "  |  " + symbols + "  |  " + suggestion.BassLineSummary + "  |  " + suggestion.Reason);
            }

            if (_suggestionList.Items.Count > 0)
            {
                _suggestionList.SelectedIndex = 0;
            }
        }

        public HarmonyProgressionSuggestion SelectedSuggestion
        {
            get
            {
                if (_suggestionList.SelectedIndex < 0 || _suggestionList.SelectedIndex >= _suggestions.Count)
                {
                    return null;
                }

                return _suggestions[_suggestionList.SelectedIndex];
            }
        }
    }
}
