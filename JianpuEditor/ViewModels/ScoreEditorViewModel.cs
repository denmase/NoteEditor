using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Core.Messaging;
using JianpuEditor.Core.Messaging.Messages;
using JianpuEditor.Models;
using JianpuEditor.Services;
using JianpuEditor.Services.EditCommands;

namespace JianpuEditor.ViewModels
{
    public sealed class ScoreEditorViewModel : ObservableObject
    {
        private readonly ScoreDocumentViewModel _document;
        private readonly ScoreSelectionViewModel _selection;
        private readonly MeasureNavigationViewModel _navigation;
        private readonly ChordEditorViewModel _chordEditor;
        private readonly OrnamentEditorViewModel _ornamentEditor;
        private readonly IAppMessenger _messenger;
        private readonly IEditCommandHistory _history;

        public ScoreEditorViewModel(
            ScoreDocumentViewModel document,
            ScoreSelectionViewModel selection,
            MeasureNavigationViewModel navigation,
            ChordEditorViewModel chordEditor,
            OrnamentEditorViewModel ornamentEditor,
            IAppMessenger messenger,
            IEditCommandHistory history)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _chordEditor = chordEditor ?? throw new ArgumentNullException(nameof(chordEditor));
            _ornamentEditor = ornamentEditor ?? throw new ArgumentNullException(nameof(ornamentEditor));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _history = history ?? throw new ArgumentNullException(nameof(history));
            DeleteCommand = new RelayCommand(() => Delete());
            ClearScoreCommand = new RelayCommand(() => ClearScore());
        }

        public RelayCommand DeleteCommand { get; }

        public RelayCommand ClearScoreCommand { get; }

        public ScoreEditResult Delete()
        {
            return EditCommandHelper.Execute(
                _history,
                new ScoreSnapshotEditCommand(_document, _navigation, _messenger, ApplyDelete, "Delete"));
        }

        public ScoreEditResult ClearScore()
        {
            return EditCommandHelper.Execute(
                _history,
                new ScoreSnapshotEditCommand(_document, _navigation, _messenger, ApplyClearScore, "Clear score"));
        }

        private ScoreEditResult ApplyDelete()
        {
            if (_selection.HasTieSelected
                && _document.Score.Ties != null
                && _selection.TieIndex >= 0
                && _selection.TieIndex < _document.Score.Ties.Count)
            {
                _document.Score.Ties.RemoveAt(_selection.TieIndex);
                return new ScoreEditResult
                {
                    Changed = true,
                    Message = "Tie deleted",
                    ClearTieSelection = true
                };
            }

            var chordResult = _chordEditor.TryRemoveSelectedChord();
            if (chordResult.Changed)
            {
                return chordResult;
            }

            var ornamentResult = _ornamentEditor.TryRemoveOrnamentsForSelection();
            if (ornamentResult.Changed)
            {
                return ornamentResult;
            }

            _document.EnsureMeasures();
            var measureIndex = Math.Max(0, _selection.MeasureIndex);
            var measure = _document.Score.Measures[measureIndex];

            if (_selection.HasNoteSelected)
            {
                var refs = GetSelectedNoteRefs();
                if (refs.Count > 0)
                {
                    var removedCount = 0;
                    // Grouped by (measure, voice) -- not measure alone -- since a selection on an
                    // extra voice must never fall into the primary-voice removal path below (ties/
                    // hairpins/ornaments/dynamics are primary-voice concepts; an extra voice's note
                    // is just removed from its own list, see VoiceLayoutService.RemoveNote).
                    foreach (var group in refs.GroupBy(item => new { item.MeasureIndex, item.VoiceIndex }).OrderByDescending(item => item.Key.MeasureIndex))
                    {
                        var groupMeasureIndex = group.Key.MeasureIndex;
                        var groupVoiceIndex = group.Key.VoiceIndex;
                        if (groupMeasureIndex < 0 || groupMeasureIndex >= _document.Score.Measures.Count)
                        {
                            continue;
                        }

                        var targetMeasure = _document.Score.Measures[groupMeasureIndex];
                        var voiceNotes = VoiceLayoutService.GetNotesList(targetMeasure, groupVoiceIndex);
                        foreach (var noteIndex in group.Select(item => item.NoteIndex).Distinct().OrderByDescending(item => item))
                        {
                            if (noteIndex < 0 || noteIndex >= voiceNotes.Count)
                            {
                                continue;
                            }

                            if (groupVoiceIndex == VoiceLayoutService.PrimaryVoiceIndex)
                            {
                                MelodyChordService.RemoveSlot(targetMeasure, noteIndex);
                                TieMaintenanceService.OnNoteRemoved(_document.Score, groupMeasureIndex, noteIndex);
                                HairpinMaintenanceService.OnNoteRemoved(_document.Score, groupMeasureIndex, noteIndex);
                                OrnamentService.OnNoteRemoved(targetMeasure, noteIndex);
                                DynamicMarkingService.OnNoteRemoved(targetMeasure, noteIndex);
                            }
                            else
                            {
                                VoiceLayoutService.RemoveNote(targetMeasure, groupVoiceIndex, noteIndex);
                            }

                            removedCount++;
                        }
                    }

                    if (removedCount > 0)
                    {
                        var message = removedCount > 1
                            ? "Deleted " + removedCount + " selected notes"
                            : "Deleted selected note";
                        return new ScoreEditResult
                        {
                            Changed = true,
                            Message = message,
                            ClearMelodySelection = true,
                            ClearTieSelection = true
                        };
                    }
                }
            }

            if (measure.MelodyNotes.Count > 0)
            {
                var noteIndex = measure.MelodyNotes.Count - 1;
                MelodyChordService.RemoveSlot(measure, noteIndex);
                TieMaintenanceService.OnNoteRemoved(_document.Score, measureIndex, noteIndex);
                HairpinMaintenanceService.OnNoteRemoved(_document.Score, measureIndex, noteIndex);
                OrnamentService.OnNoteRemoved(measure, noteIndex);
                DynamicMarkingService.OnNoteRemoved(measure, noteIndex);
                return new ScoreEditResult
                {
                    Changed = true,
                    Message = "Deleted last note in current measure",
                    ClearMelodySelection = true,
                    ClearTieSelection = true
                };
            }

            if (_document.Score.Measures.Count > 1)
            {
                var removedMeasureIndex = measureIndex;
                TieMaintenanceService.OnMeasureRemoved(_document.Score, removedMeasureIndex);
                VoltaMaintenanceService.OnMeasureRemoved(_document.Score, removedMeasureIndex);
                HairpinMaintenanceService.OnMeasureRemoved(_document.Score, removedMeasureIndex);
                _document.Score.Measures.RemoveAt(removedMeasureIndex);
                var newIndex = Math.Max(0, removedMeasureIndex - 1);
                _navigation.SyncCurrentMeasureIndex(newIndex);
                return new ScoreEditResult
                {
                    Changed = true,
                    Message = "Empty measure deleted",
                    ClearTieSelection = true,
                    SelectMeasureIndex = newIndex
                };
            }

            return ScoreEditResult.Unchanged;
        }

