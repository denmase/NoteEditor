using System;
using System.Collections.Generic;
using System.Linq;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Core.Messaging;
using JianpuEditor.Core.Messaging.Messages;
using JianpuEditor.Models;
using JianpuEditor.Services;
using JianpuEditor.Services.EditCommands;

namespace JianpuEditor.ViewModels
{
    public sealed class DynamicsEditorViewModel
    {
        private readonly ScoreDocumentViewModel _document;
        private readonly MeasureNavigationViewModel _navigation;
        private readonly ScoreSelectionViewModel _selection;
        private readonly IEditCommandHistory _history;
        private readonly IAppMessenger _messenger;

        public DynamicsEditorViewModel(
            ScoreDocumentViewModel document,
            MeasureNavigationViewModel navigation,
            ScoreSelectionViewModel selection,
            IEditCommandHistory history,
            IAppMessenger messenger)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        }

        /// <summary>Applies a dynamic marking to the currently selected note, or removes it if
        /// that note already carries the same marking (mirroring the toggle-on-repeat-click pattern
        /// the ornament buttons already use). Only the single selected note is affected -- unlike
        /// ornaments, this doesn't batch across a multi-note selection in this first version.</summary>
        public ScoreEditResult SetDynamic(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return ScoreEditResult.Unchanged;
            }

            if (!_selection.HasNoteSelected || _selection.MeasureIndex < 0 || _selection.NoteIndex < 0)
            {
                _messenger.Send(new StatusChangedMessage("Select a note first"));
                return ScoreEditResult.Unchanged;
            }

            var measureIndex = _selection.MeasureIndex;
            var noteIndex = _selection.NoteIndex;
            return EditCommandHelper.Execute(
                _history,
                new ScoreSnapshotEditCommand(
                    _document,
                    _navigation,
                    _messenger,
                    () => ApplySetDynamic(measureIndex, noteIndex, text),
                    "Set dynamic"));
        }

        private ScoreEditResult ApplySetDynamic(int measureIndex, int noteIndex, string text)
        {
            if (measureIndex < 0 || measureIndex >= _document.Score.Measures.Count)
            {
                return ScoreEditResult.Unchanged;
            }

            var measure = _document.Score.Measures[measureIndex];
            DynamicMarkingService.NormalizeMeasure(measure);
            var existing = DynamicMarkingService.GetMarkingForNote(measure, noteIndex);
            if (existing != null && string.Equals(existing.Text, text, StringComparison.Ordinal))
            {
                DynamicMarkingService.TryRemoveForNote(measure, noteIndex);
                return ScoreEditResult.WithMessage("Removed dynamic '" + text + "'");
            }

            if (!DynamicMarkingService.TrySetForNote(measure, noteIndex, text))
            {
                return ScoreEditResult.Unchanged;
            }

            return ScoreEditResult.WithMessage("Set dynamic to '" + text + "'");
        }

        /// <summary>Adds a crescendo/diminuendo hairpin spanning the current selection: the
        /// earliest selected note is the start, the latest is the end (mirroring how
        /// <c>ScoreEditorViewModel.AddVolta</c> uses the current selection's range instead of a
        /// separate two-click "pick start, then pick end" mode). A single selected note isn't
        /// enough -- a hairpin needs two distinct points to span.</summary>
        public ScoreEditResult AddHairpin(bool isCrescendo)
        {
            var refs = GetSelectedNoteRefs();
            if (refs.Count < 2)
            {
                _messenger.Send(new StatusChangedMessage("Select at least two notes (the start and end of the hairpin)"));
                return ScoreEditResult.Unchanged;
            }

            var start = refs[0];
            var end = refs[refs.Count - 1];
            return EditCommandHelper.Execute(
                _history,
                new ScoreSnapshotEditCommand(
                    _document,
                    _navigation,
                    _messenger,
                    () => ApplyAddHairpin(start, end, isCrescendo),
                    isCrescendo ? "Add crescendo" : "Add diminuendo"));
        }

        private ScoreEditResult ApplyAddHairpin(ScoreNoteRef start, ScoreNoteRef end, bool isCrescendo)
        {
            if (!HairpinService.TryAddHairpin(
                _document.Score,
                start.MeasureIndex,
                start.NoteIndex,
                end.MeasureIndex,
                end.NoteIndex,
                isCrescendo,
                out var message))
            {
                _messenger.Send(new StatusChangedMessage(message));
                return ScoreEditResult.Unchanged;
            }

            return ScoreEditResult.WithMessage(message);
        }

        public ScoreEditResult RemoveHairpin()
        {
            if (!_selection.HasNoteSelected || _selection.MeasureIndex < 0 || _selection.NoteIndex < 0)
            {
                _messenger.Send(new StatusChangedMessage("Select a note first"));
                return ScoreEditResult.Unchanged;
            }

            var measureIndex = _selection.MeasureIndex;
            var noteIndex = _selection.NoteIndex;
            return EditCommandHelper.Execute(
                _history,
                new ScoreSnapshotEditCommand(
                    _document,
                    _navigation,
                    _messenger,
                    () => ApplyRemoveHairpin(measureIndex, noteIndex),
                    "Remove hairpin"));
        }

        private ScoreEditResult ApplyRemoveHairpin(int measureIndex, int noteIndex)
        {
            if (!HairpinService.TryRemoveHairpinCovering(_document.Score, measureIndex, noteIndex, out var message))
            {
                _messenger.Send(new StatusChangedMessage(message));
                return ScoreEditResult.Unchanged;
            }

            return ScoreEditResult.WithMessage(message);
        }

        private List<ScoreNoteRef> GetSelectedNoteRefs()
        {
            List<ScoreNoteRef> refs;
            if (_selection.SelectedNotes != null && _selection.SelectedNotes.Count > 0)
            {
                refs = _selection.SelectedNotes.ToList();
            }
            else if (_selection.NoteIndex >= 0 && _selection.MeasureIndex >= 0)
            {
                refs = new List<ScoreNoteRef> { new ScoreNoteRef(_selection.MeasureIndex, _selection.NoteIndex) };
            }
            else
            {
                return new List<ScoreNoteRef>();
            }

            refs.Sort(ScoreNoteRef.Compare);
            return refs;
        }
    }
}
