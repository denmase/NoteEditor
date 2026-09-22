namespace JianpuEditor.Models
{
    /// <summary>Which chord-suggestion backend is currently answering
    /// <see cref="Core.Abstractions.IHarmonySuggestionService"/> calls.</summary>
    public enum HarmonySuggestionEngineKind
    {
        /// <summary>Simple, deterministic diatonic-lookup suggester (the original engine).</summary>
        Legacy,

        /// <summary>Enhanced engine: melody-fit weighting, voice leading, cadence detection, and
        /// an optional Markov chord-transition model trained from a corpus of real progressions.</summary>
        Markov
    }
}
