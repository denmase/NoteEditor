namespace JianpuEditor.Models
{
    /// <summary>Intervallic structure of a chord, used by the enhanced (Markov) harmony engine.</summary>
    public enum ChordQuality
    {
        Major,
        Minor,
        Diminished,
        Augmented,
        Dominant7,
        Major7,
        Minor7,
        HalfDiminished7,
        Diminished7,
        Sus2,
        Sus4
    }

    /// <summary>Translates a <see cref="ChordQuality"/> into chord-symbol suffixes and intervals.</summary>
    public static class ChordQualityExtensions
    {
        /// <summary>Symbol suffix, e.g. Minor -> "m", Dominant7 -> "7".</summary>
        public static string GetSuffix(this ChordQuality quality)
        {
            switch (quality)
            {
                case ChordQuality.Major:
                    return string.Empty;
                case ChordQuality.Minor:
                    return "m";
                case ChordQuality.Diminished:
                    return "dim";
                case ChordQuality.Augmented:
                    return "aug";
                case ChordQuality.Dominant7:
                    return "7";
                case ChordQuality.Major7:
                    return "maj7";
                case ChordQuality.Minor7:
                    return "m7";
                case ChordQuality.HalfDiminished7:
                    return "m7b5";
                case ChordQuality.Diminished7:
                    return "dim7";
                case ChordQuality.Sus2:
                    return "sus2";
                case ChordQuality.Sus4:
                    return "sus4";
                default:
                    return string.Empty;
            }
        }

        /// <summary>Semitone intervals from the root.</summary>
        public static int[] GetIntervals(this ChordQuality quality)
        {
            switch (quality)
            {
                case ChordQuality.Major:
                    return new[] { 0, 4, 7 };
                case ChordQuality.Minor:
                    return new[] { 0, 3, 7 };
                case ChordQuality.Diminished:
                    return new[] { 0, 3, 6 };
                case ChordQuality.Augmented:
                    return new[] { 0, 4, 8 };
                case ChordQuality.Dominant7:
                    return new[] { 0, 4, 7, 10 };
                case ChordQuality.Major7:
                    return new[] { 0, 4, 7, 11 };
                case ChordQuality.Minor7:
                    return new[] { 0, 3, 7, 10 };
                case ChordQuality.HalfDiminished7:
                    return new[] { 0, 3, 6, 10 };
                case ChordQuality.Diminished7:
                    return new[] { 0, 3, 6, 9 };
                case ChordQuality.Sus2:
                    return new[] { 0, 2, 7 };
                case ChordQuality.Sus4:
                    return new[] { 0, 5, 7 };
                default:
                    return new[] { 0, 4, 7 };
            }
        }

        /// <summary>True when the quality's roman numeral is conventionally spelled lower-case (uses a lowered third).</summary>
        public static bool IsMinorQuality(this ChordQuality quality)
        {
            return quality == ChordQuality.Minor
                || quality == ChordQuality.Minor7
                || quality == ChordQuality.HalfDiminished7
                || quality == ChordQuality.Diminished
                || quality == ChordQuality.Diminished7;
        }
    }
}
