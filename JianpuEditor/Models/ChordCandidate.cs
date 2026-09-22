using System.Collections.Generic;

namespace JianpuEditor.Models
{
    /// <summary>A single ranked chord candidate produced by the enhanced (Markov) harmony engine.</summary>
    public sealed class ChordCandidate
    {
        public RomanNumeral Roman { get; set; } = new RomanNumeral();

        public double Score { get; set; }

        public List<string> Reasons { get; set; } = new List<string>();

        public string ChordSymbol { get; set; } = string.Empty;
    }
}
