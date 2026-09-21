namespace JianpuEditor.Models
{
    /// <summary>Which regional jianpu/notasi angka convention the renderer follows for the small
    /// set of genuine style differences between them (as opposed to notation elements missing from
    /// one or the other, which are additive and not gated by this at all -- see ROADMAP.md's
    /// "Indonesian notasi angka completeness" section for the full gap analysis).</summary>
    public enum NotationStyle
    {
        /// <summary>Chinese/Western convention: accidentals as a `#`/`b` prefix, beat-group
        /// underlines below the melody row. The renderer's long-standing default.</summary>
        Chinese = 0,

        /// <summary>Indonesian notasi angka convention: accidentals as a `/` (kres/sharp) or `\`
        /// (mol/flat) suffix, beat-group underlines above the melody row.</summary>
        Indonesian = 1
    }
}
