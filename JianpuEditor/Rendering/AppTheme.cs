using System;
using System.Drawing;
using System.IO;
using JianpuEditor.Models;
using Newtonsoft.Json;

namespace JianpuEditor.Rendering
{
    /// <summary>
    /// Static facade the rest of the app reads for colors and a few persisted display settings.
    /// Colors are no longer ad-hoc "IsDarkMode ? a : b" ternaries -- they're all derived from
    /// <see cref="Current"/>, a <see cref="Theme"/> token set. Adding a theme means adding one more
    /// <see cref="Theme"/> preset and, if it should be user-selectable, one more branch in
    /// <see cref="SetDarkMode"/>/a future theme picker; every property below picks it up for free.
    /// </summary>
    public static class AppTheme
    {
        private static readonly string SettingsPath = ResolveSettingsPath();

        /// <summary>
        /// The portable build drops this marker file next to the exe so settings stay inside the
        /// portable folder instead of the per-user profile (an installed copy has no such file).
        /// </summary>
        private static string ResolveSettingsPath()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var portableMarker = Path.Combine(baseDirectory, "portable.txt");
            if (File.Exists(portableMarker))
            {
                return Path.Combine(baseDirectory, "settings.json");
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JianpuEditor",
                "settings.json");
        }

        /// <summary>The active theme's full token set, for code (e.g. the toolbar/status bar) that
        /// wants a token not exposed as its own named property below.</summary>
        public static Theme Current { get; private set; } = Theme.ManuscriptLight;

        public static bool IsDarkMode
        {
            get { return Current.IsDark; }
        }

        public static bool FillMeasurePlaceholdersOnAdd { get; private set; } = true;

        /// <summary>Which regional jianpu convention is active. Source of truth for every style
        /// difference between the two conventions -- see <see cref="Models.NotationStyle"/>.</summary>
        public static NotationStyle NotationStyle { get; private set; } = NotationStyle.Chinese;

        /// <summary>When true, beat-group duration underlines are drawn above the melody row instead
        /// of below it -- the notation convention used in the Indonesian Jianpu variant, as opposed
        /// to the Chinese/Western convention (lines below) this renderer defaults to. Computed from
        /// <see cref="NotationStyle"/> rather than its own independent flag, so every existing call
        /// site that reads this keeps working unchanged.</summary>
        public static bool UnderlinesAbove
        {
            get { return NotationStyle == NotationStyle.Indonesian; }
        }

        /// <summary>Path to a VST2 instrument plugin DLL to use for the melody part instead of the
        /// bundled SoundFont, or empty to use the SoundFont. Required for VST playback; the chord
        /// part falls back to this same plugin when <see cref="VstChordPluginPath"/> is empty.</summary>
        public static string VstMelodyPluginPath { get; private set; } = string.Empty;

        /// <summary>Path to a separate VST2 instrument plugin DLL for the chord part, or empty to
        /// reuse <see cref="VstMelodyPluginPath"/> for chords too.</summary>
        public static string VstChordPluginPath { get; private set; } = string.Empty;

        /// <summary>Path to a custom SoundFont (.sf2) or SFZ instrument file to use instead of the
        /// bundled General MIDI SoundFont, or empty to use the bundled one.</summary>
        public static string CustomSoundFontPath { get; private set; } = string.Empty;

        /// <summary>Path to a local <c>midi_ddsp_model_weights_urmp_9_10</c> folder (see
        /// external/csharp-midi-dssp's own README for how to obtain one), or empty if neural
        /// synthesis isn't configured. Not bundled with the app -- these are tens of MB of
        /// pretrained model weights the user downloads separately, same as a custom SoundFont.</summary>
        public static string MidiDdspWeightsPath { get; private set; } = string.Empty;

        public static event Action ThemeChanged;

