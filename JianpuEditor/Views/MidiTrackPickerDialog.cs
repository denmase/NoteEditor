using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using JianpuEditor.Models;

namespace JianpuEditor.Views
{
    /// <summary>
    /// Lets the user pick which MIDI track to import as the melody, instead of silently trusting
    /// the auto-detection heuristic -- a busy accompaniment track can easily outscore the real tune.
    /// Shown only when a file has more than one candidate track; the heuristic's own pick is
    /// pre-selected and labeled, so accepting the default is still a single Enter press.
    /// </summary>
    public sealed class MidiTrackPickerDialog : Form
    {
        private readonly ListBox _trackList;
        private readonly List<MidiTrackInfo> _tracks;

        public MidiTrackPickerDialog(IReadOnlyList<MidiTrackInfo> tracks)
        {
            _tracks = new List<MidiTrackInfo>(tracks);

            Text = "Choose Melody Track";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(440, 300);

            var hintLabel = new Label
            {
                Text = "This MIDI file has multiple tracks. Pick the one that has the melody\n"
                    + "you want to import -- the recommended pick is pre-selected.",
                Location = new Point(16, 12),
                Size = new Size(408, 36)
            };

            _trackList = new ListBox
            {
                Location = new Point(16, 54),
                Size = new Size(408, 190),
                IntegralHeight = false
            };

            var selectIndex = 0;
            for (var i = 0; i < _tracks.Count; i++)
            {
                _trackList.Items.Add(_tracks[i].DisplayLabel);
                if (_tracks[i].IsRecommended)
                {
                    selectIndex = i;
                }
            }

            if (_trackList.Items.Count > 0)
            {
                _trackList.SelectedIndex = selectIndex;
            }

            var okButton = new Button { Text = "Import", DialogResult = DialogResult.OK, Location = new Point(276, 256), Width = 76 };
            var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(360, 256), Width = 64 };

            Controls.Add(hintLabel);
            Controls.Add(_trackList);
            Controls.Add(okButton);
            Controls.Add(cancelButton);
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        /// <summary>The chosen track's real index in the MIDI file (for MidiImportService.Import).</summary>
        public int SelectedTrackIndex
        {
            get
            {
                var index = _trackList.SelectedIndex;
                return index >= 0 && index < _tracks.Count ? _tracks[index].Index : _tracks[0].Index;
            }
        }
    }
}
