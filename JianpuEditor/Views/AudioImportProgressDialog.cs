using System.Drawing;
using System.Windows.Forms;

namespace JianpuEditor.Views
{
    /// <summary>
    /// Modeless "please wait" window shown while audio transcription runs on a background
    /// thread. Owning code shows it, updates <see cref="SetMessage"/> as progress reports
    /// arrive, and closes it when the import finishes (success or failure) -- it carries no
    /// cancel button since neither transcriber currently supports cooperative cancellation.
    /// </summary>
    public sealed class AudioImportProgressDialog : Form
    {
        private readonly Label _messageLabel;
        private readonly ProgressBar _progressBar;

        public AudioImportProgressDialog(string title)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ControlBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(360, 90);

            _messageLabel = new Label
            {
                Text = "Starting...",
                Location = new Point(16, 16),
                Size = new Size(328, 32),
                AutoEllipsis = true
            };

            _progressBar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Location = new Point(16, 52),
                Size = new Size(328, 20)
            };

            Controls.Add(_messageLabel);
            Controls.Add(_progressBar);
        }

        public void SetMessage(string message)
        {
            _messageLabel.Text = message;
        }
    }
}
