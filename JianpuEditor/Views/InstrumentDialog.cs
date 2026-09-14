using System.Drawing;
using System.Windows.Forms;
using JianpuEditor.Services;

namespace JianpuEditor.Views
{
    public sealed class InstrumentDialog : Form
    {
        private readonly ComboBox _melodyCombo;
        private readonly ComboBox _chordCombo;

        public InstrumentDialog(int currentMelodyInstrument, int currentChordInstrument)
        {
            Text = "Instruments";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(380, 168);

            var melodyLabel = new Label { Text = "Melody:", Location = new Point(16, 20), AutoSize = true };
            _melodyCombo = new ComboBox
            {
                Location = new Point(108, 16),
                Width = 256,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _melodyCombo.Items.AddRange(GeneralMidiInstruments.Names);
            _melodyCombo.SelectedIndex = GeneralMidiInstruments.Clamp(currentMelodyInstrument);

            var chordLabel = new Label { Text = "Chords:", Location = new Point(16, 56), AutoSize = true };
            _chordCombo = new ComboBox
            {
                Location = new Point(108, 52),
                Width = 256,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _chordCombo.Items.AddRange(GeneralMidiInstruments.Names);
            _chordCombo.SelectedIndex = GeneralMidiInstruments.Clamp(currentChordInstrument);

            var hintLabel = new Label
            {
                Text = "General MIDI instruments, used for playback and MIDI export.",
                Location = new Point(16, 88),
                Size = new Size(348, 32),
                ForeColor = Color.DimGray
            };

            var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(196, 128), Width = 76 };
            var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(280, 128), Width = 76 };

            Controls.Add(melodyLabel);
            Controls.Add(_melodyCombo);
            Controls.Add(chordLabel);
            Controls.Add(_chordCombo);
            Controls.Add(hintLabel);
            Controls.Add(okButton);
            Controls.Add(cancelButton);
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        public int SelectedMelodyInstrument
        {
            get { return _melodyCombo.SelectedIndex; }
        }

        public int SelectedChordInstrument
        {
            get { return _chordCombo.SelectedIndex; }
        }
    }
}
