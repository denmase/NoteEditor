using System;
using System.Drawing;
using System.Windows.Forms;

namespace JianpuEditor
{
    public sealed partial class MainForm
    {
        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1280, 820);
            MinimumSize = new Size(960, 640);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Jianpu Editor";
            Font = new Font("Microsoft YaHei", 9f);
            KeyPreview = true;
            Icon = LoadApplicationIcon();
            ResumeLayout(false);
        }

        // The ApplicationIcon MSBuild property embeds the .ico as the exe's own Win32 resource --
        // it does not automatically become the running window's title-bar/taskbar icon, which
        // WinForms otherwise defaults to a generic one. Extracting it back from the exe avoids
        // needing a second, separately-embedded copy of the same icon just for this.
        private static Icon LoadApplicationIcon()
        {
            try
            {
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                return null;
            }
        }
    }
}