using System;

namespace JianpuEditor.Core.Abstractions
{
    /// <summary>Low-level MIDI output sink used for playback. Lets ScorePlaybackService be tested without a real device.</summary>
    public interface IMidiOutput : IDisposable
    {
        void NoteOn(int channel, int note, int velocity);

        void NoteOff(int channel, int note);

        void ProgramChange(int channel, int program);

        void AllNotesOff();
    }
}
