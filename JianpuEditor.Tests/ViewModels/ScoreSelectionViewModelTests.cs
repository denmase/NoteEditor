using JianpuEditor.Models;
using JianpuEditor.ViewModels;
using Xunit;

namespace JianpuEditor.Tests.ViewModels
{
    public class ScoreSelectionViewModelTests
    {
        [Fact]
        public void BuildSelectionDescription_DescribesSelectedNote()
        {
            var document = ViewModelTestHelper.CreateDocument();
            var selection = new ScoreSelectionViewModel(document);
            selection.UpdateFrom(new ScoreSelectionInfo
            {
                MeasureIndex = 0,
                NoteIndex = 2
            });

            var description = selection.BuildSelectionDescription();

            Assert.Contains("note 3 in measure 1", description);
        }

        [Fact]
        public void BuildSelectionDescription_DescribesMultipleSelectedNotes()
        {
            var document = ViewModelTestHelper.CreateDocument();
            var selection = new ScoreSelectionViewModel(document);
            selection.UpdateFrom(new ScoreSelectionInfo
            {
                MeasureIndex = 0,
                NoteIndex = 1,
                SelectedNotes = new[]
                {
                    new ScoreNoteRef(0, 0),
                    new ScoreNoteRef(0, 1),
                    new ScoreNoteRef(0, 2)
                }
            });

            var description = selection.BuildSelectionDescription();

            Assert.Contains("Selected 3 note(s)", description);
        }

        [Fact]
        public void BuildSelectionDescription_DescribesMultiMeasureRange()
        {
            var document = ViewModelTestHelper.CreateDocument();
            var selection = new ScoreSelectionViewModel(document);
            selection.UpdateFrom(new ScoreSelectionInfo
            {
                MeasureIndex = 0,
                SelectedMeasureIndices = new[] { 0, 1, 2 }
            });

            var description = selection.BuildSelectionDescription();

            Assert.Contains("measures 1 to 3", description);
        }
    }
}
