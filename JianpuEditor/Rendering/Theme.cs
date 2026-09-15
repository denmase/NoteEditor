using System.Drawing;

namespace JianpuEditor.Rendering
{
    /// <summary>
    /// A named set of color tokens the rest of the app (forms, dialogs, the score canvas) reads
    /// through <see cref="AppTheme"/> instead of hard-coding colors or branching on a dark-mode
    /// flag inline. Adding a new theme means adding one more <see cref="Theme"/> instance here --
    /// nothing that reads <see cref="AppTheme.Current"/> or its per-purpose color properties needs
    /// to change.
    /// </summary>
    public sealed class Theme
    {
        public static readonly Theme ManuscriptLight = new Theme(
            id: "ManuscriptLight",
            name: "Manuscript Light",
            isDark: false,
            paper: Color.FromArgb(0xF6, 0xF3, 0xEC),
            paperRaised: Color.FromArgb(0xFF, 0xFD, 0xF9),
            paperSunken: Color.FromArgb(0xEF, 0xEC, 0xE2),
            ink: Color.FromArgb(0x22, 0x1F, 0x1A),
            inkSoft: Color.FromArgb(0x6F, 0x67, 0x59),
            inkFaint: Color.FromArgb(0xA8, 0x9E, 0x8C),
            line: Color.FromArgb(0xE2, 0xDB, 0xCB),
            lineStrong: Color.FromArgb(0xCF, 0xC5, 0xAC),
            accent: Color.FromArgb(0xB8, 0x41, 0x2F),
            accentStrong: Color.FromArgb(0x93, 0x32, 0x1F),
            accentWash: Color.FromArgb(0xF4, 0xE2, 0xDC),
            good: Color.FromArgb(0x3D, 0x7A, 0x4F));

        public static readonly Theme ManuscriptDark = new Theme(
            id: "ManuscriptDark",
            name: "Manuscript Dark",
            isDark: true,
            paper: Color.FromArgb(0x18, 0x16, 0x0F),
            paperRaised: Color.FromArgb(0x22, 0x1F, 0x17),
            paperSunken: Color.FromArgb(0x10, 0x0E, 0x09),
            ink: Color.FromArgb(0xEC, 0xE6, 0xD8),
            inkSoft: Color.FromArgb(0xAB, 0x9F, 0x8A),
            inkFaint: Color.FromArgb(0x6D, 0x65, 0x52),
            line: Color.FromArgb(0x38, 0x2F, 0x22),
            lineStrong: Color.FromArgb(0x4A, 0x40, 0x30),
            accent: Color.FromArgb(0xD9, 0x63, 0x4F),
            accentStrong: Color.FromArgb(0xEC, 0x79, 0x62),
            accentWash: Color.FromArgb(0x3A, 0x26, 0x20),
            good: Color.FromArgb(0x6C, 0xBF, 0x82));

        private Theme(
            string id,
            string name,
            bool isDark,
            Color paper,
            Color paperRaised,
            Color paperSunken,
            Color ink,
            Color inkSoft,
            Color inkFaint,
            Color line,
            Color lineStrong,
            Color accent,
            Color accentStrong,
            Color accentWash,
            Color good)
        {
            Id = id;
            Name = name;
            IsDark = isDark;
            Paper = paper;
            PaperRaised = paperRaised;
            PaperSunken = paperSunken;
            Ink = ink;
            InkSoft = inkSoft;
            InkFaint = inkFaint;
            Line = line;
            LineStrong = lineStrong;
            Accent = accent;
            AccentStrong = accentStrong;
            AccentWash = accentWash;
            Good = good;
        }

        /// <summary>Stable key persisted in settings.json; not shown to the user.</summary>
        public string Id { get; }

        /// <summary>Display name for a future theme picker.</summary>
        public string Name { get; }

        public bool IsDark { get; }

        /// <summary>Page/canvas background -- the "desk" the score sits on.</summary>
        public Color Paper { get; }

        /// <summary>Toolbar, dialog, and score-paper background -- sits above <see cref="Paper"/>.</summary>
        public Color PaperRaised { get; }

        /// <summary>Recessed chrome around the canvas.</summary>
        public Color PaperSunken { get; }

        /// <summary>Primary text and note glyphs.</summary>
        public Color Ink { get; }

        /// <summary>Secondary text (captions, meta info).</summary>
        public Color InkSoft { get; }

        /// <summary>Tertiary/disabled text.</summary>
        public Color InkFaint { get; }

        /// <summary>Hairline borders and separators.</summary>
        public Color Line { get; }

        /// <summary>Stronger borders -- input outlines, card edges.</summary>
        public Color LineStrong { get; }

        /// <summary>The one accent hue: selection, active/playing state, primary actions.</summary>
        public Color Accent { get; }

        /// <summary>Accent hover/pressed state.</summary>
        public Color AccentStrong { get; }

        /// <summary>Accent tinted background -- selection fills, active-toggle backgrounds.</summary>
        public Color AccentWash { get; }

        /// <summary>Connected/success indicator (e.g. the audio engine status dot).</summary>
        public Color Good { get; }

        public static Theme FromId(string id)
        {
            return id == ManuscriptDark.Id ? ManuscriptDark : ManuscriptLight;
        }
    }
}
