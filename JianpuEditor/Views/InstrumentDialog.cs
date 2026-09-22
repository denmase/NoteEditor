using System.Drawing;
using System.Windows.Forms;
using JianpuEditor.Models;
using JianpuEditor.Services;

namespace JianpuEditor.Views
{
    public sealed class InstrumentDialog : Form
    {
        private static readonly string[] ChordStyleNames = { "Block (sustained)", "Comping (rhythmic)", "Arpeggio (broken chord)", "Strum" };

        private readonly ComboBox _melodyCombo;
        private readonly ComboBox _chordCombo;
        private readonly ComboBox _chordStyleCombo;

        public InstrumentDialog(int currentMelodyInstrument, int currentChordInstrument, ChordPlaybackStyle currentChordPlaybackStyle)
        {
            Text = "Instruments";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(380, 204);

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

            var chordStyleLabel = new Label { Text = "Chord style:", Location = new Point(16, 92), AutoSize = true };
            _chordStyleCombo = new ComboBox
            {
                Location = new Point(108, 88),
                Width = 256,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _chordStyleCombo.Items.AddRange(ChordStyleNames);
            _chordStyleCombo.SelectedIndex = (int)currentChordPlaybackStyle;

            var hintLabel = new Label
            {
                Text = "General MIDI instruments and chord accompaniment pattern, used for playback and MIDI export.",
                Location = new Point(16, 124),
                Size = new Size(348, 32),
                ForeColor = Color.DimGray
            };

            var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(196, 164), Width = 76 };
            var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(280, 164), Width = 76 };

            Controls.Add(melodyLabel);
            Controls.Add(_melodyCombo);
            Controls.Add(chordLabel);
            Controls.Add(_chordCombo);
            Controls.Add(chordStyleLabel);
            Controls.Add(_chordStyleCombo);
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

        public ChordPlaybackStyle SelectedChordPlaybackStyle
        {
            get { return (ChordPlaybackStyle)_chordStyleCombo.SelectedIndex; }
        }
    }
}
