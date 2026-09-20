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
        /// </summary>
        public const float AnnotationLayerClearance = 6f;

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
