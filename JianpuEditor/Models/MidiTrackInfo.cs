namespace JianpuEditor.Models
{
    /// <summary>
    /// Summary of one track in a MIDI file, for showing the user a track picker before import
    /// (rather than silently trusting the melody-detection heuristic). Track index matches the raw
    /// order in the MIDI file, so it can be passed straight back into MidiImportService.Import.
    /// </summary>
    public sealed class MidiTrackInfo
    {
        public int Index { get; set; }

        /// <summary>The track's name meta-event text, or empty if the track has none.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Non-drum note-on count -- what the melody heuristic actually scores.</summary>
        public int NoteCount { get; set; }

        /// <summary>Highest number of notes sounding at once. 1 means genuinely monophonic.</summary>
        public int MaxSimultaneousNotes { get; set; }

        /// <summary>True for the track the heuristic would pick if the user doesn't override it.</summary>
        public bool IsRecommended { get; set; }

        public string DisplayLabel
        {
            get
            {
                var label = "Track " + (Index + 1);
                if (!string.IsNullOrWhiteSpace(Name))
                {
                    label += ": " + Name;
                }

                label += " (" + NoteCount + " notes";
                if (MaxSimultaneousNotes > 1)
                {
                    label += ", up to " + MaxSimultaneousNotes + " at once";
                }

                label += ")";
                if (IsRecommended)
                {
                    label += " [recommended]";
                }

                return label;
            }
        }
    }
}
