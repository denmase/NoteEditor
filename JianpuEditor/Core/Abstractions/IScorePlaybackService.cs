using System;
using JianpuEditor.Models;

namespace JianpuEditor.Core.Abstractions
{
    public interface IScorePlaybackService : IDisposable
    {
        bool IsPlaying { get; }

        double PositionQuarter { get; }

        double TotalQuarterLength { get; }

        event Action<double> PositionChanged;

        event Action PlaybackFinished;

        event Action<Exception> PlaybackError;

        /// <summary>Fires with a short status message while a MIDI-DDSP neural render is in
        /// progress (before playback of a score with a DDSP-eligible part actually starts), and
        /// with null once it's done (either way -- succeeded, failed, or wasn't needed).</summary>
        event Action<string> RenderingStatusChanged;

        void Prepare(JianpuScore score, double startQuarter = 0);

        void Play(JianpuScore score, int bpm, double startQuarter = 0);

        void StopPlayback();

        void Seek(double quarterBeat);
    }
}
