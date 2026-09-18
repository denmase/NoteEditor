using System.Drawing;
using System.Windows.Forms;

namespace JianpuEditor.Views
{
    public sealed class AudioEngineDialog : Form
    {
        private readonly RadioButton _soundFontOption;
        private readonly RadioButton _vstOption;
        private readonly TextBox _melodyVstPathBox;
        private readonly Button _melodyBrowseButton;
        private readonly TextBox _chordVstPathBox;
        private readonly Button _chordBrowseButton;

        public AudioEngineDialog(string currentMelodyVstPluginPath, string currentChordVstPluginPath, string activeEngineName)
        {
            Text = "Audio Engine";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(420, 268);

            var hasVstPath = !string.IsNullOrWhiteSpace(currentMelodyVstPluginPath);

            var activeLabel = new Label
            {
                Text = "Currently active: " + (activeEngineName ?? "unknown"),
                Location = new Point(16, 12),
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold)
            };

            _soundFontOption = new RadioButton
            {
                Text = "Bundled SoundFont (default)",
                Location = new Point(16, 40),
                AutoSize = true,
                Checked = !hasVstPath
            };

            _vstOption = new RadioButton
            {
                Text = "VST2 instrument plugin(s):",
                Location = new Point(16, 68),
                AutoSize = true,
                Checked = hasVstPath
            };

            var melodyLabel = new Label { Text = "Melody:", Location = new Point(36, 98), AutoSize = true };
            _melodyVstPathBox = new TextBox
            {
                Location = new Point(100, 94),
                Width = 236,
                Text = currentMelodyVstPluginPath ?? string.Empty,
                Enabled = hasVstPath
            };
            _melodyBrowseButton = new Button
            {
                Text = "Browse...",
                Location = new Point(340, 92),
                Width = 64,
                Enabled = hasVstPath
            };
            _melodyBrowseButton.Click += (s, e) => BrowseInto(_melodyVstPathBox);

            var chordLabel = new Label { Text = "Chords:", Location = new Point(36, 126), AutoSize = true };
            _chordVstPathBox = new TextBox
            {
                Location = new Point(100, 122),
                Width = 236,
                Text = currentChordVstPluginPath ?? string.Empty,
                Enabled = hasVstPath
            };
            _chordBrowseButton = new Button
            {
                Text = "Browse...",
                Location = new Point(340, 120),
                Width = 64,
                Enabled = hasVstPath
            };
            _chordBrowseButton.Click += (s, e) => BrowseInto(_chordVstPathBox);

            _vstOption.CheckedChanged += (s, e) =>
            {
                _melodyVstPathBox.Enabled = _vstOption.Checked;
                _melodyBrowseButton.Enabled = _vstOption.Checked;
                _chordVstPathBox.Enabled = _vstOption.Checked;
                _chordBrowseButton.Enabled = _vstOption.Checked;
            };

            var hintLabel = new Label
            {
                Text = "Hosts VST2 instrument DLLs (not VST3) via BASSVST -- one for melody, one for\n" +
                       "chords. Leave Chords blank to use the melody plugin for both. Takes effect\n" +
                       "after restarting Jianpu Editor. If a configured plugin fails to load, playback\n" +
                       "silently falls back to the bundled SoundFont.",
                Location = new Point(16, 152),
                Size = new Size(388, 66),
                ForeColor = Color.DimGray
            };

            var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(236, 230), Width = 76 };
            var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(320, 230), Width = 76 };

            Controls.Add(activeLabel);
            Controls.Add(_soundFontOption);
            Controls.Add(_vstOption);
            Controls.Add(melodyLabel);
            Controls.Add(_melodyVstPathBox);
            Controls.Add(_melodyBrowseButton);
            Controls.Add(chordLabel);
            Controls.Add(_chordVstPathBox);
            Controls.Add(_chordBrowseButton);
            Controls.Add(hintLabel);
            Controls.Add(okButton);
            Controls.Add(cancelButton);
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        /// <summary>Empty string means "use the bundled SoundFont".</summary>
        public string SelectedMelodyVstPluginPath
        {
            get { return _vstOption.Checked ? (_melodyVstPathBox.Text ?? string.Empty).Trim() : string.Empty; }
        }

        /// <summary>Empty string means "reuse the melody plugin for chords too" (or "use the
        /// bundled SoundFont", if <see cref="SelectedMelodyVstPluginPath"/> is also empty).</summary>
        public string SelectedChordVstPluginPath
        {
            get { return _vstOption.Checked ? (_chordVstPathBox.Text ?? string.Empty).Trim() : string.Empty; }
        }

        private void BrowseInto(TextBox targetBox)
        {
            using (var dialog = new OpenFileDialog
            {
                Filter = "VST2 Plugin (*.dll)|*.dll|All Files (*.*)|*.*",
                Title = "Select VST2 Instrument Plugin"
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    targetBox.Text = dialog.FileName;
                }
            }
        }
    }
}
