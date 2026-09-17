using System;
using System.ComponentModel;
using System.Windows.Forms;
using JianpuEditor.Controls;
using JianpuEditor.Models;
using JianpuEditor.Services;
using JianpuEditor.ViewModels;

namespace JianpuEditor.Glue
{
    internal sealed class MainFormViewBinder : IDisposable
    {
        private readonly MainViewModel _viewModel;
        private readonly Form _form;
        private readonly TextBox _chordBox;
        private readonly NumericUpDown _measureSelector;
        private readonly NumericUpDown _measureRangeFrom;
        private readonly NumericUpDown _measureRangeTo;
        private readonly ScoreStatusBar _statusBar;
        private readonly RibbonButton _tieButton;
        private readonly RibbonButton _playButton;
        private readonly RibbonButton _stopButton;
        private bool _suppressMeasureTextSync;
        private bool _suppressMeasureRangeSync;
        private bool _suppressMeasureSelectorSync;

        public MainFormViewBinder(
            MainViewModel viewModel,
            Form form,
            TextBox chordBox,
            NumericUpDown measureSelector,
            NumericUpDown measureRangeFrom,
            NumericUpDown measureRangeTo,
            ScoreStatusBar statusBar,
            RibbonButton tieButton,
            RibbonButton playButton,
            RibbonButton stopButton)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _form = form ?? throw new ArgumentNullException(nameof(form));
            _chordBox = chordBox;
            _measureSelector = measureSelector;
            _measureRangeFrom = measureRangeFrom;
            _measureRangeTo = measureRangeTo;
            _statusBar = statusBar;
            _tieButton = tieButton;
            _playButton = playButton;
            _stopButton = stopButton;

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.Document.PropertyChanged += OnDocumentPropertyChanged;
            _viewModel.TieEditor.PropertyChanged += OnTieEditorPropertyChanged;
            _viewModel.Playback.PropertyChanged += OnPlaybackPropertyChanged;
            _viewModel.ChordEditor.PropertyChanged += OnChordEditorPropertyChanged;

            SyncHeaderFromDocument();
            SyncFromViewModels();
            UpdatePlaybackButtons();
            UpdateTieButton();
        }

        public bool SuppressMeasureTextSync
        {
            get { return _suppressMeasureTextSync; }
        }

        public bool SuppressMeasureRangeSync
        {
            get { return _suppressMeasureRangeSync; }
        }

        public bool SuppressMeasureSelectorSync
        {
            get { return _suppressMeasureSelectorSync; }
        }

        public void SyncHeaderFromDocument()
        {
            _form.Text = _viewModel.Document.WindowTitle;
        }

        public void SyncFromViewModels()
        {
            _statusBar.SetMessage(_viewModel.StatusMessage);
            SyncMeasureControls();
            SyncMeasureTextBoxes();
            UpdatePlaybackButtons();
            UpdateTieButton();
        }

        public void SyncMeasureControls()
        {
            var score = _viewModel.Document.Score;
            _statusBar.SetKeySignature(score?.KeySignature ?? string.Empty);
            _statusBar.SetTempo(score != null ? score.Tempo + " · BPM " + score.Bpm : string.Empty);

            var measureCount = Math.Max(1, _viewModel.MeasureNavigation.MeasureCount);
            _suppressMeasureSelectorSync = true;
            _suppressMeasureRangeSync = true;
            _measureSelector.Maximum = measureCount;
            _measureRangeFrom.Maximum = measureCount;
            _measureRangeTo.Maximum = measureCount;

            var currentIndex = Math.Max(0, Math.Min(
                _viewModel.MeasureNavigation.CurrentMeasureIndex,
                measureCount - 1));
            _measureSelector.Value = Math.Max(1, Math.Min(measureCount, currentIndex + 1));
            _statusBar.SetMeasure(currentIndex + 1, measureCount, GetBeatsWarning(score, currentIndex));

            var range = _viewModel.Selection.GetMeasureRangeIndices();
            var fromIndex = Math.Max(0, Math.Min(range.fromIndex, measureCount - 1));
            var toIndex = Math.Max(0, Math.Min(range.toIndex, measureCount - 1));
            _measureRangeFrom.Value = Math.Max(1, Math.Min(measureCount, fromIndex + 1));
            _measureRangeTo.Value = Math.Max(1, Math.Min(measureCount, toIndex + 1));
            _suppressMeasureRangeSync = false;
            _suppressMeasureSelectorSync = false;
        }

        /// <summary>Null when the current measure's note content matches what the score's
        /// time signature implies (or the time signature can't be parsed, or there's no
        /// current measure) -- otherwise "actual/expected beats" for the status bar.</summary>
        private static string GetBeatsWarning(JianpuScore score, int measureIndex)
        {
            if (score?.Measures == null || measureIndex < 0 || measureIndex >= score.Measures.Count)
            {
                return null;
            }

            if (!TimeSignatureService.TryGetQuarterBeatsPerMeasure(score.TimeSignature, out var expectedBeats))
            {
                return null;
            }

            var actualBeats = MelodyChordService.GetMeasureDurationUnits(score.Measures[measureIndex]);
            if (Math.Abs(actualBeats - expectedBeats) <= 0.02)
            {
                return null;
            }

            return actualBeats.ToString("0.##") + "/" + expectedBeats.ToString("0.##") + " beats";
        }

        public void SyncMeasureTextBoxes()
        {
            _suppressMeasureTextSync = true;
            _chordBox.Text = _viewModel.ChordEditor.SelectedChordText ?? string.Empty;
            _chordBox.Enabled = _viewModel.ChordEditor.IsChordEditorEnabled;
            _suppressMeasureTextSync = false;
        }

        public void Dispose()
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Document.PropertyChanged -= OnDocumentPropertyChanged;
            _viewModel.TieEditor.PropertyChanged -= OnTieEditorPropertyChanged;
            _viewModel.Playback.PropertyChanged -= OnPlaybackPropertyChanged;
            _viewModel.ChordEditor.PropertyChanged -= OnChordEditorPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.StatusMessage))
            {
                _statusBar.SetMessage(_viewModel.StatusMessage);
            }
        }

        private void OnDocumentPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ScoreDocumentViewModel.WindowTitle)
                || e.PropertyName == nameof(ScoreDocumentViewModel.Title))
            {
                SyncHeaderFromDocument();
            }
            else if (e.PropertyName == nameof(ScoreDocumentViewModel.Score))
            {
                SyncMeasureControls();
            }
        }

        private void OnTieEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TieEditorViewModel.IsTieModeActive))
            {
                UpdateTieButton();
            }
        }

        private void OnPlaybackPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlaybackViewModel.IsPlaying))
            {
                UpdatePlaybackButtons();
            }
        }

        private void OnChordEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChordEditorViewModel.SelectedChordText)
                || e.PropertyName == nameof(ChordEditorViewModel.IsChordEditorEnabled))
            {
                if (!_suppressMeasureTextSync)
                {
                    SyncMeasureTextBoxes();
                }
            }
        }

        private void UpdateTieButton()
        {
            if (_tieButton == null)
            {
                return;
            }

            _tieButton.IsActive = _viewModel.TieEditor.IsTieModeActive;
        }

        private void UpdatePlaybackButtons()
        {
            if (_playButton == null || _stopButton == null)
            {
                return;
            }

            _playButton.Enabled = !_viewModel.Playback.IsPlaying;
            _stopButton.Enabled = _viewModel.Playback.IsPlaying;
        }
    }
}
