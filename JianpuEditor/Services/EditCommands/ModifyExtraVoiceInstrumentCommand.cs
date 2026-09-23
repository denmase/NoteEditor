using System;
using JianpuEditor.Core.Messaging;
using JianpuEditor.Core.Messaging.Messages;
using JianpuEditor.Services.NoteEditCommands;
using JianpuEditor.ViewModels;

namespace JianpuEditor.Services.EditCommands
{
    internal sealed class ModifyExtraVoiceInstrumentCommand : INoteEditCommand
    {
        private readonly ScoreDocumentViewModel _document;
        private readonly IAppMessenger _messenger;
        private readonly int _voiceIndex;
        private readonly int _oldProgram;
        private readonly int _newProgram;

        public ModifyExtraVoiceInstrumentCommand(
            ScoreDocumentViewModel document,
            IAppMessenger messenger,
            int voiceIndex,
            int oldProgram,
            int newProgram)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _voiceIndex = voiceIndex;
            _oldProgram = oldProgram;
            _newProgram = newProgram;
            Description = "Change voice " + (voiceIndex + 1) + " instrument";
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
            _document.SetExtraVoiceInstrument(_voiceIndex, program);
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
