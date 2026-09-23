using System;
using System.IO;
using JianpuEditor.Core.Abstractions;
using ManagedBass;

namespace JianpuEditor.Services
{
    /// <summary>
    /// <see cref="IDdspAudioPlayer"/> backed by a BASS file stream. Goes through a temp WAV file
    /// (via MidiDdsp.Core's own WavWriter) rather than an in-memory PCM buffer: BASS's
    /// byte[]-based CreateStream expects a recognized container format (WAV/MP3/etc.), not raw
    /// samples, and hand-rolling a WAV header risks getting the format chunk subtly wrong --
    /// reusing MidiDdsp.Core's own tested writer avoids that entirely. The file has to stay on
    /// disk for BASS to stream from during playback (unlike a soundfont, which BASS reads once
    /// and can then discard the file), so it's tracked and only replaced/cleaned up once the next
    /// stream (or Dispose) no longer needs it.
    /// </summary>
    internal sealed class BassDdspAudioPlayer : IDdspAudioPlayer
    {
        private int _streamHandle;
        private string _tempWavPath;
        private bool _disposed;

        public bool HasAudio => _streamHandle != 0;

        public void LoadSamples(float[] monoSamples, int sampleRate)
        {
            FreeStream();
            if (monoSamples == null || monoSamples.Length == 0)
            {
                return;
            }

            if (!Bass.Init())
            {
                // BASS_ERROR_ALREADY (already initialized, typically by the IMidiOutput singleton
                // constructed earlier) is expected and harmless; anything else means no audio
                // device is available, so this playback simply stays silent for this session.
                if (Bass.LastError != Errors.Already)
                {
                    AppLog.Error("Failed to initialize BASS for MIDI-DDSP playback. Error: " + Bass.LastError);
                    return;
                }
            }

            try
            {
                _tempWavPath = Path.Combine(Path.GetTempPath(), "jianpu-ddsp-" + Guid.NewGuid().ToString("N") + ".wav");
                MidiDdsp.Core.Audio.WavWriter.WriteFloat32(_tempWavPath, monoSamples, sampleRate);
                _streamHandle = Bass.CreateStream(_tempWavPath, 0, 0, BassFlags.Default);
                if (_streamHandle == 0)
                {
                    AppLog.Error("Failed to create MIDI-DDSP audio stream. Error: " + Bass.LastError);
                    DeleteTempFile();
                }
            }
            catch (Exception ex)
            {
                AppLog.Exception("Failed to load MIDI-DDSP rendered audio", ex);
                FreeStream();
            }
        }

        public void Play(double startSeconds)
        {
            if (_streamHandle == 0)
            {
                return;
            }

            Seek(startSeconds);
            if (!Bass.ChannelPlay(_streamHandle))
            {
                AppLog.Error("Failed to start MIDI-DDSP audio playback. Error: " + Bass.LastError);
            }
        }

        public void Seek(double startSeconds)
        {
            if (_streamHandle == 0)
            {
                return;
            }

            var bytePosition = Bass.ChannelSeconds2Bytes(_streamHandle, Math.Max(0, startSeconds));
            Bass.ChannelSetPosition(_streamHandle, bytePosition);
        }

        public void Stop()
        {
            if (_streamHandle == 0)
            {
                return;
            }

            Bass.ChannelStop(_streamHandle);
        }

        private void FreeStream()
        {
            if (_streamHandle != 0)
            {
                Bass.StreamFree(_streamHandle);
                _streamHandle = 0;
            }

            DeleteTempFile();
        }

        private void DeleteTempFile()
        {
            if (string.IsNullOrEmpty(_tempWavPath))
            {
                return;
            }

            try
            {
                if (File.Exists(_tempWavPath))
                {
                    File.Delete(_tempWavPath);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup; a locked/in-use temp file is left for the OS to reclaim.
            }

            _tempWavPath = null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            FreeStream();
        }
    }
}
