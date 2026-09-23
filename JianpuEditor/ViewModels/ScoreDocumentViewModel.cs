using System;
using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Core.Messaging;
using JianpuEditor.Core.Messaging.Messages;
using JianpuEditor.Models;
using JianpuEditor.Services;
using JianpuEditor.Services.EditCommands;

namespace JianpuEditor.ViewModels
{
    public sealed class ScoreDocumentViewModel : ObservableObject
    {
        private readonly IScoreFileService _fileService;
        private readonly IAppMessenger _messenger;
        private readonly IEditCommandHistory _history;
        private JianpuScore _score = new JianpuScore();
        private string _currentFilePath;
        private bool _isDirty;

        public ScoreDocumentViewModel(
            IScoreFileService fileService,
            IAppMessenger messenger,
            IEditCommandHistory history)
        {
            _fileService = fileService ?? new ScoreFileServiceAdapter();
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _history = history ?? throw new ArgumentNullException(nameof(history));
        }

        public JianpuScore Score
        {
            get { return _score; }
            set
            {
                if (ReferenceEquals(_score, value))
                {
                    return;
                }

                _score = value ?? new JianpuScore();
                EnsureMeasures();
                ChordMarkerService.NormalizeScore(_score);
                OnPropertyChanged(nameof(Score));
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(KeySignature));
                OnPropertyChanged(nameof(Tempo));
                OnPropertyChanged(nameof(Bpm));
                OnPropertyChanged(nameof(Composer));
                OnPropertyChanged(nameof(WindowTitle));
                MarkDirty();
            }
        }

