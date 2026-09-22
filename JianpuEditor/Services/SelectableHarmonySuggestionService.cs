using System.Collections.Generic;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Models;

namespace JianpuEditor.Services
{
    /// <summary>
    /// Runtime-switchable <see cref="IHarmonySuggestionService"/>: delegates every call to either
    /// the original rule-based suggester ("Legacy") or the enhanced Markov/voice-leading engine
    /// ("Markov"), chosen via <see cref="ActiveKind"/>. Registered as the app's single
    /// IHarmonySuggestionService so existing consumers (e.g. ChordEditorViewModel) don't need to
    /// know which backend answered; casting to <see cref="IHarmonySuggestionEngineOptions"/>
    /// exposes the picker to the UI. Defaults to Legacy, so existing behaviour is unchanged until
    /// a user opts in.
    /// </summary>
    internal sealed class SelectableHarmonySuggestionService : IHarmonySuggestionService, IHarmonySuggestionEngineOptions
    {
        private readonly IHarmonySuggestionService _legacy;
        private readonly IHarmonySuggestionService _markov;

        public SelectableHarmonySuggestionService(IHarmonySuggestionService legacy, IHarmonySuggestionService markov)
        {
            _legacy = legacy;
            _markov = markov;
        }

        public HarmonySuggestionEngineKind ActiveKind { get; set; } = HarmonySuggestionEngineKind.Legacy;

        private IHarmonySuggestionService Active
        {
            get { return ActiveKind == HarmonySuggestionEngineKind.Markov ? _markov : _legacy; }
        }

        public IReadOnlyList<HarmonySuggestion> SuggestForMeasure(JianpuMeasure measure, string keySignature, double beatPosition)
        {
            return Active.SuggestForMeasure(measure, keySignature, beatPosition);
        }

        public IReadOnlyList<HarmonyProgressionSuggestion> SuggestForMeasureRange(IReadOnlyList<JianpuMeasure> measures, string keySignature)
        {
            return Active.SuggestForMeasureRange(measures, keySignature);
        }
    }
}
