using System;
using JianpuEditor.Core.Messaging;
using JianpuEditor.Core.Messaging.Messages;
using JianpuEditor.Services.NoteEditCommands;
using JianpuEditor.ViewModels;

namespace JianpuEditor.Services.EditCommands
{
    internal sealed class ModifyInstrumentCommand : INoteEditCommand
    {
        private readonly ScoreDocumentViewModel _document;
        private readonly IAppMessenger _messenger;
        private readonly bool _isChordInstrument;
        private readonly int _oldProgram;
        private readonly int _newProgram;

        public ModifyInstrumentCommand(
            ScoreDocumentViewModel document,
            IAppMessenger messenger,
            bool isChordInstrument,
            int oldProgram,
            int newProgram)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _isChordInstrument = isChordInstrument;
            _oldProgram = oldProgram;
            _newProgram = newProgram;
            Description = "Change " + (isChordInstrument ? "chord" : "melody") + " instrument";
        }

        public string Description { get; }

        public ScoreEditResult Result { get; private set; }

        public void Execute()
        {
            Apply(_newProgram, Description);
        }

        public void Undo()
        {
            Apply(_oldProgram, "Undone: " + Description);
        }

        private void Apply(int program, string message)
        {
            if (_isChordInstrument)
            {
                _document.ChordInstrument = program;
            }
            else
            {
                _document.MelodyInstrument = program;
            }

            _messenger.Send(new ScoreEditedMessage(message, stopPlayback: false));
            Result = new ScoreEditResult
            {
                Changed = true,
                Message = message,
                RequiresScoreRefresh = false
            };
        }
    }
}
