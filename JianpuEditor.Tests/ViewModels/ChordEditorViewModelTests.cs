using JianpuEditor.Models;
using JianpuEditor.Services;
using JianpuEditor.Tests.Helpers;
using JianpuEditor.ViewModels;
using Xunit;

namespace JianpuEditor.Tests.ViewModels
{
    public class ChordEditorViewModelTests
    {
        [Fact]
        public void AddChordMarker_AddsMarkerToCurrentMeasure()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var chordEditor = ViewModelTestHelper.CreateChordEditor(document, selection, navigation, messenger, history: history);
            document.EnsureMeasures();
            selection.UpdateFrom(new ScoreSelectionInfo { MeasureIndex = 0 });

            var result = chordEditor.AddChordMarker();

            Assert.True(result.Changed);
            Assert.Single(document.Score.Measures[0].ChordMarkers);
        }

        [Fact]
        public void TransposeChords_UpdatesKeySignature()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            var transpose = new FakeChordTransposeService();
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var chordEditor = ViewModelTestHelper.CreateChordEditor(document, selection, navigation, messenger, transpose, history: history);
            document.EnsureMeasures();
            document.KeySignature = "1=C";
            document.Score.Measures[0].ChordMarkers.Add(new ChordMarker { Text = "C", BeatPosition = 0 });

            var result = chordEditor.TransposeChords("1=G");

            Assert.True(result.Changed);
            Assert.Equal("1=G", document.KeySignature);
        }

        [Fact]
        public void GetHarmonySuggestions_CMajor_ReturnsPrimaryChords()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var chordEditor = ViewModelTestHelper.CreateChordEditor(document, selection, navigation, messenger, history: history);
            document.EnsureMeasures();
            document.KeySignature = "1=C";
            document.Score.Measures[0].MelodyNotes = new System.Collections.Generic.List<JianpuNote>
            {
                ScoreTestHelper.Note(1)
            };

            var suggestions = chordEditor.GetHarmonySuggestions(0, 0);

            Assert.NotEmpty(suggestions);
            Assert.Equal("C", suggestions[0].ChordSymbol);
            Assert.Equal("I", suggestions[0].RomanNumeral);
        }

        [Fact]
        public void GetHarmonyProgressionSuggestions_ContiguousMeasures_ReturnsProgression()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var chordEditor = ViewModelTestHelper.CreateChordEditor(document, selection, navigation, messenger, history: history);
            document.EnsureMeasures();
            document.KeySignature = "1=C";
            document.Score.Measures[0].MelodyNotes = new System.Collections.Generic.List<JianpuNote> { ScoreTestHelper.Note(1) };
            document.Score.Measures.Add(ScoreTestHelper.Measure(ScoreTestHelper.Note(4)));
            document.Score.Measures.Add(ScoreTestHelper.Measure(ScoreTestHelper.Note(5)));
            document.Score.Measures.Add(ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));

            var suggestions = chordEditor.GetHarmonyProgressionSuggestions(0, 3);

            Assert.NotEmpty(suggestions);
            Assert.Equal(4, suggestions[0].Steps.Count);
        }

        [Fact]
        public void ApplyHarmonyProgressionSuggestion_WritesChordsAcrossMeasures()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var chordEditor = ViewModelTestHelper.CreateChordEditor(document, selection, navigation, messenger, history: history);
            document.EnsureMeasures();
            document.Score.Measures.Add(ScoreTestHelper.Measure(ScoreTestHelper.Note(4)));
            var progression = chordEditor.GetHarmonyProgressionSuggestions(0, 1)[0];

            var result = chordEditor.ApplyHarmonyProgressionSuggestion(progression, 0);

            Assert.True(result.Changed);
            Assert.NotEmpty(document.Score.Measures[0].ChordMarkers);
            Assert.NotEmpty(document.Score.Measures[1].ChordMarkers);
        }

        [Fact]
        public void ApplyHarmonySuggestion_AddsChordMarkerAtBeat()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var chordEditor = ViewModelTestHelper.CreateChordEditor(document, selection, navigation, messenger, history: history);
            document.EnsureMeasures();
            selection.UpdateFrom(new ScoreSelectionInfo { MeasureIndex = 0, NoteIndex = 0 });

            var result = chordEditor.ApplyHarmonySuggestion(0, 0, "G");

            Assert.True(result.Changed);
            Assert.Single(document.Score.Measures[0].ChordMarkers);
            Assert.Equal("G", document.Score.Measures[0].ChordMarkers[0].Text);
        }

        [Fact]
        public void TransposeChords_ReturnsErrorWhenServiceFails()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            var transpose = new FakeChordTransposeService { ShouldSucceed = false, ErrorMessage = "Invalid key signature" };
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var chordEditor = ViewModelTestHelper.CreateChordEditor(document, selection, navigation, messenger, transpose, history: history);
            document.EnsureMeasures();
            document.KeySignature = "1=C";

            var result = chordEditor.TransposeChords("1=Z");

            Assert.False(result.Changed);
            Assert.Equal("Invalid key signature", result.Message);
            Assert.Equal("1=C", document.KeySignature);
        }

        [Fact]
        public void SupportsHarmonyEngineSelection_PlainHarmonyService_IsFalse()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var chordEditor = ViewModelTestHelper.CreateChordEditor(document, selection, navigation, messenger, history: history);

            Assert.False(chordEditor.SupportsHarmonyEngineSelection);
            Assert.Equal(HarmonySuggestionEngineKind.Legacy, chordEditor.HarmonyEngineKind);

            // Writing is a harmless no-op when the injected service doesn't support switching.
            chordEditor.HarmonyEngineKind = HarmonySuggestionEngineKind.Markov;
            Assert.Equal(HarmonySuggestionEngineKind.Legacy, chordEditor.HarmonyEngineKind);
        }

        [Fact]
        public void HarmonyEngineKind_SelectableService_SwitchesTheActiveBackend()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var selectable = new SelectableHarmonySuggestionService(
                new HarmonySuggestionServiceAdapter(),
                new MarkovHarmonySuggestionService());
            var chordEditor = ViewModelTestHelper.CreateChordEditor(
                document, selection, navigation, messenger, harmonySuggestionService: selectable, history: history);

            Assert.True(chordEditor.SupportsHarmonyEngineSelection);
            Assert.Equal(HarmonySuggestionEngineKind.Legacy, chordEditor.HarmonyEngineKind);

            chordEditor.HarmonyEngineKind = HarmonySuggestionEngineKind.Markov;

            Assert.Equal(HarmonySuggestionEngineKind.Markov, chordEditor.HarmonyEngineKind);
            Assert.Equal(HarmonySuggestionEngineKind.Markov, selectable.ActiveKind);
        }
    }
}
