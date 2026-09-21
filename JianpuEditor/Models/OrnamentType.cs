namespace JianpuEditor.Models
{
    /// <summary>Ornament and score symbol types, for future toolbar and rendering extensions.</summary>
    public enum OrnamentType
    {
        Unknown = 0,
        GraceNote = 1,
        Trill = 2,
        Mordent = 3,
        Turn = 4,
        Glissando = 5,
        Fermata = 6,
        Staccato = 7,
        Accent = 8,
        Tenuto = 9,
        RepeatStart = 10,
        RepeatEnd = 11,
        Segno = 12,
        Coda = 13,
        BreathMark = 14,

        /// <summary>"D.C." -- jump back to the very beginning of the score. See
        /// <see cref="Services.SegnoCodaPlaybackExpander"/> for how this, <see cref="DalSegno"/>,
        /// <see cref="Fine"/>, and <see cref="Coda"/> combine to drive actual playback/MIDI-export
        /// navigation (Segno/Coda alone are just point markers with no jump semantics of their
        /// own).</summary>
        DaCapo = 15,

        /// <summary>"D.S." -- jump back to the <see cref="Segno"/> mark.</summary>
        DalSegno = 16,

        /// <summary>"Fine" -- where a D.C./D.S. "al Fine" ending stops.</summary>
        Fine = 17,

        Custom = 99
    }
}
