using JianpuEditor.Models;
using JianpuEditor.Tests.Helpers;
using JianpuEditor.ViewModels;
using Xunit;

namespace JianpuEditor.Tests.ViewModels
{
    public sealed class DynamicsEditorViewModelTests
    {
        [Fact]
        public void SetDynamic_AppliesToSelectedNote()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            document.EnsureMeasures();
            document.Score.Measures[0].MelodyNotes.Add(ScoreTestHelper.Note(1));
            selection.UpdateFrom(new ScoreSelectionInfo { MeasureIndex = 0, NoteIndex = 0 });
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var editor = ViewModelTestHelper.CreateDynamicsEditor(document, navigation, selection, messenger, history);

            var result = editor.SetDynamic("mf");

            Assert.True(result.Changed);
            Assert.Single(document.Score.Measures[0].Dynamics);
            Assert.Equal("mf", document.Score.Measures[0].Dynamics[0].Text);
        }

        [Fact]
        public void SetDynamic_SameLevelTwice_RemovesIt()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            document.EnsureMeasures();
            document.Score.Measures[0].MelodyNotes.Add(ScoreTestHelper.Note(1));
            selection.UpdateFrom(new ScoreSelectionInfo { MeasureIndex = 0, NoteIndex = 0 });
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var editor = ViewModelTestHelper.CreateDynamicsEditor(document, navigation, selection, messenger, history);
            editor.SetDynamic("f");

            var result = editor.SetDynamic("f");

            Assert.True(result.Changed);
            Assert.Empty(document.Score.Measures[0].Dynamics);
        }

        [Fact]
        public void SetDynamic_DifferentLevel_ReplacesPreviousOne()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            document.EnsureMeasures();
            document.Score.Measures[0].MelodyNotes.Add(ScoreTestHelper.Note(1));
            selection.UpdateFrom(new ScoreSelectionInfo { MeasureIndex = 0, NoteIndex = 0 });
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var editor = ViewModelTestHelper.CreateDynamicsEditor(document, navigation, selection, messenger, history);
            editor.SetDynamic("p");

            var result = editor.SetDynamic("ff");

            Assert.True(result.Changed);
            Assert.Single(document.Score.Measures[0].Dynamics);
            Assert.Equal("ff", document.Score.Measures[0].Dynamics[0].Text);
        }

        [Fact]
        public void SetDynamic_NoSelection_ReturnsUnchanged()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            document.EnsureMeasures();
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var editor = ViewModelTestHelper.CreateDynamicsEditor(document, navigation, selection, messenger, history);

            var result = editor.SetDynamic("mf");

            Assert.False(result.Changed);
        }

        [Fact]
        public void AddHairpin_MultiNoteSelection_SpansEarliestToLatestSelectedNote()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            document.EnsureMeasures();
            document.Score.Measures[0].MelodyNotes.Add(ScoreTestHelper.Note(1));
            document.Score.Measures[0].MelodyNotes.Add(ScoreTestHelper.Note(2));
            document.Score.Measures[0].MelodyNotes.Add(ScoreTestHelper.Note(3));
            selection.UpdateFrom(new ScoreSelectionInfo
            {
                SelectedNotes = new[] { new ScoreNoteRef(0, 2), new ScoreNoteRef(0, 0) }
            });
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var editor = ViewModelTestHelper.CreateDynamicsEditor(document, navigation, selection, messenger, history);

            var result = editor.AddHairpin(true);

            Assert.True(result.Changed);
            Assert.Single(document.Score.Hairpins);
            var hairpin = document.Score.Hairpins[0];
            Assert.Equal(0, hairpin.StartNoteIndex);
            Assert.Equal(2, hairpin.EndNoteIndex);
            Assert.True(hairpin.IsCrescendo);
        }

        [Fact]
        public void AddHairpin_SingleNoteSelected_ReturnsUnchanged()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            document.EnsureMeasures();
            document.Score.Measures[0].MelodyNotes.Add(ScoreTestHelper.Note(1));
            selection.UpdateFrom(new ScoreSelectionInfo { MeasureIndex = 0, NoteIndex = 0 });
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var editor = ViewModelTestHelper.CreateDynamicsEditor(document, navigation, selection, messenger, history);

            var result = editor.AddHairpin(false);

            Assert.False(result.Changed);
            Assert.Empty(document.Score.Hairpins);
        }

        [Fact]
        public void RemoveHairpin_SelectedNoteInsideSpan_RemovesIt()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            document.EnsureMeasures();
            document.Score.Measures[0].MelodyNotes.Add(ScoreTestHelper.Note(1));
            document.Score.Measures[0].MelodyNotes.Add(ScoreTestHelper.Note(2));
            document.Score.Measures[0].MelodyNotes.Add(ScoreTestHelper.Note(3));
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var editor = ViewModelTestHelper.CreateDynamicsEditor(document, navigation, selection, messenger, history);
            selection.UpdateFrom(new ScoreSelectionInfo
            {
                SelectedNotes = new[] { new ScoreNoteRef(0, 0), new ScoreNoteRef(0, 2) }
            });
            editor.AddHairpin(true);
            selection.UpdateFrom(new ScoreSelectionInfo { MeasureIndex = 0, NoteIndex = 1 });

            var result = editor.RemoveHairpin();

            Assert.True(result.Changed);
            Assert.Empty(document.Score.Hairpins);
        }

        [Fact]
        public void AddHairpin_Undo_RemovesAddedHairpin()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            document.EnsureMeasures();
            document.Score.Measures[0].MelodyNotes.Add(ScoreTestHelper.Note(1));
            document.Score.Measures[0].MelodyNotes.Add(ScoreTestHelper.Note(2));
            selection.UpdateFrom(new ScoreSelectionInfo
            {
                SelectedNotes = new[] { new ScoreNoteRef(0, 0), new ScoreNoteRef(0, 1) }
            });
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var editor = ViewModelTestHelper.CreateDynamicsEditor(document, navigation, selection, messenger, history);
            editor.AddHairpin(true);

            history.Undo();

            Assert.Empty(document.Score.Hairpins);
        }

        [Fact]
        public void SetDynamic_Undo_RemovesAddedMarking()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            document.EnsureMeasures();
            document.Score.Measures[0].MelodyNotes.Add(ScoreTestHelper.Note(1));
            selection.UpdateFrom(new ScoreSelectionInfo { MeasureIndex = 0, NoteIndex = 0 });
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var editor = ViewModelTestHelper.CreateDynamicsEditor(document, navigation, selection, messenger, history);
            editor.SetDynamic("mf");

            history.Undo();

            Assert.Empty(document.Score.Measures[0].Dynamics);
        }
    }
}
