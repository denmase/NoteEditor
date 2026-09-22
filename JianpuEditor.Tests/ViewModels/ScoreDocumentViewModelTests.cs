using JianpuEditor.Core.Messaging;
using JianpuEditor.Models;
using JianpuEditor.Services;
using JianpuEditor.ViewModels;
using Xunit;

namespace JianpuEditor.Tests.ViewModels
{
    public class ScoreDocumentViewModelTests
    {
        [Fact]
        public void Title_ChangeMarksDocumentDirty()
        {
            var viewModel = ViewModelTestHelper.CreateDocument();

            viewModel.Title = "Test Title";

            Assert.Equal("Test Title", viewModel.Score.Title);
            Assert.True(viewModel.IsDirty);
        }

        [Fact]
        public void ResetAsNew_ClearsDirtyState()
        {
            var viewModel = ViewModelTestHelper.CreateDocument();
            viewModel.Title = "Dirty";

            viewModel.ResetAsNew();

            Assert.False(viewModel.IsDirty);
            Assert.Null(viewModel.CurrentFilePath);
        }

        [Fact]
        public void LoadDemoScore_LoadsOdeToJoyWithoutDirtyFlag()
        {
            var viewModel = ViewModelTestHelper.CreateDocument();

            viewModel.LoadDemoScore();

            Assert.Equal("Ode to Joy", viewModel.Title);
            Assert.False(viewModel.IsDirty);
            Assert.Equal(8, viewModel.Score.Measures.Count);
        }

        [Fact]
        public void EnsureMeasures_CreatesDefaultMeasureWhenEmpty()
        {
            var viewModel = ViewModelTestHelper.CreateDocument();
            viewModel.Score.Measures.Clear();

            viewModel.EnsureMeasures();

            Assert.Single(viewModel.Score.Measures);
        }

        [Fact]
        public void ApplyInstrumentEdit_UpdatesMelodyAndChordInstrumentsAndSupportsUndo()
        {
            var messenger = ViewModelTestHelper.CreateMessenger();
            var history = ViewModelTestHelper.CreateHistory(messenger);
            var viewModel = ViewModelTestHelper.CreateDocument(messenger, history);

            viewModel.ApplyInstrumentEdit(isChordInstrument: false, newProgram: 40);
            viewModel.ApplyInstrumentEdit(isChordInstrument: true, newProgram: 24);

            Assert.Equal(40, viewModel.MelodyInstrument);
            Assert.Equal(24, viewModel.ChordInstrument);
            Assert.True(history.CanUndo);

            history.Undo();

            Assert.Equal(40, viewModel.MelodyInstrument);
            Assert.Equal(0, viewModel.ChordInstrument);

            history.Undo();

            Assert.Equal(0, viewModel.MelodyInstrument);
        }

        [Fact]
        public void ApplyInstrumentEdit_ClampsOutOfRangeProgramNumbers()
        {
            var viewModel = ViewModelTestHelper.CreateDocument();

            viewModel.ApplyInstrumentEdit(isChordInstrument: false, newProgram: 999);

            Assert.Equal(127, viewModel.MelodyInstrument);
        }

        [Fact]
        public void ApplyInstrumentEdit_NoOpWhenValueUnchanged()
        {
            var messenger = ViewModelTestHelper.CreateMessenger();
            var history = ViewModelTestHelper.CreateHistory(messenger);
            var viewModel = ViewModelTestHelper.CreateDocument(messenger, history);

            viewModel.ApplyInstrumentEdit(isChordInstrument: false, newProgram: 0);

            Assert.False(history.CanUndo);
        }

        [Fact]
        public void ApplyChordPlaybackStyleEdit_UpdatesStyleAndSupportsUndo()
        {
            var messenger = ViewModelTestHelper.CreateMessenger();
            var history = ViewModelTestHelper.CreateHistory(messenger);
            var viewModel = ViewModelTestHelper.CreateDocument(messenger, history);

            viewModel.ApplyChordPlaybackStyleEdit(ChordPlaybackStyle.Arpeggio);

            Assert.Equal(ChordPlaybackStyle.Arpeggio, viewModel.ChordPlaybackStyle);
            Assert.True(history.CanUndo);

            history.Undo();

            Assert.Equal(ChordPlaybackStyle.Block, viewModel.ChordPlaybackStyle);
        }

        [Fact]
        public void ApplyChordPlaybackStyleEdit_NoOpWhenValueUnchanged()
        {
            var messenger = ViewModelTestHelper.CreateMessenger();
            var history = ViewModelTestHelper.CreateHistory(messenger);
            var viewModel = ViewModelTestHelper.CreateDocument(messenger, history);

            viewModel.ApplyChordPlaybackStyleEdit(ChordPlaybackStyle.Block);

            Assert.False(history.CanUndo);
        }
    }
}
