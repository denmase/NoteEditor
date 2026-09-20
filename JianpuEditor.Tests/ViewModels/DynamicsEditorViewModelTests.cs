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
