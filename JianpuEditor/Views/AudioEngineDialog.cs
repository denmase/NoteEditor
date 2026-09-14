using System.Drawing;
using System.Windows.Forms;

namespace JianpuEditor.Views
{
    public sealed class AudioEngineDialog : Form
    {
        private readonly RadioButton _soundFontOption;
        private readonly RadioButton _vstOption;
        private readonly TextBox _vstPathBox;
        private readonly Button _browseButton;

        public AudioEngineDialog(string currentVstPluginPath, string activeEngineName)
        {
            Text = "Audio Engine";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(420, 214);

            var hasVstPath = !string.IsNullOrWhiteSpace(currentVstPluginPath);

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
                Text = "VST2 instrument plugin:",
                Location = new Point(16, 68),
                AutoSize = true,
                Checked = hasVstPath
            };

            _vstPathBox = new TextBox
            {
                Location = new Point(36, 94),
                Width = 300,
                Text = currentVstPluginPath ?? string.Empty,
                Enabled = hasVstPath
            };

            _browseButton = new Button
            {
                Text = "Browse...",
                Location = new Point(340, 92),
                Width = 64,
                Enabled = hasVstPath
            };
            _browseButton.Click += OnBrowseClicked;

            _vstOption.CheckedChanged += (s, e) =>
            {
                _vstPathBox.Enabled = _vstOption.Checked;
                _browseButton.Enabled = _vstOption.Checked;
            };

            var hintLabel = new Label
            {
                Text = "Hosts a VST2 instrument DLL (not VST3) via BASSVST for both melody and chords.\nTakes effect after restarting Jianpu Editor. If the configured plugin fails to\nload, playback silently falls back to the bundled SoundFont.",
                Location = new Point(16, 126),
                Size = new Size(388, 50),
                ForeColor = Color.DimGray
            };

            var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(236, 176), Width = 76 };
            var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(320, 176), Width = 76 };

            Controls.Add(activeLabel);
            Controls.Add(_soundFontOption);
            Controls.Add(_vstOption);
            Controls.Add(_vstPathBox);
            Controls.Add(_browseButton);
            Controls.Add(hintLabel);
            Controls.Add(okButton);
            Controls.Add(cancelButton);
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        /// <summary>Empty string means "use the bundled SoundFont".</summary>
        public string SelectedVstPluginPath
        {
            get { return _vstOption.Checked ? (_vstPathBox.Text ?? string.Empty).Trim() : string.Empty; }
        }

        private void OnBrowseClicked(object sender, System.EventArgs e)
        {
            using (var dialog = new OpenFileDialog
            {
                Filter = "VST2 Plugin (*.dll)|*.dll|All Files (*.*)|*.*",
                Title = "Select VST2 Instrument Plugin"
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _vstPathBox.Text = dialog.FileName;
                }
            }
        }
    }
}