        private ScoreEditResult ApplyClearScore()
        {
            _document.ClearMeasures();
            _navigation.SyncCurrentMeasureIndex(0);
            return new ScoreEditResult
            {
                Changed = true,
                Message = "Score cleared",
                SelectMeasureIndex = 0
            };
        }

        public ScoreEditResult AddVolta(string label)
        {
            return EditCommandHelper.Execute(
                _history,
                new ScoreSnapshotEditCommand(_document, _navigation, _messenger, () => ApplyAddVolta(label), "Add volta bracket"));
        }

        public ScoreEditResult RemoveVolta()
        {
            return EditCommandHelper.Execute(
                _history,
                new ScoreSnapshotEditCommand(_document, _navigation, _messenger, ApplyRemoveVolta, "Remove volta bracket"));
        }

        private ScoreEditResult ApplyAddVolta(string label)
        {
            _document.EnsureMeasures();
            _selection.TryGetContiguousMeasureRange(out var fromIndex, out var toIndex);
            if (!VoltaService.TryAddVolta(_document.Score, fromIndex, toIndex, label, out var message))
            {
                _messenger.Send(new StatusChangedMessage(message));
                return ScoreEditResult.Unchanged;
            }

            return ScoreEditResult.WithMessage(message);
        }

        private ScoreEditResult ApplyRemoveVolta()
        {
            _document.EnsureMeasures();
            var measureIndex = Math.Max(0, _navigation.CurrentMeasureIndex);
            if (!VoltaService.TryRemoveVoltaCovering(_document.Score, measureIndex, out var message))
            {
                _messenger.Send(new StatusChangedMessage(message));
                return ScoreEditResult.Unchanged;
            }

            return ScoreEditResult.WithMessage(message);
        }

        /// <summary>Backs the Edit &gt; Voices &gt; Single/SATB menu preset (see
        /// <see cref="VoiceModeService"/>) -- the minimal, whole-score way to reach the new
        /// multi-voice rendering/playback path.</summary>
        public ScoreEditResult SetVoiceMode(bool useSatb)
        {
            return EditCommandHelper.Execute(
                _history,
                new ScoreSnapshotEditCommand(
                    _document,
                    _navigation,
                    _messenger,
                    () => ApplySetVoiceMode(useSatb),
                    useSatb ? "Switch to SATB voices" : "Switch to single voice"));
        }

        private ScoreEditResult ApplySetVoiceMode(bool useSatb)
        {
            _document.EnsureMeasures();
            var changed = useSatb
                ? VoiceModeService.ApplySatb(_document.Score)
                : VoiceModeService.ApplySingle(_document.Score);
            if (!changed)
            {
                return ScoreEditResult.Unchanged;
            }

            return ScoreEditResult.WithMessage(useSatb ? "Switched to SATB voices" : "Switched to single voice");
        }

        private List<ScoreNoteRef> GetSelectedNoteRefs()
        {
            if (_selection.SelectedNotes != null && _selection.SelectedNotes.Count > 0)
            {
                return _selection.SelectedNotes.ToList();
            }

            if (_selection.NoteIndex >= 0 && _selection.MeasureIndex >= 0)
            {
                return new List<ScoreNoteRef>
                {
                    new ScoreNoteRef(_selection.MeasureIndex, _selection.NoteIndex, _selection.VoiceIndex)
                };
            }

            return new List<ScoreNoteRef>();
        }
    }
}
