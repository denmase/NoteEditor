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
using JianpuEditor.Services.NoteEditCommands;

namespace JianpuEditor.ViewModels
{
    public sealed class NoteEditorViewModel : ObservableObject
    {
        private readonly ScoreDocumentViewModel _document;
        private readonly ScoreSelectionViewModel _selection;
        private readonly MeasureNavigationViewModel _navigation;
        private readonly IAppMessenger _messenger;
        private readonly IEditCommandHistory _history;
        private readonly INoteClipboardService _clipboard;
        private JianpuNote _pendingNote = CreateDefaultNote();

        public NoteEditorViewModel(
            ScoreDocumentViewModel document,
            ScoreSelectionViewModel selection,
            MeasureNavigationViewModel navigation,
            IAppMessenger messenger,
            IEditCommandHistory history,
            INoteClipboardService clipboard)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));

            AddNoteCommand = new RelayCommand<int>(pitch => AddNote(pitch));
            AddRestCommand = new RelayCommand(() => AddRest());
            SetOctaveUpCommand = new RelayCommand(() => SetOctave(1));
            SetOctaveDownCommand = new RelayCommand(() => SetOctave(-1));
            ToggleDottedCommand = new RelayCommand(() => ToggleDotted());
            DecreaseDurationCommand = new RelayCommand(() => DecreaseDuration());
            IncreaseDurationCommand = new RelayCommand(() => IncreaseDuration());
            TransposePitchUpCommand = new RelayCommand(() => TransposePitch(1));
            TransposePitchDownCommand = new RelayCommand(() => TransposePitch(-1));
            SplitNoteCommand = new RelayCommand(() => SplitSelectedNotes());
            MergeNotesCommand = new RelayCommand(() => MergeSelectedNotes());
        }

        public RelayCommand<int> AddNoteCommand { get; }

        public RelayCommand AddRestCommand { get; }

        public RelayCommand SetOctaveUpCommand { get; }

        public RelayCommand SetOctaveDownCommand { get; }

        public RelayCommand ToggleDottedCommand { get; }

        public RelayCommand DecreaseDurationCommand { get; }

        public RelayCommand IncreaseDurationCommand { get; }

        public RelayCommand TransposePitchUpCommand { get; }

        public RelayCommand TransposePitchDownCommand { get; }

        public RelayCommand SplitNoteCommand { get; }

        public RelayCommand MergeNotesCommand { get; }

        public JianpuNote PendingNote
        {
            get { return _pendingNote; }
        }

        public ScoreEditResult AddNote(int pitch)
        {
            var selectedNotes = GetSelectedNotes();
            if (selectedNotes.Count > 0)
            {
                var message = selectedNotes.Count > 1
                    ? "Changed " + selectedNotes.Count + " selected notes to " + pitch
                    : "Changed selected note to " + pitch;
                return ExecuteCommand(new ModifyMelodyNotesCommand(
                    _document.Score,
                    _messenger,
                    GetSelectedNoteRefs(),
                    () =>
                    {
                        foreach (var selected in selectedNotes)
                        {
                            selected.Type = NoteType.Note;
                            JianpuPitchCodec.SetAccidentalPitch(selected, selected.Accidental, pitch);
                        }
                    },
                    message));
            }

            var note = ClonePendingNote();
            note.Type = NoteType.Note;
            JianpuPitchCodec.SetAccidentalPitch(note, note.Accidental, pitch);
            return InsertMelodyNote(note, "Inserted note " + pitch);
        }

        public ScoreEditResult AddRest()
        {
            var selectedNotes = GetSelectedNotes();
            if (selectedNotes.Count > 0)
            {
                var message = selectedNotes.Count > 1
                    ? "Changed " + selectedNotes.Count + " selected notes to rests"
                    : "Changed selected note to a rest";
                return ExecuteCommand(new ModifyMelodyNotesCommand(
                    _document.Score,
                    _messenger,
                    GetSelectedNoteRefs(),
                    () =>
                    {
                        foreach (var selected in selectedNotes)
                        {
                            selected.Type = NoteType.Rest;
                            selected.Pitch = 0;
                        }
                    },
                    message));
            }

            var note = ClonePendingNote();
            note.Type = NoteType.Rest;
            note.Pitch = 0;
            return InsertMelodyNote(note, "Inserted rest");
        }

        public ScoreEditResult AppendNote(int pitch, bool copyPreviousNoteStyle = false)
        {
            var note = CreateAppendNote(copyPreviousNoteStyle);
            note.Type = NoteType.Note;
            JianpuPitchCodec.SetAccidentalPitch(note, note.Accidental, pitch);
            var message = copyPreviousNoteStyle
                ? "Appended note " + pitch + " (copied duration/octave from previous note)"
                : "Appended note " + pitch;
            return InsertMelodyNoteAt(GetCurrentMeasureIndex(), note, message);
        }

        public ScoreEditResult AppendRest(bool copyPreviousNoteStyle = false)
        {
            var note = CreateAppendNote(copyPreviousNoteStyle);
            note.Type = NoteType.Rest;
            note.Pitch = 0;
            var message = copyPreviousNoteStyle
                ? "Appended rest (copied duration/octave from previous note)"
                : "Appended rest";
            return InsertMelodyNoteAt(GetCurrentMeasureIndex(), note, message);
        }

        public ScoreEditResult SetOctave(int octave)
        {
            var selectedNotes = GetSelectedNotes();
            if (selectedNotes.Count > 0)
            {
                var message = selectedNotes.Count > 1
                    ? "Changed octave for " + selectedNotes.Count + " selected notes"
                    : "Changed octave for selected note";
                return ExecuteCommand(new ModifyMelodyNotesCommand(
                    _document.Score,
                    _messenger,
                    GetSelectedNoteRefs(),
                    () =>
                    {
                        foreach (var selected in selectedNotes)
                        {
                            selected.Octave = selected.Octave == octave ? 0 : octave;
                        }
                    },
                    message));
            }

            _pendingNote.Octave = _pendingNote.Octave == octave ? 0 : octave;
            var label = _pendingNote.Octave > 0 ? "high octave" : _pendingNote.Octave < 0 ? "low octave" : "middle octave";
            _messenger.Send(new StatusChangedMessage("Current octave: " + label));
            return ScoreEditResult.Unchanged;
        }

        public ScoreEditResult SetAccidental(AccidentalKind accidental)
        {
            var selectedNotes = GetSelectedNotes();
            if (selectedNotes.Count > 0)
            {
                var message = selectedNotes.Count > 1
                    ? "Changed accidental for " + selectedNotes.Count + " selected notes"
                    : "Changed accidental for selected note";
                return ExecuteCommand(new ModifyMelodyNotesCommand(
                    _document.Score,
                    _messenger,
                    GetSelectedNoteRefs(),
                    () =>
                    {
                        foreach (var selected in selectedNotes)
                        {
                            if (selected.Type != NoteType.Note)
                            {
                                continue;
                            }

                            var degree = JianpuPitchCodec.GetDisplayDegree(selected);
                            var newAccidental = selected.Accidental == accidental ? AccidentalKind.None : accidental;
                            JianpuPitchCodec.SetAccidentalPitch(selected, newAccidental, degree);
                        }
                    },
                    message));
            }

            _pendingNote.Accidental = _pendingNote.Accidental == accidental ? AccidentalKind.None : accidental;
            var accidentalLabel = _pendingNote.Accidental == AccidentalKind.None
                ? "no accidental"
                : _pendingNote.Accidental.ToString().ToLowerInvariant();
            _messenger.Send(new StatusChangedMessage("Current accidental: " + accidentalLabel));
            return ScoreEditResult.Unchanged;
        }

        public ScoreEditResult ToggleDotted()
        {
            var selectedNotes = GetSelectedNotes();
            if (selectedNotes.Count > 0)
            {
                var dotted = !selectedNotes[0].Dotted;
                var message = selectedNotes.Count > 1
                    ? (dotted ? "Added dot to " + selectedNotes.Count + " selected notes" : "Removed dot from " + selectedNotes.Count + " selected notes")
                    : (dotted ? "Added dot to selected note" : "Removed dot from selected note");
                return ExecuteCommand(new ModifyMelodyNotesCommand(
                    _document.Score,
                    _messenger,
                    GetSelectedNoteRefs(),
                    () =>
                    {
                        foreach (var selected in selectedNotes)
                        {
                            selected.Dotted = !selected.Dotted;
                        }
                    },
                    message));
            }

            _pendingNote.Dotted = !_pendingNote.Dotted;
            _messenger.Send(new StatusChangedMessage(_pendingNote.Dotted ? "Next note will be dotted" : "Dot removed"));
            return ScoreEditResult.Unchanged;
        }

        public ScoreEditResult DecreaseDuration()
        {
            return StepDuration(-1);
        }

        public ScoreEditResult IncreaseDuration()
        {
            return StepDuration(1);
        }

        public ScoreEditResult SplitSelectedNotes()
        {
            var refs = GetSelectedNoteRefs();
            if (refs.Count == 0)
            {
                _messenger.Send(new StatusChangedMessage("Select a note to split first"));
                return ScoreEditResult.Unchanged;
            }

            _document.EnsureMeasures();
            if (!CanSplitAny(refs))
            {
                return PublishEdit("Cannot split: note is already at the shortest duration or is a rest");
            }

            return ExecuteCommand(new MeasuresMelodySnapshotCommand(
                _document.Score,
                _messenger,
                () => ApplySplitSelectedNotes(refs),
                "Split note"));
        }

        public ScoreEditResult MergeSelectedNotes()
        {
            var refs = GetSelectedNoteRefs();
            if (refs.Count < 2)
            {
                _messenger.Send(new StatusChangedMessage("Select at least two notes to merge"));
                return ScoreEditResult.Unchanged;
            }

            _document.EnsureMeasures();
            if (NoteSplitMergeService.GetMergePairs(refs).Count == 0)
            {
                return PublishEdit("Cannot merge: select at least two notes");
            }

            return ExecuteCommand(new MeasuresMelodySnapshotCommand(
                _document.Score,
                _messenger,
                () => ApplyMergeSelectedNotes(refs),
                "Merge notes"));
        }

        public bool HasClipboardContent
        {
            get { return _clipboard.HasNotes; }
        }

        /// <summary>Copies the selected note(s) to the shared clipboard. Returns false (no-op,
        /// nothing copied) when no note is selected, so callers like Cut know not to follow up
        /// with a delete.</summary>
        public bool CopySelectedNotes()
        {
            var refs = GetOrderedSelectedNoteRefs();
            if (refs.Count == 0)
            {
                _messenger.Send(new StatusChangedMessage("Select a note to copy first"));
                return false;
            }

            _document.EnsureMeasures();
            var notes = new List<JianpuNote>();
            foreach (var noteRef in refs)
            {
                if (noteRef.MeasureIndex < 0 || noteRef.MeasureIndex >= _document.Score.Measures.Count)
                {
                    continue;
                }

                var measureNotes = _document.Score.Measures[noteRef.MeasureIndex].MelodyNotes;
                if (noteRef.NoteIndex < 0 || noteRef.NoteIndex >= measureNotes.Count)
                {
                    continue;
                }

                notes.Add(measureNotes[noteRef.NoteIndex]);
            }

            if (notes.Count == 0)
            {
                return false;
            }

            _clipboard.SetNotes(notes);
            _messenger.Send(new StatusChangedMessage(notes.Count > 1 ? "Copied " + notes.Count + " notes" : "Copied note"));
            return true;
        }

        public ScoreEditResult PasteNotes()
        {
            var clipboardNotes = _clipboard.GetNotes();
            if (clipboardNotes.Count == 0)
            {
                _messenger.Send(new StatusChangedMessage("Nothing to paste"));
                return ScoreEditResult.Unchanged;
            }

            _document.EnsureMeasures();
            var message = clipboardNotes.Count > 1 ? "Pasted " + clipboardNotes.Count + " notes" : "Pasted note";
            return ExecuteCommand(new MeasuresMelodySnapshotCommand(
                _document.Score,
                _messenger,
                () => ApplyPasteNotes(clipboardNotes),
                message));
        }

        public ScoreEditResult TransposePitch(int delta)
        {
            var selectedNotes = GetSelectedNotes();
            if (selectedNotes.Count > 0)
            {
                var changedCount = CountTransposableNotes(selectedNotes, delta);
                if (changedCount == 0)
                {
                    return PublishEdit(delta > 0 ? "Already at the highest pitch" : "Already at the lowest pitch");
                }

                var direction = delta > 0 ? "up" : "down";
                var message = changedCount > 1
                    ? "Transposed " + direction + " key for " + changedCount + " selected notes"
                    : "Transposed " + direction + " key for selected note";
                return ExecuteCommand(new ModifyMelodyNotesCommand(
                    _document.Score,
                    _messenger,
                    GetSelectedNoteRefs(),
                    () =>
                    {
                        foreach (var selected in selectedNotes)
                        {
                            JianpuPitchService.TryTranspose(selected, delta);
                        }
                    },
                    message));
            }

            if (!JianpuPitchService.TryTranspose(_pendingNote, delta))
            {
                _messenger.Send(new StatusChangedMessage(delta > 0 ? "Next note is already at the highest pitch" : "Next note is already at the lowest pitch"));
                return ScoreEditResult.Unchanged;
            }

            _messenger.Send(new StatusChangedMessage(
                "Next note pitch: " + _pendingNote.Pitch +
                (_pendingNote.Octave > 0 ? "·" : _pendingNote.Octave < 0 ? ".." : string.Empty)));
            return ScoreEditResult.Unchanged;
        }

        private ScoreEditResult StepDuration(int delta)
        {
            var selectedNotes = GetSelectedNotes();
            if (selectedNotes.Count > 0)
            {
                var tier = GetDurationTier(selectedNotes[0]);
                var nextTier = Math.Max(MinDurationTier, Math.Min(MaxDurationTier, tier + delta));
                if (nextTier == tier)
                {
                    var limit = delta > 0 ? "Already at the longest duration" : "Already at the shortest duration";
                    return PublishEdit(limit + " (" + GetDurationTierLabel(tier) + ")");
                }

                var message = selectedNotes.Count > 1
                    ? "Duration: " + GetDurationTierLabel(nextTier) + " (" + selectedNotes.Count + " notes)"
                    : "Duration: " + GetDurationTierLabel(nextTier);
                return ExecuteCommand(new ModifyMelodyNotesCommand(
                    _document.Score,
                    _messenger,
                    GetSelectedNoteRefs(),
                    () =>
                    {
                        foreach (var selected in selectedNotes)
                        {
                            ApplyDurationTier(selected, nextTier);
                        }
                    },
                    message));
            }

            var pendingTier = GetDurationTier(_pendingNote);
            var nextPendingTier = Math.Max(MinDurationTier, Math.Min(MaxDurationTier, pendingTier + delta));
            ApplyDurationTier(_pendingNote, nextPendingTier);
            _messenger.Send(new StatusChangedMessage("Next note duration: " + GetDurationTierLabel(nextPendingTier)));
            return ScoreEditResult.Unchanged;
        }

        public void ResetPendingModifiers()
        {
            _pendingNote = CreateDefaultNote();
        }

        private ScoreEditResult InsertMelodyNote(JianpuNote note, string message)
        {
            _document.EnsureMeasures();
            var measureIndex = Math.Max(0, _selection.MeasureIndex);
            var measure = _document.Score.Measures[measureIndex];
            int insertIndex;

            if (_selection.HasGapSelected)
            {
                insertIndex = _selection.InsertIndex;
            }
            else
            {
                insertIndex = measure.MelodyNotes.Count;
            }

            return InsertMelodyNoteAt(measureIndex, insertIndex, note, message);
        }

        private ScoreEditResult InsertMelodyNoteAt(int measureIndex, JianpuNote note, string message)
        {
            _document.EnsureMeasures();
            measureIndex = Math.Max(0, Math.Min(measureIndex, _document.Score.Measures.Count - 1));
            var insertIndex = _document.Score.Measures[measureIndex].MelodyNotes.Count;
            return InsertMelodyNoteAt(measureIndex, insertIndex, note, message);
        }

        private ScoreEditResult InsertMelodyNoteAt(int measureIndex, int insertIndex, JianpuNote note, string message)
        {
            return ExecuteCommand(new InsertMelodyNoteCommand(
                _document.Score,
                _messenger,
                measureIndex,
                insertIndex,
                note,
                message,
                ResetPendingModifiers));
        }

        private int GetCurrentMeasureIndex()
        {
            _document.EnsureMeasures();
            return Math.Max(0, Math.Min(_navigation.CurrentMeasureIndex, _document.Score.Measures.Count - 1));
        }

        private JianpuNote CreateAppendNote(bool copyPreviousNoteStyle)
        {
            _document.EnsureMeasures();
            var measure = _document.Score.Measures[GetCurrentMeasureIndex()];
            if (copyPreviousNoteStyle && measure.MelodyNotes.Count > 0)
            {
                return CloneNoteStyle(measure.MelodyNotes[measure.MelodyNotes.Count - 1]);
            }

            return ClonePendingNote();
        }

        private static JianpuNote CloneNoteStyle(JianpuNote source)
        {
            return new JianpuNote
            {
                Type = source.Type,
                Pitch = source.Pitch,
                Accidental = source.Accidental,
                Octave = source.Octave,
                Underlines = source.Underlines,
                Dashes = source.Dashes,
                Dotted = source.Dotted
            };
        }

        private ScoreEditResult ApplySplitSelectedNotes(IReadOnlyList<ScoreNoteRef> refs)
        {
            _document.EnsureMeasures();
            var splitCount = 0;
            int? selectMeasureIndex = null;
            int? selectNoteIndex = null;

            foreach (var group in refs.GroupBy(item => item.MeasureIndex))
            {
                foreach (var noteRef in group.OrderByDescending(item => item.NoteIndex))
                {
                    var measure = _document.Score.Measures[group.Key];
                    if (noteRef.NoteIndex < 0 || noteRef.NoteIndex >= measure.MelodyNotes.Count)
                    {
                        continue;
                    }

                    var source = measure.MelodyNotes[noteRef.NoteIndex];
                    if (!NoteSplitMergeService.TrySplitNote(source, out var parts))
                    {
                        continue;
                    }

                    NoteSplitMergeService.ReplaceNoteWithMany(
                        _document.Score,
                        group.Key,
                        noteRef.NoteIndex,
                        parts);
                    splitCount++;
                    selectMeasureIndex = group.Key;
                    selectNoteIndex = noteRef.NoteIndex;
                }
            }

            if (splitCount == 0)
            {
                return BuildEditResult("Cannot split: note is already at the shortest duration or is a rest");
            }

            return BuildEditResult(
                splitCount > 1 ? "Split " + splitCount + " notes" : "Split selected note",
                selectMeasureIndex,
                selectNoteIndex);
        }

        private ScoreEditResult ApplyMergeSelectedNotes(IReadOnlyList<ScoreNoteRef> refs)
        {
            _document.EnsureMeasures();
            if (NoteSplitMergeService.GetMergePairs(refs).Count == 0)
            {
                return BuildEditResult("Cannot merge: select at least two notes");
            }

            var mergedCount = NoteSplitMergeService.ApplyMergePairs(_document.Score, refs);
            int? selectMeasureIndex = null;
            int? selectNoteIndex = null;
            if (mergedCount > 0)
            {
                var firstPair = NoteSplitMergeService.GetMergePairs(refs)
                    .Where(item => item.Right.NoteIndex == item.Left.NoteIndex + 1)
                    .OrderBy(item => item.Left.MeasureIndex)
                    .ThenBy(item => item.Left.NoteIndex)
                    .FirstOrDefault();
                if (firstPair.Left.MeasureIndex >= 0)
                {
                    selectMeasureIndex = firstPair.Left.MeasureIndex;
                    selectNoteIndex = firstPair.Left.NoteIndex;
                }
            }

            if (mergedCount == 0)
            {
                return BuildEditResult("Cannot merge: paired notes are not adjacent, differ in duration by more than 2x, or have incompatible types");
            }

            return BuildEditResult(
                mergedCount > 1 ? "Merged " + mergedCount + " note pairs" : "Merged selected notes",
                selectMeasureIndex,
                selectNoteIndex);
        }

        private ScoreEditResult ExecuteCommand(INoteEditCommand command)
        {
            _history.Execute(command);
            return command.Result ?? ScoreEditResult.Unchanged;
        }

        private bool CanSplitAny(IReadOnlyList<ScoreNoteRef> refs)
        {
            for (var i = 0; i < refs.Count; i++)
            {
                var noteRef = refs[i];
                if (noteRef.MeasureIndex < 0 || noteRef.MeasureIndex >= _document.Score.Measures.Count)
                {
                    continue;
                }

                var measure = _document.Score.Measures[noteRef.MeasureIndex];
                if (noteRef.NoteIndex < 0 || noteRef.NoteIndex >= measure.MelodyNotes.Count)
                {
                    continue;
                }

                if (NoteSplitMergeService.TrySplitNote(measure.MelodyNotes[noteRef.NoteIndex], out _))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountTransposableNotes(IReadOnlyList<JianpuNote> notes, int delta)
        {
            var changedCount = 0;
            for (var i = 0; i < notes.Count; i++)
            {
                var selected = notes[i];
                if (selected.Type == NoteType.Rest)
                {
                    continue;
                }

                if (JianpuPitchCodec.IsNatural(selected.Pitch)
                    && selected.Accidental == AccidentalKind.None
                    && JianpuPitchService.TryGetTransposedPitch(
                        (int)Math.Round(selected.Pitch),
                        selected.Octave,
                        delta,
                        out _,
                        out _))
                {
                    changedCount++;
                }
            }

            return changedCount;
        }

        private static ScoreEditResult BuildEditResult(
            string message,
            int? selectMeasureIndex = null,
            int? selectNoteIndex = null)
        {
            return new ScoreEditResult
            {
                Changed = true,
                Message = message,
                SelectNoteMeasureIndex = selectMeasureIndex,
                SelectNoteIndex = selectNoteIndex
            };
        }

        private ScoreEditResult ApplyPasteNotes(IReadOnlyList<JianpuNote> clipboardNotes)
        {
            var (measureIndex, insertIndex) = ResolvePasteInsertPoint();
            if (measureIndex < 0 || measureIndex >= _document.Score.Measures.Count)
            {
                return ScoreEditResult.Unchanged;
            }

            var measure = _document.Score.Measures[measureIndex];
            for (var i = 0; i < clipboardNotes.Count; i++)
            {
                MelodyChordService.InsertSlot(measure, insertIndex + i, clipboardNotes[i]);
            }

            return new ScoreEditResult
            {
                Changed = true,
                SelectNoteMeasureIndex = measureIndex,
                SelectNoteIndex = insertIndex + clipboardNotes.Count - 1
            };
        }

        /// <summary>Pastes into the selected gap, right after the last selected note (in
        /// ascending measure/note order), or at the end of the current measure if nothing more
        /// specific is selected -- mirrors <see cref="InsertMelodyNote"/>'s own fallback.</summary>
        private (int measureIndex, int insertIndex) ResolvePasteInsertPoint()
        {
            if (_selection.HasGapSelected)
            {
                var gapMeasureIndex = Math.Max(0, _selection.MeasureIndex);
                return (gapMeasureIndex, _selection.InsertIndex);
            }

            var refs = GetOrderedSelectedNoteRefs();
            if (refs.Count > 0)
            {
                var last = refs[refs.Count - 1];
                return (last.MeasureIndex, last.NoteIndex + 1);
            }

            var measureIndex = GetCurrentMeasureIndex();
            return (measureIndex, _document.Score.Measures[measureIndex].MelodyNotes.Count);
        }

        private List<ScoreNoteRef> GetOrderedSelectedNoteRefs()
        {
            var refs = GetSelectedNoteRefs();
            refs.Sort((a, b) => ScoreNoteRef.Compare(a, b));
            return refs;
        }

        private List<ScoreNoteRef> GetSelectedNoteRefs()
        {
            var result = new List<ScoreNoteRef>();
            if (!_selection.HasNoteSelected)
            {
                return result;
            }

            if (_selection.SelectedNotes != null && _selection.SelectedNotes.Count > 0)
            {
                result.AddRange(_selection.SelectedNotes);
                return result;
            }

            if (_selection.NoteIndex >= 0 && _selection.MeasureIndex >= 0)
            {
                result.Add(new ScoreNoteRef(_selection.MeasureIndex, _selection.NoteIndex));
            }

            return result;
        }

        private List<JianpuNote> GetSelectedNotes()
        {
            var result = new List<JianpuNote>();
            if (!_selection.HasNoteSelected)
            {
                return result;
            }

            _document.EnsureMeasures();
            if (_selection.SelectedNotes != null && _selection.SelectedNotes.Count > 0)
            {
                foreach (var selected in _selection.SelectedNotes)
                {
                    if (selected.MeasureIndex < 0 || selected.MeasureIndex >= _document.Score.Measures.Count)
                    {
                        continue;
                    }

                    var notes = _document.Score.Measures[selected.MeasureIndex].MelodyNotes;
                    if (selected.NoteIndex < 0 || selected.NoteIndex >= notes.Count)
                    {
                        continue;
                    }

                    result.Add(notes[selected.NoteIndex]);
                }

                return result;
            }

            var measureIndex = _selection.MeasureIndex;
            if (measureIndex < 0 || measureIndex >= _document.Score.Measures.Count)
            {
                return result;
            }

            var melodyNotes = _document.Score.Measures[measureIndex].MelodyNotes;
            if (_selection.NoteIndex < 0 || _selection.NoteIndex >= melodyNotes.Count)
            {
                return result;
            }

            result.Add(melodyNotes[_selection.NoteIndex]);
            return result;
        }

        private JianpuNote ClonePendingNote()
        {
            return new JianpuNote
            {
                Type = _pendingNote.Type,
                Pitch = _pendingNote.Pitch,
                Accidental = _pendingNote.Accidental,
                Octave = _pendingNote.Octave,
                Underlines = _pendingNote.Underlines,
                Dashes = _pendingNote.Dashes,
                Dotted = _pendingNote.Dotted
            };
        }

        private static JianpuNote CreateDefaultNote()
        {
            return new JianpuNote
            {
                Type = NoteType.Note,
                Pitch = 1,
                Accidental = AccidentalKind.None,
                Octave = 0,
                Underlines = 0,
                Dashes = 0,
                Dotted = false
            };
        }

        private const int MinDurationTier = 0;
        private const int MaxDurationTier = 5;

        internal static int GetDurationTier(JianpuNote note)
        {
            if (note == null)
            {
                return 2;
            }

            if (note.Dashes > 0)
            {
                return Math.Min(MaxDurationTier, 2 + note.Dashes);
            }

            return Math.Max(MinDurationTier, 2 - Math.Min(2, note.Underlines));
        }

        internal static void ApplyDurationTier(JianpuNote note, int tier)
        {
            if (note == null)
            {
                return;
            }

            tier = Math.Max(MinDurationTier, Math.Min(MaxDurationTier, tier));
            if (tier <= 2)
            {
                note.Dashes = 0;
                note.Underlines = 2 - tier;
                return;
            }

            note.Underlines = 0;
            note.Dashes = tier - 2;
        }

        private static string GetDurationTierLabel(int tier)
        {
            switch (tier)
            {
                case 0:
                    return "1/16";
                case 1:
                    return "1/8";
                case 3:
                    return "+1 beat";
                case 4:
                    return "+2 beats";
                case 5:
                    return "+3 beats";
                default:
                    return "1/4";
            }
        }

        private ScoreEditResult PublishEdit(string message)
        {
            _messenger.Send(new ScoreEditedMessage(message));
            return ScoreEditResult.WithMessage(message);
        }
    }
}