        public static void Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return;
                }

                var json = File.ReadAllText(SettingsPath);
                var settings = JsonConvert.DeserializeObject<ThemeSettings>(json);
                Current = (settings?.DarkMode ?? false) ? Theme.ManuscriptDark : Theme.ManuscriptLight;
                FillMeasurePlaceholdersOnAdd = settings?.FillMeasurePlaceholdersOnAdd ?? true;
                // VstMelodyPluginPath falls back to the pre-dual-instrument VstPluginPath field so
                // a setting saved by an older build isn't silently dropped.
                VstMelodyPluginPath = settings?.VstMelodyPluginPath ?? settings?.VstPluginPath ?? string.Empty;
                VstChordPluginPath = settings?.VstChordPluginPath ?? string.Empty;
                CustomSoundFontPath = settings?.CustomSoundFontPath ?? string.Empty;
                MidiDdspWeightsPath = settings?.MidiDdspWeightsPath ?? string.Empty;
                NotationStyle = ResolveNotationStyle(settings);
            }
            catch
            {
                Current = Theme.ManuscriptLight;
                FillMeasurePlaceholdersOnAdd = true;
                VstMelodyPluginPath = string.Empty;
                VstChordPluginPath = string.Empty;
                CustomSoundFontPath = string.Empty;
                MidiDdspWeightsPath = string.Empty;
                NotationStyle = NotationStyle.Chinese;
            }
        }

        /// <summary>A settings file saved by a build from before <see cref="NotationStyle"/>
        /// existed only has the old <c>UnderlinesAbove</c> bool -- migrate `true` there to
        /// `Indonesian` so nobody's saved preference is silently reset back to Chinese.</summary>
        private static NotationStyle ResolveNotationStyle(ThemeSettings settings)
        {
            if (settings == null)
            {
                return NotationStyle.Chinese;
            }

            if (!string.IsNullOrEmpty(settings.NotationStyle)
                && Enum.TryParse(settings.NotationStyle, out NotationStyle parsed))
            {
                return parsed;
            }

            return settings.UnderlinesAbove ? NotationStyle.Indonesian : NotationStyle.Chinese;
        }

        public static void SetDarkMode(bool enabled, bool persist = true)
        {
            var next = enabled ? Theme.ManuscriptDark : Theme.ManuscriptLight;
            if (Current == next)
            {
                return;
            }

            Current = next;
            if (persist)
            {
                Save();
            }

            ThemeChanged?.Invoke();
        }

        public static void SetFillMeasurePlaceholdersOnAdd(bool enabled, bool persist = true)
        {
            if (FillMeasurePlaceholdersOnAdd == enabled)
            {
                return;
            }

            FillMeasurePlaceholdersOnAdd = enabled;
            if (persist)
            {
                Save();
            }
        }

        public static void SetNotationStyle(NotationStyle style, bool persist = true)
        {
            if (NotationStyle == style)
            {
                return;
            }

            NotationStyle = style;
            if (persist)
            {
                Save();
            }

            ThemeChanged?.Invoke();
        }

        public static void SetVstPluginPaths(string melodyPath, string chordPath, bool persist = true)
        {
            melodyPath = melodyPath ?? string.Empty;
            chordPath = chordPath ?? string.Empty;
            if (VstMelodyPluginPath == melodyPath && VstChordPluginPath == chordPath)
            {
                return;
            }

            VstMelodyPluginPath = melodyPath;
            VstChordPluginPath = chordPath;
            if (persist)
            {
                Save();
            }
        }

        public static void SetCustomSoundFontPath(string path, bool persist = true)
        {
            path = path ?? string.Empty;
            if (CustomSoundFontPath == path)
            {
                return;
            }

            CustomSoundFontPath = path;
            if (persist)
            {
                Save();
            }
        }

        public static void SetMidiDdspWeightsPath(string path, bool persist = true)
        {
            path = path ?? string.Empty;
            if (MidiDdspWeightsPath == path)
            {
                return;
            }

            MidiDdspWeightsPath = path;
            if (persist)
            {
                Save();
            }
        }

        public static Color FormBackground
        {
            get { return Current.Paper; }
        }

        public static Color FormForeground
        {
            get { return Current.Ink; }
        }

        public static Color InputBackground
        {
            get { return Current.PaperRaised; }
        }

        public static Color InputForeground
        {
            get { return Current.Ink; }
        }

        public static Color CanvasChrome
        {
            get { return Current.PaperSunken; }
        }

        public static Color ScorePaper
        {
            get { return Current.PaperRaised; }
        }

        public static Color PrimaryText
        {
            get { return Current.Ink; }
        }

        public static Color SecondaryText
        {
            get { return Current.InkSoft; }
        }

        public static Color TieActive
        {
            get { return Current.Accent; }
        }

        public static Color TieInactive
        {
            get { return Current.Ink; }
        }

        public static Color ChordBackground
        {
            get { return Current.PaperRaised; }
        }

        public static Color ChordSelectedBackground
        {
            get { return Current.AccentWash; }
        }

        public static Color ChordBorder
        {
            get { return Current.LineStrong; }
        }

        public static Color ChordSelectedBorder
        {
            get { return Current.Accent; }
        }

        public static Color InlineEditorBackground
        {
            get { return Current.AccentWash; }
        }

        public static Color TieModeButtonBackground
        {
            get { return Current.AccentWash; }
        }

        public static Color Separator
        {
            get { return Current.Line; }
        }

        public static Color GetScoreBackground(bool respectTheme)
        {
            return respectTheme ? ScorePaper : Color.White;
        }

        private static void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(
                    new ThemeSettings
                    {
                        DarkMode = Current.IsDark,
                        FillMeasurePlaceholdersOnAdd = FillMeasurePlaceholdersOnAdd,
                        VstMelodyPluginPath = VstMelodyPluginPath,
                        VstChordPluginPath = VstChordPluginPath,
                        CustomSoundFontPath = CustomSoundFontPath,
                        MidiDdspWeightsPath = MidiDdspWeightsPath,
                        NotationStyle = NotationStyle.ToString()
                    },
                    Formatting.Indented);
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Theme persistence is best-effort.
            }
        }

        private sealed class ThemeSettings
        {
            public bool DarkMode { get; set; }

            public bool FillMeasurePlaceholdersOnAdd { get; set; } = true;

            /// <summary>Legacy field from before melody/chord VST plugins were separate; read as a
            /// fallback for VstMelodyPluginPath, never written.</summary>
            public string VstPluginPath { get; set; } = string.Empty;

            public string VstMelodyPluginPath { get; set; } = string.Empty;

            public string VstChordPluginPath { get; set; } = string.Empty;

            public string CustomSoundFontPath { get; set; } = string.Empty;

            public string MidiDdspWeightsPath { get; set; } = string.Empty;

            /// <summary>Legacy field from before NotationStyle existed; read as a migration
            /// fallback (see ResolveNotationStyle), never written.</summary>
            public bool UnderlinesAbove { get; set; }

            public string NotationStyle { get; set; }
        }
    }
}
