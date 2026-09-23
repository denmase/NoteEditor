using System;

namespace JianpuEditor.Core.Abstractions
{
    /// <summary>Plays a pre-rendered mono audio buffer (the MIDI-DDSP neural-synthesis mix) back
    /// alongside the live, per-note MIDI playback -- a second audio channel <see
    /// cref="Services.ScorePlaybackService"/> keeps time-synchronized with its own timeline, since
    /// MIDI-DDSP itself can only render a whole part upfront, not stream note-by-note.</summary>
    public interface IDdspAudioPlayer : IDisposable
    {
        bool HasAudio { get; }

        /// <summary>Loads a new mono buffer to play, replacing any previous one. Pass an empty
        /// span to clear playback (e.g. when nothing needs neural rendering this time).</summary>
        void LoadSamples(float[] monoSamples, int sampleRate);

        void Play(double startSeconds);

        void Seek(double startSeconds);

        void Stop();
    }
}
