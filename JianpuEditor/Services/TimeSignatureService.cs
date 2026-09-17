using System.Text.RegularExpressions;

namespace JianpuEditor.Services
{
    /// <summary>Parses the score header's free-text time signature (e.g. "3/4", "6/8")
    /// into the quarter-note-equivalent beat count a measure should hold, so editor
    /// validation can compare it against a measure's actual note content.</summary>
    public static class TimeSignatureService
    {
        private static readonly Regex Pattern = new Regex(
            @"^\s*(\d{1,2})\s*/\s*(\d{1,2})\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool TryGetQuarterBeatsPerMeasure(string timeSignature, out double quarterBeats)
        {
            quarterBeats = 0;
            if (string.IsNullOrWhiteSpace(timeSignature))
            {
                return false;
            }

            var match = Pattern.Match(timeSignature);
            if (!match.Success)
            {
                return false;
            }

            var numerator = int.Parse(match.Groups[1].Value);
            var denominator = int.Parse(match.Groups[2].Value);
            if (numerator <= 0 || denominator <= 0)
            {
                return false;
            }

            quarterBeats = numerator * (4.0 / denominator);
            return true;
        }
    }
}
