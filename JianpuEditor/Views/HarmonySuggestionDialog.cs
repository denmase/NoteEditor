using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using JianpuEditor.Models;

namespace JianpuEditor.Views
{
    public sealed class HarmonySuggestionDialog : Form
    {
        private readonly Func<HarmonySuggestionEngineKind, IReadOnlyList<HarmonySuggestion>> _reloadSuggestions;
        private readonly ListBox _suggestionList;
        private IReadOnlyList<HarmonySuggestion> _suggestions;

        public HarmonySuggestionDialog(
            int measureNumber,
            double beatPosition,
            string keySignature,
            IReadOnlyList<HarmonySuggestion> suggestions,
            bool supportsEngineSelection = false,
            HarmonySuggestionEngineKind initialEngineKind = HarmonySuggestionEngineKind.Legacy,
            Func<HarmonySuggestionEngineKind, IReadOnlyList<HarmonySuggestion>> reloadSuggestions = null)
        {
            _suggestions = suggestions ?? Array.Empty<HarmonySuggestion>();
            _reloadSuggestions = reloadSuggestions;

            Text = "Chord Suggestions";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;

            var y = 16;
            var contextLabel = new Label
            {
                Location = new Point(16, y),
                Size = new Size(388, 36),
                Text = "Measure " + measureNumber + ", beat " + (beatPosition + 1) + ", key " + (keySignature ?? "1=C")
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
                    _suggestions = _reloadSuggestions(kind) ?? Array.Empty<HarmonySuggestion>();
                    PopulateSuggestionList();
                };
                Controls.Add(engineCombo);
                y += 26;
            }

            var hintLabel = new Label
            {
                Location = new Point(16, y),
                Size = new Size(388, 20),
                ForeColor = Color.DimGray,
                Text = "Based on the current key signature and melody pitch, suggests 1-3 chords."
            };
            Controls.Add(hintLabel);
            y += 26;

            _suggestionList = new ListBox
            {
                Location = new Point(16, y),
                Size = new Size(388, 160),
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
            y += 160 + 16;

            PopulateSuggestionList();

            var applyButton = new Button
            {
                Text = "Apply",
                DialogResult = DialogResult.OK,
                Location = new Point(232, y),
                Width = 80
            };
            var cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(324, y),
                Width = 80
            };
            Controls.Add(applyButton);
            Controls.Add(cancelButton);
            AcceptButton = applyButton;
            CancelButton = cancelButton;

            ClientSize = new Size(420, y + 16 + applyButton.Height);
        }

        private void PopulateSuggestionList()
        {
            _suggestionList.Items.Clear();
            foreach (var suggestion in _suggestions)
            {
                _suggestionList.Items.Add("(" + suggestion.RomanNumeral + ") " + suggestion.ChordSymbol + " — " + suggestion.Reason);
            }

            if (_suggestionList.Items.Count > 0)
            {
                _suggestionList.SelectedIndex = 0;
            }
        }

        public HarmonySuggestion SelectedSuggestion
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
