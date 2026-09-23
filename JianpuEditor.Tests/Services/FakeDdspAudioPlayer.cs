using System;
using System.Threading;
using JianpuEditor.Core.Abstractions;

namespace JianpuEditor.Tests.Services
{
    internal sealed class FakeDdspAudioPlayer : IDdspAudioPlayer
    {
        private readonly ManualResetEventSlim _loadSamplesSignal = new ManualResetEventSlim(false);

        public bool HasAudio { get; private set; }

        public float[] LoadedSamples { get; private set; }

        public int SampleRate { get; private set; }

        public int LoadSamplesCallCount { get; private set; }

        public double? LastPlayStartSeconds { get; private set; }

        public double? LastSeekSeconds { get; private set; }

        public int StopCallCount { get; private set; }

        public void LoadSamples(float[] monoSamples, int sampleRate)
        {
            LoadSamplesCallCount++;
            LoadedSamples = monoSamples;
            SampleRate = sampleRate;
            HasAudio = monoSamples != null && monoSamples.Length > 0;
            _loadSamplesSignal.Set();
        }

        /// <summary>Waits for LoadSamples to have been called at least <paramref name="count"/>
        /// times. ScorePlaybackService.Play starts MIDI-DDSP rendering asynchronously (via
        /// Task.Run + ContinueWith, which always queues rather than inlining even for an
        /// already-completed antecedent task), so its effects aren't visible immediately after
        /// Play() returns -- only the "nothing needs rendering" fast path is synchronous.</summary>
        public bool WaitForLoadSamplesCount(int count, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (LoadSamplesCallCount < count && DateTime.UtcNow < deadline)
            {
                _loadSamplesSignal.Wait(TimeSpan.FromMilliseconds(50));
                _loadSamplesSignal.Reset();
            }

            return LoadSamplesCallCount >= count;
        }

        public void Play(double startSeconds)
        {
            LastPlayStartSeconds = startSeconds;
        }

        public void Seek(double startSeconds)
        {
            LastSeekSeconds = startSeconds;
        }

        public void Stop()
        {
            StopCallCount++;
        }

        public void Dispose()
        {
        }
    }
}
