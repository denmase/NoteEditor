using JianpuEditor.Services;
using JianpuEditor.Services.EditCommands;
using JianpuEditor.Tests.Helpers;
using JianpuEditor.Tests.ViewModels;
using JianpuEditor.ViewModels;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public sealed class BulkLyricEditServiceTests
    {
        [Fact]
        public void GetLyricLines_ReturnsTextsForRange()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.MeasureWithLyrics("First", new[] { "First" }, new[] { 0 }, ScoreTestHelper.Note(1), ScoreTestHelper.Note(2)),
                ScoreTestHelper.MeasureWithLyrics("Second", new[] { "Second" }, new[] { 0 }, ScoreTestHelper.Note(1), ScoreTestHelper.Note(2)));

            var lines = BulkLyricEditService.GetLyricLines(score, 0, 1);

            Assert.Equal(2, lines.Count);
            Assert.Equal("First", lines[0]);
            Assert.Equal("Second", lines[1]);
        }

        [Fact]
        public void BuildCommands_TextChanges_CreateModifyCommandsOnly()
        {
            var messenger = ViewModelTestHelper.CreateMessenger();
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2)),
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2)));
            score.Measures[0].LyricText = "Old one";
            score.Measures[1].LyricText = "Old two";

            var commands = BulkLyricEditService.BuildCommands(
                score,
                messenger,
                0,
                1,
                new[] { "New one", "Old two" },
                realign: false);

            Assert.Single(commands);
            Assert.IsType<ModifyLyricTextCommand>(commands[0]);
        }

        [Fact]
        public void BuildCommands_WithRealign_AddsAlignmentCommands()
        {
            var messenger = ViewModelTestHelper.CreateMessenger();
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4)));
            score.Measures[0].LyricText = "Hello World";

            var commands = BulkLyricEditService.BuildCommands(
                score,
                messenger,
                0,
                0,
                new[] { "Hello World" },
                realign: true);

            Assert.Single(commands);
            Assert.IsType<AlignLyricSyllablesCommand>(commands[0]);
        }

        [Fact]
        public void BuildCommands_NoChanges_ReturnsEmpty()
        {
            var messenger = ViewModelTestHelper.CreateMessenger();
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));
            score.Measures[0].LyricText = "Unchanged";

            var commands = BulkLyricEditService.BuildCommands(
                score,
                messenger,
                0,
                0,
                new[] { "Unchanged" },
                realign: false);

            Assert.Empty(commands);
        }

        [Fact]
        public void ApplyBulkLyrics_UpdatesMultipleMeasuresAndSupportsUndo()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var content = new MeasureContentViewModel(document, navigation, messenger, history);
            document.EnsureMeasures();
            document.Score.Measures.Clear();
            document.Score.Measures.Add(ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2)));
            document.Score.Measures.Add(ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2)));
            document.Score.Measures[0].LyricText = "Old one";
            document.Score.Measures[1].LyricText = "Old two";

            var result = content.ApplyBulkLyrics(0, 1, new[] { "New one", "New two" }, realign: false);

            Assert.True(result.Changed);
            Assert.Equal("New one", document.Score.Measures[0].LyricText);
            Assert.Equal("New two", document.Score.Measures[1].LyricText);
            Assert.True(history.CanUndo);

            history.Undo();

            Assert.Equal("Old one", document.Score.Measures[0].LyricText);
            Assert.Equal("Old two", document.Score.Measures[1].LyricText);
        }

        [Fact]
        public void ApplyBulkLyrics_UnchangedWhenNoEdits()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var content = new MeasureContentViewModel(document, navigation, messenger, history);
            document.EnsureMeasures();
            document.Score.Measures[0].LyricText = "Same";

            var result = content.ApplyBulkLyrics(0, 0, new[] { "Same" }, realign: false);

            Assert.False(result.Changed);
            Assert.False(history.CanUndo);
        }
    }
}
