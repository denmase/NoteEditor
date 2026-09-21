namespace JianpuEditor.Models
{
    public enum NoteType
    {
        Note,
        Rest
    }

    public class JianpuNote
    {
        public NoteType Type { get; set; } = NoteType.Note;

        /// <summary>0 = rest, 1-7 = natural pitch, 1.5 / 2.5 = chromatic (.5 + Accidental).</summary>
        public double Pitch { get; set; } = 1;

        /// <summary>Sharp / flat spelling for .5 pitches (#1, b3).</summary>
        public AccidentalKind Accidental { get; set; }

        /// <summary>-1 = low octave dot, 0 = normal, 1 = high octave dot.</summary>
        public int Octave { get; set; }

        /// <summary>Number of duration underlines (0=quarter, 1=eighth, 2=sixteenth).</summary>
        public int Underlines { get; set; }

        /// <summary>Number of duration-extension dashes: 0=quarter, 1=half, 3=whole note (each dash adds +1 beat).</summary>
        public int Dashes { get; set; }

        public bool Dotted { get; set; }

        /// <summary>True for a jianpu continuation dot ("holds the previous pitch") when
        /// <see cref="Type"/> is <see cref="NoteType.Rest"/> -- as opposed to a true rest (silence).
        /// Unlike <see cref="Dashes"/> (which only widens the *preceding* note's own cell and can't
        /// be beamed on its own), a continuation dot is its own beat-grid slot with its own
        /// <see cref="Underlines"/> depth, so it can share a beam with an adjacent note exactly like
        /// a real note can -- the gap found cross-checking against real notasi angka sheet music
        /// (see ROADMAP.md). Rendered as "." instead of "0"; contributes no new MIDI note-on of its
        /// own but extends the duration of whatever note is currently sounding (see
        /// ScoreMidiSchedule.BuildMelodyNotes). Ignored when <see cref="Type"/> is
        /// <see cref="NoteType.Note"/>.</summary>
        public bool IsContinuation { get; set; }
    }
}
