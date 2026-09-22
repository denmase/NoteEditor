namespace JianpuEditor.Models
{
    /// <summary>
    /// How a score's chord markers are turned into MIDI notes during playback/export -- the
    /// "chord track"'s backing-accompaniment pattern. Default (0) is <see cref="Block"/>, so an
    /// existing score with no explicit choice plays exactly as it always has.
    /// </summary>
    public enum ChordPlaybackStyle
    {
        /// <summary>One sustained hit spanning the chord's full duration (the original, only
        /// behavior before this style existed).</summary>
        Block,

        /// <summary>Re-strikes the chord on every beat within its span, each hit slightly
        /// detached -- a simple piano/guitar "comping" backing part.</summary>
        Comping,

        /// <summary>Breaks the chord into its individual notes, played one at a time in a
        /// repeating up-down pattern (e.g. a triad becomes root, third, fifth, third, root, ...)
        /// instead of struck together.</summary>
        Arpeggio,

        /// <summary>Same notes as <see cref="Block"/>, but each note's onset is staggered by a
        /// small fraction of a beat (low to high), mimicking a real strum across strings.</summary>
        Strum
    }
}
