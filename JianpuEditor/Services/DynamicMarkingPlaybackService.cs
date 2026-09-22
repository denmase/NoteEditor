namespace JianpuEditor.Services
{
    /// <summary>Maps a dynamics marking's text to a fixed MIDI velocity, evenly spaced across the
    /// 0-127 range (roughly matching common notation-software defaults). Unrecognized text (a
    /// score with no dynamics at all, or a future marking this doesn't know about yet) falls back
    /// to whatever velocity was already in effect, so a score with none of these markings schedules
    /// byte-identical output to before this existed.</summary>
    public static class DynamicMarkingPlaybackService
    {
        public const int PianissimoVelocity = 33;
        public const int PianoVelocity = 49;
        public const int MezzoPianoVelocity = 64;
        public const int MezzoForteVelocity = 80;
        public const int ForteVelocity = 96;
        public const int FortissimoVelocity = 112;

        /// <summary>How much a hairpin nudges velocity when its end note carries no explicit
        /// discrete dynamic marking to interpolate toward -- roughly one step of the pp..ff
        /// ladder above (adjacent levels are spaced ~16 apart), so an unmarked crescendo/
        /// diminuendo still produces an audible, proportionate change instead of guessing wildly
        /// or doing nothing.</summary>
        public const int NominalHairpinVelocityDelta = 16;

        public static int ResolveVelocity(string text, int fallback)
        {
            switch ((text ?? string.Empty).Trim())
            {
                case "pp":
                    return PianissimoVelocity;
                case "p":
                    return PianoVelocity;
                case "mp":
                    return MezzoPianoVelocity;
                case "mf":
                    return MezzoForteVelocity;
                case "f":
                    return ForteVelocity;
                case "ff":
                    return FortissimoVelocity;
                default:
                    return fallback;
            }
        }
    }
}
