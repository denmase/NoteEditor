using System;
using JianpuEditor.Core.Messaging;
using JianpuEditor.Core.Messaging.Messages;
using JianpuEditor.Models;
using JianpuEditor.Services.NoteEditCommands;
using JianpuEditor.ViewModels;

namespace JianpuEditor.Services.EditCommands
{
    internal sealed class ModifyRepeatStartCommand : INoteEditCommand
    {
        private readonly JianpuScore _score;
        private readonly IAppMessenger _messenger;
        private readonly int _measureIndex;
        private readonly bool _oldValue;
        private readonly bool _newValue;

        public ModifyRepeatStartCommand(
            JianpuScore score,
            IAppMessenger messenger,
            int measureIndex,
            bool oldValue,
            bool newValue)
        {
            if (score == null)
            {
                throw new ArgumentNullException(nameof(score));
            }

            if (messenger == null)
            {
                throw new ArgumentNullException(nameof(messenger));
            }

            _score = score;
            _messenger = messenger;
            _measureIndex = measureIndex;
            _oldValue = oldValue;
            _newValue = newValue;
            Description = "Toggle repeat start";
        }

        public string Description { get; }

        public ScoreEditResult Result { get; private set; }

        public void Execute()
        {
            Apply(_newValue, "Repeat start updated");
        }

        public void Undo()
        {
            Apply(_oldValue, "Undone: " + Description);
        }

        private void Apply(bool value, string message)
        {
            if (_measureIndex < 0 || _measureIndex >= _score.Measures.Count)
            {
                return;
            }

            _score.Measures[_measureIndex].IsRepeatStart = value;
            _messenger.Send(new ScoreEditedMessage(message, markDirty: true));
            Result = ScoreEditResult.WithMessage(message);
        }
    }
}
