using JianpuEditor.Models;
using JianpuEditor.ViewModels;
using Xunit;

namespace JianpuEditor.Tests.ViewModels
{
    public sealed class MeasureContentViewModelBarLineTests
    {
        [Fact]
        public void SetBarLineType_UpdatesCurrentMeasure()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            document.EnsureMeasures();
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var measureContent = new MeasureContentViewModel(document, navigation, messenger, history);

            var result = measureContent.SetBarLineType(BarLineType.Final);

            Assert.True(result.Changed);
            Assert.Equal(BarLineType.Final, document.Score.Measures[0].BarLineType);
        }

        [Fact]
        public void SetBarLineType_SameValue_ReturnsUnchanged()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            document.EnsureMeasures();
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var measureContent = new MeasureContentViewModel(document, navigation, messenger, history);

            var result = measureContent.SetBarLineType(BarLineType.Single);

            Assert.False(result.Changed);
        }

        [Fact]
        public void SetBarLineType_Undo_RestoresPreviousValue()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            document.EnsureMeasures();
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var measureContent = new MeasureContentViewModel(document, navigation, messenger, history);
            measureContent.SetBarLineType(BarLineType.RepeatEnd);

            history.Undo();

            Assert.Equal(BarLineType.Single, document.Score.Measures[0].BarLineType);
        }

        [Fact]
        public void ToggleRepeatStart_TogglesCurrentMeasure()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            document.EnsureMeasures();
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var measureContent = new MeasureContentViewModel(document, navigation, messenger, history);

            var result = measureContent.ToggleRepeatStart();

            Assert.True(result.Changed);
            Assert.True(document.Score.Measures[0].IsRepeatStart);

            measureContent.ToggleRepeatStart();

            Assert.False(document.Score.Measures[0].IsRepeatStart);
        }

        [Fact]
        public void ToggleRepeatStart_Undo_RestoresPreviousValue()
        {
            var (document, selection, messenger, history) = ViewModelTestHelper.CreateDocumentWithSelection();
            document.EnsureMeasures();
            var navigation = ViewModelTestHelper.CreateMeasureNavigation(document, selection, messenger, history);
            var measureContent = new MeasureContentViewModel(document, navigation, messenger, history);
            measureContent.ToggleRepeatStart();

            history.Undo();

            Assert.False(document.Score.Measures[0].IsRepeatStart);
        }
    }
}
