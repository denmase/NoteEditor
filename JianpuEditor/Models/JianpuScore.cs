using System.Collections.Generic;

namespace JianpuEditor.Models
{
    public class JianpuScore
    {
        public string Title { get; set; } = "Untitled Score";

        public string KeySignature { get; set; } = "1=C";

        public string Tempo { get; set; } = "Moderato";

        public string TimeSignature { get; set; } = "4/4";

        /// <summary>Beats per minute, used for MIDI export.</summary>
        public int Bpm { get; set; } = 120;

        public string Composer { get; set; } = string.Empty;

        /// <summary>Display name for the primary voice's (<see cref="JianpuMeasure.MelodyNotes"/>)
        /// own row label, shown next to the melody staff the way each <see cref="JianpuVoice.Role"/>
        /// labels its own row. Null/empty falls back to "Melody" -- the default for a plain,
        /// single-voice score. <see cref="Services.VoiceModeService.ApplySatb"/> sets this to
        /// "Soprano" (only when it's still at that default, so it never clobbers a name the user
        /// picked by hand) and <see cref="Services.VoiceModeService.ApplySingle"/> resets it back to
        /// null, since "Soprano" only makes sense alongside Alto/Tenor/Bass.</summary>
        public string PrimaryVoiceLabel { get; set; }

        /// <summary>General MIDI program number (0-127) used for the main melody during playback/MIDI export.</summary>
        public int MelodyInstrument { get; set; }

        /// <summary>General MIDI program number (0-127) used for chord markers during playback/MIDI export.</summary>
        public int ChordInstrument { get; set; }

        public List<JianpuMeasure> Measures { get; set; } = new List<JianpuMeasure>();

        public List<JianpuTie> Ties { get; set; } = new List<JianpuTie>();

        public List<JianpuVolta> Voltas { get; set; } = new List<JianpuVolta>();

        public List<JianpuHairpin> Hairpins { get; set; } = new List<JianpuHairpin>();
    }
}
