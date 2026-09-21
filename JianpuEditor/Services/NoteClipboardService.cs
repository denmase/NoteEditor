using System.Collections.Generic;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Models;

namespace JianpuEditor.Services
{
    internal sealed class NoteClipboardService : INoteClipboardService
    {
        private List<JianpuNote> _notes = new List<JianpuNote>();

        public bool HasNotes
        {
            get { return _notes.Count > 0; }
        }

        public void SetNotes(IReadOnlyList<JianpuNote> notes)
        {
            _notes = Clone(notes);
        }

        public IReadOnlyList<JianpuNote> GetNotes()
        {
            return Clone(_notes);
        }

        private static List<JianpuNote> Clone(IReadOnlyList<JianpuNote> notes)
        {
            var clone = new List<JianpuNote>();
            if (notes == null)
            {
                return clone;
            }

            for (var i = 0; i < notes.Count; i++)
            {
                var source = notes[i];
                clone.Add(new JianpuNote
                {
                    Type = source.Type,
                    Pitch = source.Pitch,
                    Accidental = source.Accidental,
                    Octave = source.Octave,
                    Underlines = source.Underlines,
                    Dashes = source.Dashes,
                    Dotted = source.Dotted,
                    IsContinuation = source.IsContinuation
                });
            }

            return clone;
        }
    }
}