        public string Title
        {
            get { return _score.Title; }
            set
            {
                if (_score.Title == value)
                {
                    return;
                }

                _score.Title = value ?? string.Empty;
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(WindowTitle));
                MarkDirty();
            }
        }

        public string KeySignature
        {
            get { return _score.KeySignature; }
            set
            {
                if (_score.KeySignature == value)
                {
                    return;
                }

                _score.KeySignature = value ?? string.Empty;
                OnPropertyChanged(nameof(KeySignature));
                MarkDirty();
            }
        }

        public string Tempo
        {
            get { return _score.Tempo; }
            set
            {
                if (_score.Tempo == value)
                {
                    return;
                }

                _score.Tempo = value ?? string.Empty;
                OnPropertyChanged(nameof(Tempo));
                MarkDirty();
            }
        }

        public string TimeSignature
        {
            get { return _score.TimeSignature; }
            set
            {
                if (_score.TimeSignature == value)
                {
                    return;
                }

                _score.TimeSignature = value ?? string.Empty;
                OnPropertyChanged(nameof(TimeSignature));
                MarkDirty();
            }
        }

        public int Bpm
        {
            get { return _score.Bpm; }
            set
            {
                if (_score.Bpm == value)
                {
                    return;
                }

                _score.Bpm = value;
                OnPropertyChanged(nameof(Bpm));
                MarkDirty();
            }
        }

        public string Composer
        {
            get { return _score.Composer; }
            set
            {
                if (_score.Composer == value)
                {
                    return;
                }

                _score.Composer = value ?? string.Empty;
                OnPropertyChanged(nameof(Composer));
                MarkDirty();
            }
        }

        public int MelodyInstrument
        {
            get { return _score.MelodyInstrument; }
            set
            {
                if (_score.MelodyInstrument == value)
                {
                    return;
                }

                _score.MelodyInstrument = value;
                OnPropertyChanged(nameof(MelodyInstrument));
                MarkDirty();
            }
        }

        public int ChordInstrument
        {
            get { return _score.ChordInstrument; }
            set
            {
                if (_score.ChordInstrument == value)
                {
                    return;
                }

                _score.ChordInstrument = value;
                OnPropertyChanged(nameof(ChordInstrument));
                MarkDirty();
            }
        }

        public ChordPlaybackStyle ChordPlaybackStyle
        {
            get { return _score.ChordPlaybackStyle; }
            set
            {
                if (_score.ChordPlaybackStyle == value)
                {
                    return;
                }

                _score.ChordPlaybackStyle = value;
                OnPropertyChanged(nameof(ChordPlaybackStyle));
                MarkDirty();
            }
        }

        /// <summary>The display label (e.g. "Alto") for each extra-voice channel slot -- see
        /// <see cref="ScoreMidiSchedule.GetExtraVoiceRoleLabels"/>.</summary>
        public IReadOnlyList<string> ExtraVoiceRoleLabels
        {
            get { return ScoreMidiSchedule.GetExtraVoiceRoleLabels(_score); }
        }

        /// <summary>Resolves the General MIDI program for one extra-voice channel slot -- see
        /// <see cref="ScoreMidiSchedule.GetExtraVoiceInstrument"/>.</summary>
        public int GetExtraVoiceInstrument(int voiceIndex)
        {
            return ScoreMidiSchedule.GetExtraVoiceInstrument(_score, voiceIndex);
        }

        /// <summary>Sets one extra-voice channel slot's instrument directly, growing <see
        /// cref="JianpuScore.ExtraVoiceInstruments"/> (padding any newly-created earlier slots
        /// with the current default so they keep sounding the same) as needed. Called by <see
        /// cref="Services.EditCommands.ModifyExtraVoiceInstrumentCommand"/>; use <see
        /// cref="ApplyExtraVoiceInstrumentEdit"/> for an undoable edit.</summary>
        public void SetExtraVoiceInstrument(int voiceIndex, int program)
        {
            if (_score.ExtraVoiceInstruments == null)
            {
                _score.ExtraVoiceInstruments = new List<int>();
            }

            while (_score.ExtraVoiceInstruments.Count <= voiceIndex)
            {
                _score.ExtraVoiceInstruments.Add(ScoreMidiSchedule.DefaultExtraVoiceInstrument);
            }

            _score.ExtraVoiceInstruments[voiceIndex] = program;
            MarkDirty();
        }

        public string CurrentFilePath
        {
            get { return _currentFilePath; }
            private set
            {
                if (SetProperty(ref _currentFilePath, value))
                {
                    OnPropertyChanged(nameof(WindowTitle));
                }
            }
        }

        public string WindowTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_currentFilePath))
                {
                    return "Jianpu Editor - " + Path.GetFileName(_currentFilePath);
                }

                return "Jianpu Editor";
            }
        }

        public bool IsDirty
        {
            get { return _isDirty; }
            private set { SetProperty(ref _isDirty, value); }
        }

        public void EnsureMeasures()
        {
            if (_score.Measures == null || _score.Measures.Count == 0)
            {
                _score.Measures = new List<JianpuMeasure> { new JianpuMeasure() };
            }
        }

        public void LoadFromFile(string path)
        {
            Score = _fileService.Load(path);
            CurrentFilePath = path;
            IsDirty = false;
            _messenger?.Send(new ScoreLoadedMessage(_score, path));
        }

        public void SaveToFile(string path)
        {
            _fileService.Save(_score, path);
            CurrentFilePath = path;
            IsDirty = false;
        }

        public void ResetAsNew()
        {
            _score = new JianpuScore();
            EnsureMeasures();
            ChordMarkerService.NormalizeScore(_score);
            CurrentFilePath = null;
            IsDirty = false;
            OnPropertyChanged(nameof(Score));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(KeySignature));
            OnPropertyChanged(nameof(Tempo));
            OnPropertyChanged(nameof(Bpm));
            OnPropertyChanged(nameof(Composer));
            OnPropertyChanged(nameof(WindowTitle));
            _messenger?.Send(new ScoreLoadedMessage(_score, null));
        }

        public void LoadDemoScore()
        {
            _score = DemoScoreFactory.CreateOdeToJoy();
            CurrentFilePath = null;
            IsDirty = false;
            OnPropertyChanged(nameof(Score));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(KeySignature));
            OnPropertyChanged(nameof(Tempo));
            OnPropertyChanged(nameof(Bpm));
            OnPropertyChanged(nameof(Composer));
            OnPropertyChanged(nameof(WindowTitle));
        }

        public void LoadSample(string path)
        {
            _score = _fileService.Load(path);
            ChordMarkerService.NormalizeScore(_score);
            CurrentFilePath = null;
            IsDirty = false;
            OnPropertyChanged(nameof(Score));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(KeySignature));
            OnPropertyChanged(nameof(Tempo));
            OnPropertyChanged(nameof(Bpm));
            OnPropertyChanged(nameof(Composer));
            OnPropertyChanged(nameof(WindowTitle));
        }

        /// <summary>Session restore for a tab that was still dirty when the app last closed: loads
        /// the autosaved content but keeps <paramref name="originalFilePath"/> (which may itself be
        /// null, for a document that had never been saved) as <see cref="CurrentFilePath"/> and
        /// stays dirty -- a later Save writes back to the original location, standard "recovered
        /// document" editor behavior, unlike <see cref="LoadFromFile"/> which would point
        /// CurrentFilePath at the autosave copy itself and clear the dirty flag.</summary>
        public void RestoreFromAutosave(string autosavePath, string originalFilePath)
        {
            _score = _fileService.Load(autosavePath);
            EnsureMeasures();
            ChordMarkerService.NormalizeScore(_score);
            CurrentFilePath = originalFilePath;
            IsDirty = true;
            _history.Clear();
            OnPropertyChanged(nameof(Score));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(KeySignature));
            OnPropertyChanged(nameof(Tempo));
            OnPropertyChanged(nameof(Bpm));
            OnPropertyChanged(nameof(Composer));
            OnPropertyChanged(nameof(WindowTitle));
            _messenger?.Send(new ScoreLoadedMessage(_score, originalFilePath));
        }

        public void LoadFromMidi(JianpuScore score)
        {
            _score = score ?? new JianpuScore();
            EnsureMeasures();
            ChordMarkerService.NormalizeScore(_score);
            LyricSyllableService.NormalizeScore(_score);
            OrnamentService.NormalizeScore(_score);
            CurrentFilePath = null;
            IsDirty = true;
            _history.Clear();
            OnPropertyChanged(nameof(Score));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(KeySignature));
            OnPropertyChanged(nameof(Tempo));
            OnPropertyChanged(nameof(Bpm));
            OnPropertyChanged(nameof(Composer));
            OnPropertyChanged(nameof(WindowTitle));
            _messenger?.Send(new ScoreLoadedMessage(_score, null));
        }

        public void ClearMeasures()
        {
            EnsureMeasures();
            _score.Measures = new List<JianpuMeasure> { new JianpuMeasure() };
            MarkDirty();
            OnPropertyChanged(nameof(Score));
        }

        public void MarkDirty()
        {
            IsDirty = true;
        }

        public void MarkClean()
        {
            IsDirty = false;
        }

        public void ApplyHeaderFieldEdit(ScoreHeaderField field, string text)
        {
            if (field == ScoreHeaderField.None)
            {
                return;
            }

            var oldStringValue = GetHeaderStringValue(field);
            var oldBpm = Bpm;
            var newStringValue = text ?? string.Empty;
            var newBpm = oldBpm;

            if (field == ScoreHeaderField.Bpm)
            {
                if (!int.TryParse(text?.Trim(), out var parsedBpm))
                {
                    return;
                }

                newBpm = Math.Max(30, Math.Min(300, parsedBpm));
                if (newBpm == oldBpm)
                {
                    return;
                }
            }
            else if (string.Equals(oldStringValue, newStringValue, StringComparison.Ordinal))
            {
                return;
            }

            _history.Execute(new ModifyHeaderFieldCommand(
                this,
                _messenger,
                field,
                oldStringValue,
                newStringValue,
                oldBpm,
                newBpm));
        }

        public void ApplyInstrumentEdit(bool isChordInstrument, int newProgram)
        {
            newProgram = GeneralMidiInstruments.Clamp(newProgram);
            var oldProgram = isChordInstrument ? ChordInstrument : MelodyInstrument;
            if (oldProgram == newProgram)
            {
                return;
            }

            _history.Execute(new ModifyInstrumentCommand(this, _messenger, isChordInstrument, oldProgram, newProgram));
        }

        public void ApplyExtraVoiceInstrumentEdit(int voiceIndex, int newProgram)
        {
            newProgram = GeneralMidiInstruments.Clamp(newProgram);
            var oldProgram = GetExtraVoiceInstrument(voiceIndex);
            if (oldProgram == newProgram)
            {
                return;
            }

            _history.Execute(new ModifyExtraVoiceInstrumentCommand(this, _messenger, voiceIndex, oldProgram, newProgram));
        }

        public void ApplyChordPlaybackStyleEdit(ChordPlaybackStyle newStyle)
        {
            var oldStyle = ChordPlaybackStyle;
            if (oldStyle == newStyle)
            {
                return;
            }

            _history.Execute(new ModifyChordPlaybackStyleCommand(this, _messenger, oldStyle, newStyle));
        }

        private string GetHeaderStringValue(ScoreHeaderField field)
        {
            switch (field)
            {
                case ScoreHeaderField.Title:
                    return Title;
                case ScoreHeaderField.KeySignature:
                    return KeySignature;
                case ScoreHeaderField.TimeSignature:
                    return TimeSignature;
                case ScoreHeaderField.Tempo:
                    return Tempo;
                case ScoreHeaderField.Composer:
                    return Composer;
                default:
                    return string.Empty;
            }
        }
    }
}
