using System.Drawing;
using System.Windows.Forms;

namespace JianpuEditor.Views
{
    public sealed class AudioEngineDialog : Form
    {
        private readonly TextBox _soundFontPathBox;
        private readonly Button _browseButton;

        public AudioEngineDialog(string currentCustomSoundFontPath, string activeEngineName)
        {
            Text = "Audio Engine";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(420, 190);

            var activeLabel = new Label
            {
                Text = "Currently active: " + (activeEngineName ?? "unknown"),
                Location = new Point(16, 12),
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold)
            };

            var pathLabel = new Label
            {
                Text = "Instrument file (SoundFont .sf2 or SFZ .sfz):",
                Location = new Point(16, 44),
                AutoSize = true
            };

            _soundFontPathBox = new TextBox
            {
                Location = new Point(16, 64),
                Width = 324,
                Text = currentCustomSoundFontPath ?? string.Empty
            };
            _browseButton = new Button
            {
                Text = "Browse...",
                Location = new Point(340, 62),
                Width = 64
            };
            _browseButton.Click += (s, e) =>
            {
                using (var browseDialog = new OpenFileDialog
                {
                    Filter = "SoundFont/SFZ Instrument (*.sf2;*.sfz)|*.sf2;*.sfz|All Files (*.*)|*.*",
                    Title = "Select Instrument File"
                })
                {
                    if (browseDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        _soundFontPathBox.Text = browseDialog.FileName;
                    }
                }
            };

            var hintLabel = new Label
            {
                Text = "Leave blank to use the bundled General MIDI SoundFont. Point to a full-quality\n" +
                       "SF2 or SFZ instrument library instead for better piano/guitar/cello sound.\n" +
                       "Takes effect after restarting Jianpu Editor. If the file fails to load,\n" +
                       "playback silently falls back to the bundled SoundFont.",
                Location = new Point(16, 94),
                Size = new Size(388, 60),
                ForeColor = Color.DimGray
            };

            var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(236, 152), Width = 76 };
            var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(320, 152), Width = 76 };

            Controls.Add(activeLabel);
            Controls.Add(pathLabel);
            Controls.Add(_soundFontPathBox);
            Controls.Add(_browseButton);
            Controls.Add(hintLabel);
            Controls.Add(okButton);
            Controls.Add(cancelButton);
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        /// <summary>Empty string means "use the bundled SoundFont".</summary>
        public string SelectedSoundFontPath
        {
            get { return (_soundFontPathBox.Text ?? string.Empty).Trim(); }
        }
    }
}
