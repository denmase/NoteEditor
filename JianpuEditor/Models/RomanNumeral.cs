using System;

namespace JianpuEditor.Models
{
    /// <summary>
    /// Roman-numeral chord label with optional accidental, quality and secondary target, used by
    /// the enhanced (Markov) harmony engine. Examples: "I", "V7", "ii7", "bVII", "V7/V".
    /// </summary>
    public sealed class RomanNumeral
    {
        /// <summary>Scale degree in the current key (1..7).</summary>
        public int Degree { get; set; }

        /// <summary>-1 = flat, 0 = natural, +1 = sharp.</summary>
        public int Accidental { get; set; }

        public ChordQuality Quality { get; set; }

        /// <summary>Non-null when this functions as a secondary chord (e.g. V7/V).</summary>
        public RomanNumeral SecondaryOf { get; set; }

        public override string ToString()
        {
            var core = AccidentalPrefix(Accidental) + DegreeToRoman(Degree, Quality) + RomanSuffix(Quality);
            if (SecondaryOf == null)
            {
                return core;
            }

            return core + "/" + AccidentalPrefix(SecondaryOf.Accidental) + DegreeToRoman(SecondaryOf.Degree, SecondaryOf.Quality);
        }

        private static string AccidentalPrefix(int accidental)
        {
            if (accidental < 0)
            {
                return "b";
            }

            if (accidental > 0)
            {
                return "#";
            }

            return string.Empty;
        }

        private static string DegreeToRoman(int degree, ChordQuality quality)
        {
            var upper = new[] { string.Empty, "I", "II", "III", "IV", "V", "VI", "VII" };
            var clampedDegree = Math.Max(1, Math.Min(7, degree));
            var roman = upper[clampedDegree];
            return quality.IsMinorQuality() ? roman.ToLowerInvariant() : roman;
        }

        /// <summary>
        /// <see cref="ChordQualityExtensions.GetSuffix"/> already signals minor-ness with a
        /// leading "m" (e.g. Minor -> "m", Minor7 -> "m7") for use in a plain chord symbol like
        /// "Dm7", where there's no letter case to lean on. A roman numeral instead signals minor
        /// via lower-case (see <see cref="DegreeToRoman"/>), so appending the full suffix here
        /// too would double-encode it (e.g. "vi" + "m" = "vim", "ii" + "m7" = "iim7" instead of
        /// the conventional "vi" / "ii7"). Strip that redundant leading "m" for the roman-numeral
        /// case only; the plain chord-symbol path (<see cref="Services.HarmonyEngine"/>'s
        /// ComputeChordSymbol) still uses the untouched <see cref="ChordQualityExtensions.GetSuffix"/>.
        /// </summary>
        private static string RomanSuffix(ChordQuality quality)
        {
            var suffix = quality.GetSuffix();
            if (quality.IsMinorQuality() && suffix.StartsWith("m", StringComparison.Ordinal))
            {
                return suffix.Substring(1);
            }

            return suffix;
        }
    }
}
