using JianpuEditor.Models;
using JianpuEditor.ViewModels;
using Xunit;

namespace JianpuEditor.Tests.ViewModels
{
    public class MainViewModelTests
    {
        [Fact]
        public void NewScoreCommand_ResetsDocumentAndStatus()
        {
            var main = ViewModelTestHelper.CreateMainViewModel();
            main.Document.Title = "Old";

            main.NewScoreCommand.Execute(null);

            Assert.Equal("Untitled Score", main.Document.Title);
            Assert.Equal("New score created", main.StatusMessage);
        }

        [Fact]
        public void HandleSelectionChanged_UpdatesStatusForNoteSelection()
        {
            var main = ViewModelTestHelper.CreateMainViewModel();
            main.Document.EnsureMeasures();

            main.HandleSelectionChanged(new ScoreSelectionInfo
            {
                MeasureIndex = 0,
                NoteIndex = 1
            });

            Assert.Contains("note 2 in measure 1", main.StatusMessage);
        }
    }
}
