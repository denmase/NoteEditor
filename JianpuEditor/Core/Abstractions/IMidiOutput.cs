using System;

namespace JianpuEditor.Core.Abstractions
{
    /// <summary>Low-level MIDI output sink used for playback. Lets ScorePlaybackService be tested without a real device.</summary>
    public interface IMidiOutput : IDisposable
    {
        /// <summary>Short, human-readable description of the active playback engine (e.g. "SoundFont (GeneralUser-GS)", "VST2: Serum.dll"), shown in the UI so users can see what's actually active rather than just what's configured.</summary>
        string EngineName { get; }

        void NoteOn(int channel, int note, int velocity);

        void NoteOff(int channel, int note);

        void ProgramChange(int channel, int program);

        void AllNotesOff();
    }
}
