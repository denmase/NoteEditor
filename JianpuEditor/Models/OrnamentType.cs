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
        Custom = 99
    }
}
