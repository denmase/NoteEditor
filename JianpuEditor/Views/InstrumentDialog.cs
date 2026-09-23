using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using JianpuEditor.Models;
using JianpuEditor.Services;

namespace JianpuEditor.Views
{
    public sealed class InstrumentDialog : Form
    {
        private static readonly string[] ChordStyleNames = { "Block (sustained)", "Comping (rhythmic)", "Arpeggio (broken chord)", "Strum" };

        private const int RowSpacing = 36;

        private readonly ComboBox _melodyCombo;
        private readonly ComboBox _chordCombo;
        private readonly ComboBox _chordStyleCombo;
        private readonly List<ComboBox> _extraVoiceCombos = new List<ComboBox>();

        public InstrumentDialog(
            int currentMelodyInstrument,
            int currentChordInstrument,
            ChordPlaybackStyle currentChordPlaybackStyle,
            IReadOnlyList<string> extraVoiceLabels,
            IReadOnlyList<int> currentExtraVoiceInstruments)
        {
            extraVoiceLabels = extraVoiceLabels ?? Array.Empty<string>();

            Text = "Instruments";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;

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

            Controls.Add(melodyLabel);
            Controls.Add(_melodyCombo);
            Controls.Add(chordLabel);
            Controls.Add(_chordCombo);
            Controls.Add(chordStyleLabel);
            Controls.Add(_chordStyleCombo);

            var lastRowY = 92;
            for (var i = 0; i < extraVoiceLabels.Count; i++)
            {
                lastRowY += RowSpacing;
                var label = string.IsNullOrEmpty(extraVoiceLabels[i]) ? "Voice " + (i + 1) : extraVoiceLabels[i];
                var voiceLabel = new Label { Text = label + ":", Location = new Point(16, lastRowY), AutoSize = true };
                var voiceCombo = new ComboBox
                {
                    Location = new Point(108, lastRowY - 4),
                    Width = 256,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                voiceCombo.Items.AddRange(GeneralMidiInstruments.Names);
                var currentProgram = (currentExtraVoiceInstruments != null && i < currentExtraVoiceInstruments.Count)
                    ? currentExtraVoiceInstruments[i]
                    : ScoreMidiSchedule.DefaultExtraVoiceInstrument;
                voiceCombo.SelectedIndex = GeneralMidiInstruments.Clamp(currentProgram);

                Controls.Add(voiceLabel);
                Controls.Add(voiceCombo);
                _extraVoiceCombos.Add(voiceCombo);
            }

            var hintY = lastRowY + 32;
            var hintLabel = new Label
            {
                Text = "General MIDI instruments and chord accompaniment pattern, used for playback and MIDI export.",
                Location = new Point(16, hintY),
                Size = new Size(348, 32),
                ForeColor = Color.DimGray
            };
            Controls.Add(hintLabel);

            var buttonsY = hintY + 40;
            var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(196, buttonsY), Width = 76 };
            var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(280, buttonsY), Width = 76 };
            Controls.Add(okButton);
            Controls.Add(cancelButton);
            AcceptButton = okButton;
            CancelButton = cancelButton;

            ClientSize = new Size(380, buttonsY + 40);
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

        /// <summary>Selected General MIDI program per extra-voice slot, in the same order as the
        /// <c>extraVoiceLabels</c>/<c>currentExtraVoiceInstruments</c> constructor arguments.</summary>
        public IReadOnlyList<int> SelectedExtraVoiceInstruments
        {
            get
            {
                var result = new int[_extraVoiceCombos.Count];
                for (var i = 0; i < result.Length; i++)
                {
                    result[i] = _extraVoiceCombos[i].SelectedIndex;
                }

                return result;
            }
        }
    }
}
