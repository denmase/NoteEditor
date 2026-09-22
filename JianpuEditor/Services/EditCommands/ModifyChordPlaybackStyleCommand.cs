using System;
using JianpuEditor.Core.Messaging;
using JianpuEditor.Core.Messaging.Messages;
using JianpuEditor.Models;
using JianpuEditor.Services.NoteEditCommands;
using JianpuEditor.ViewModels;

namespace JianpuEditor.Services.EditCommands
{
    internal sealed class ModifyChordPlaybackStyleCommand : INoteEditCommand
    {
        private readonly ScoreDocumentViewModel _document;
        private readonly IAppMessenger _messenger;
        private readonly ChordPlaybackStyle _oldStyle;
        private readonly ChordPlaybackStyle _newStyle;

        public ModifyChordPlaybackStyleCommand(
            ScoreDocumentViewModel document,
            IAppMessenger messenger,
            ChordPlaybackStyle oldStyle,
            ChordPlaybackStyle newStyle)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _oldStyle = oldStyle;
            _newStyle = newStyle;
            Description = "Change chord playback style";
        }

        public string Description { get; }

        public ScoreEditResult Result { get; private set; }

        public void Execute()
        {
            Apply(_newStyle, Description);
        }

        public void Undo()
        {
            Apply(_oldStyle, "Undone: " + Description);
        }

        private void Apply(ChordPlaybackStyle style, string message)
        {
            _document.ChordPlaybackStyle = style;
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
