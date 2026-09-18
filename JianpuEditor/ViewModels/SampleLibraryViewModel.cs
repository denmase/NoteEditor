using System;
using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Core.Messaging;
using JianpuEditor.Core.Messaging.Messages;
using JianpuEditor.Services;

namespace JianpuEditor.ViewModels
{
    /// <summary>
    /// Lists the bundled sample scores and applies one to a caller-supplied document. This is a
    /// process-wide singleton (the sample list on disk isn't per-document), so it deliberately
    /// does *not* hold a document/messenger of its own the way it used to -- doing so would
    /// permanently capture whichever document happened to exist when this was first constructed
    /// once documents become per-tab (each tab gets its own scoped <see cref="ScoreDocumentViewModel"/>
    /// and <see cref="IAppMessenger"/>). The caller (today, <see cref="MainViewModel"/>'s own
    /// Document/messenger; eventually, whichever tab is being populated) passes both in per call.
    /// </summary>
    public sealed class SampleLibraryViewModel : ObservableObject
    {
        private readonly ISampleLibraryService _sampleLibraryService;
        private IReadOnlyList<string> _samples = Array.Empty<string>();

        public SampleLibraryViewModel(ISampleLibraryService sampleLibraryService)
        {
            _sampleLibraryService = sampleLibraryService ?? throw new ArgumentNullException(nameof(sampleLibraryService));
            RefreshSamplesCommand = new RelayCommand(RefreshSamples);
            RefreshSamples();
        }

        public IReadOnlyList<string> Samples
        {
            get { return _samples; }
            private set { SetProperty(ref _samples, value); }
        }

        public bool HasSamples
        {
            get { return Samples != null && Samples.Count > 0; }
        }

        public RelayCommand RefreshSamplesCommand { get; }

        public void RefreshSamples()
        {
            Samples = _sampleLibraryService.ListSampleFiles();
            OnPropertyChanged(nameof(HasSamples));
        }

        public ScoreEditResult LoadDemoScore(ScoreDocumentViewModel document, IAppMessenger messenger)
        {
            document.LoadDemoScore();
            var message = "Loaded sample score \"Ode to Joy\"";
            messenger.Send(new ScoreEditedMessage(message, markDirty: false));
            messenger.Send(new ScoreLoadedMessage(document.Score, null));
            return new ScoreEditResult
            {
                Changed = true,
                Message = message,
                SelectMeasureIndex = 0,
                RequiresScoreRefresh = true
            };
        }

        public ScoreEditResult LoadSample(ScoreDocumentViewModel document, IAppMessenger messenger, string path)
        {
            try
            {
                document.LoadSample(path);
                var message = "Loaded sample score: " + document.Score.Title;
                messenger.Send(new ScoreEditedMessage(message, markDirty: false));
                messenger.Send(new ScoreLoadedMessage(document.Score, path));
                return new ScoreEditResult
                {
                    Changed = true,
                    Message = message,
                    SelectMeasureIndex = 0,
                    RequiresScoreRefresh = true
                };
            }
            catch (Exception ex)
            {
                AppLog.Exception("Failed to load sample score: " + path, ex);
                throw;
            }
        }

        public string GetDisplayName(string path)
        {
            return _sampleLibraryService.GetDisplayName(path);
        }

        public string BuildWindowTitle(ScoreDocumentViewModel document, string samplePath)
        {
            if (!string.IsNullOrWhiteSpace(document.CurrentFilePath))
            {
                return "Jianpu Editor - " + Path.GetFileName(document.CurrentFilePath);
            }

            if (!string.IsNullOrWhiteSpace(samplePath))
            {
                return "Jianpu Editor - " + _sampleLibraryService.GetDisplayName(samplePath);
            }

            return "Jianpu Editor";
        }
    }
}
