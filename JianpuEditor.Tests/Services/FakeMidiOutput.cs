using System.Collections.Generic;
using JianpuEditor.Core.Abstractions;

namespace JianpuEditor.Tests.Services
{
    internal sealed class FakeMidiOutput : IMidiOutput
    {
        public string EngineName => "Fake";

        public List<(int Channel, int Program)> ProgramChanges { get; } = new List<(int Channel, int Program)>();

        public List<(int Channel, int Note, int Velocity)> NotesOn { get; } = new List<(int Channel, int Note, int Velocity)>();

        public int AllNotesOffCount { get; private set; }

        public void NoteOn(int channel, int note, int velocity)
        {
            NotesOn.Add((channel, note, velocity));
        }

        public void NoteOff(int channel, int note)
        {
        }

        public void ProgramChange(int channel, int program)
        {
            ProgramChanges.Add((channel, program));
        }

        public void AllNotesOff()
        {
            AllNotesOffCount++;
        }

        public void Dispose()
        {
        }
    }
}
