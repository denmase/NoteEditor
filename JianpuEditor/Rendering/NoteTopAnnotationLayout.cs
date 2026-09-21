using JianpuEditor.Models;

namespace JianpuEditor.Rendering
{
    public sealed class NoteTopAnnotationLayout
    {
        public const float DigitTextY = 18f;

        public const float AccidentalBandY = 12f;

        public const float OctaveDotBandY = 7f;

        public const float OctaveDotBandYWithoutAccidental = 12f;

        public const float OrnamentBandYWithoutLowerLayers = 12f;

        /// <summary>
        /// Vertical clearance a stacked outer layer (center/grace ornament, then fermata) claims
        /// above whatever sits directly below it -- accidental/octave dots, another ornament layer,
        /// or the note itself. Layers are placed outward one at a time by subtracting this from
        /// the Y of whatever's below, so any combination gets real clearance instead of the fixed,
        /// hand-picked-per-combination bands this replaced (which had no notion of, e.g., a fermata
        /// stacked above a center ornament -- both landed 2px apart regardless of what else was on
        /// the note).
        /// Was 6f originally, which visually collided with an octave dot below it (the gap is
        /// between the two layers' anchor Y values, not their actual rendered glyph heights, and
        /// 6px is well under the ornament font's real line height) -- confirmed by rendering real
        /// output via Mono+libgdiplus and visually inspecting it, something not previously possible
        /// in this sandbox. Meant to cover the taller of the two stacked ornament fonts' own
        /// <see cref="System.Drawing.Font.Height"/> (Microsoft YaHei / Arial Italic, both 11pt) --
        /// libgdiplus reports 18 for these (no Microsoft YaHei on Linux, so it substitutes a
        /// fallback font), but real Windows GDI+ CI reported 20, so this carries a couple of
        /// pixels of headroom above that measured value rather than sitting exactly on it. See
        /// <c>NoteTopAnnotationPlannerTests.AnnotationLayerClearance_CoversTheTallestStackedOrnamentFont</c>.
        /// </summary>
        public const float AnnotationLayerClearance = 22f;

        public const float OctaveDotDiameter = 6f;

        public const float OctaveDotStackSpacing = 6f;

        public const float AccidentalMarkWidth = 8f;

        /// <summary>Same gap <see cref="JianpuRenderer"/>'s non-compact layout path uses -- kept as
        /// its own constant here since this class has no reference to JianpuRenderer's.</summary>
        private const float BreathMarkGap = 3f;

        public float HeadCenterX { get; set; }

        public float OctaveDotCenterX { get; set; }

        public float OctaveDotBaseY { get; set; }

        public bool HasAccidental { get; set; }

        /// <summary>True for a Sharp/Flat accidental under <see cref="Models.NotationStyle.Indonesian"/>
        /// (kres `/` / mol `\`), which draws as a suffix to the right of the digit at its own
        /// baseline instead of the stacked upper-left band <see cref="AccidentalX"/>/<see
        /// cref="AccidentalY"/> describe -- so it needs neither the octave-dot dodge nor the
        /// ornament-band headroom those positions exist for. A Natural sign stays in the upper-left
        /// band under both notation styles (see the note on <c>JianpuPitchCodec.IsSuffixAccidental</c>),
        /// so this is false for it even when the style is Indonesian.</summary>
        public bool AccidentalIsSuffix { get; set; }

        public bool HasHighOctaveDots { get; set; }

        public float AccidentalX { get; set; }

        public float AccidentalY { get; set; }

        public AccidentalKind AccidentalKind { get; set; }

        public bool HasGraceOrnament { get; set; }

        public bool HasCenterOrnament { get; set; }

        public bool HasFermata { get; set; }

        public float OrnamentY { get; set; }

        public float FermataY { get; set; }

        public float GetOrnamentAnchorX(OrnamentType type, int noteX, int headWidth)
        {
            if (type == OrnamentType.BreathMark)
            {
                return noteX + headWidth + BreathMarkGap;
            }

            return HeadCenterX;
        }

        public float GetOrnamentY(OrnamentType type)
        {
            return type == OrnamentType.Fermata ? FermataY : OrnamentY;
        }
    }
}
