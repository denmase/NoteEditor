using System;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Models;

namespace JianpuEditor.Tests.ViewModels
{
    internal sealed class FakePlaybackService : IScorePlaybackService
    {
        public bool IsPlaying { get; private set; }

        public double PositionQuarter { get; private set; }

        public double TotalQuarterLength { get; private set; }

        public event Action<double> PositionChanged;

#pragma warning disable CS0067 // Required by IScorePlaybackService; this fake never raises them.
        public event Action PlaybackFinished;

        public event Action<Exception> PlaybackError;
#pragma warning restore CS0067

        public void Prepare(JianpuScore score, double startQuarter = 0)
        {
            PositionQuarter = startQuarter;
            TotalQuarterLength = 16;
            PositionChanged?.Invoke(PositionQuarter);
        }

        public void Play(JianpuScore score, int bpm, double startQuarter = 0)
        {
            PositionQuarter = startQuarter;
            IsPlaying = true;
        }

        public void StopPlayback()
        {
            IsPlaying = false;
        }

        public void Seek(double quarterBeat)
        {
            PositionQuarter = quarterBeat;
            PositionChanged?.Invoke(quarterBeat);
        }

        public void Dispose()
        {
        }
    }
}
