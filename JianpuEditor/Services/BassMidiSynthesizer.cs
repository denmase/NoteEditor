using System;
using System.IO;
using JianpuEditor.Core.Abstractions;
using ManagedBass;
using ManagedBass.Midi;

namespace JianpuEditor.Services
{
    /// <summary>
    /// SoundFont-based synthesizer (BASSMIDI), used so playback and MIDI export sound consistent
    /// across machines instead of depending on whatever GM device Windows happens to provide.
    /// </summary>
    internal sealed class BassMidiSynthesizer : IMidiOutput
    {
        private const int Channels = 16;
        private const int SampleRate = 44100;

        private readonly int _streamHandle;
        private readonly int _fontHandle;
        private bool _disposed;

        public string EngineName { get; }

        public BassMidiSynthesizer(string soundFontPath)
        {
            if (string.IsNullOrWhiteSpace(soundFontPath) || !File.Exists(soundFontPath))
            {
                throw new FileNotFoundException("SoundFont file not found.", soundFontPath);
            }

            if (!Bass.Init())
            {
                throw new InvalidOperationException("Failed to initialize BASS audio output. Error: " + Bass.LastError);
            }

            _streamHandle = BassMidi.CreateStream(Channels, BassFlags.Default, SampleRate);
            if (_streamHandle == 0)
            {
                Bass.Free();
                throw new InvalidOperationException("Failed to create BASSMIDI stream. Error: " + Bass.LastError);
            }

            _fontHandle = BassMidi.FontInit(soundFontPath, FontInitFlags.Unicode);
            if (_fontHandle == 0)
            {
                Bass.StreamFree(_streamHandle);
                Bass.Free();
                throw new InvalidOperationException("Failed to load SoundFont: " + soundFontPath + ". Error: " + Bass.LastError);
            }

            var fonts = new[] { new MidiFont { Handle = _fontHandle, Preset = -1, Bank = 0 } };
            if (BassMidi.StreamSetFonts(_streamHandle, fonts, fonts.Length) != fonts.Length)
            {
                AppLog.Error("Failed to assign SoundFont to BASSMIDI stream. Error: " + Bass.LastError);
            }

            if (!Bass.ChannelPlay(_streamHandle))
            {
                AppLog.Error("Failed to start BASSMIDI stream playback. Error: " + Bass.LastError);
            }

            EngineName = "SoundFont (" + Path.GetFileNameWithoutExtension(soundFontPath) + ")";
            AppLog.Info("BassMidiSynthesizer initialized: " + soundFontPath);
        }

        public void NoteOn(int channel, int note, int velocity)
        {
            BassMidi.StreamEvent(_streamHandle, channel, MidiEventType.Note, note | (velocity << 8));
        }

        public void NoteOff(int channel, int note)
        {
            BassMidi.StreamEvent(_streamHandle, channel, MidiEventType.Note, note);
        }

        public void ProgramChange(int channel, int program)
        {
            BassMidi.StreamEvent(_streamHandle, channel, MidiEventType.Program, program);
        }

        public void AllNotesOff()
        {
            for (var channel = 0; channel < Channels; channel++)
            {
                BassMidi.StreamEvent(_streamHandle, channel, MidiEventType.NotesOff, 0);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                AllNotesOff();
            }
            catch (Exception ex)
            {
                AppLog.Exception("Failed to send AllNotesOff before closing BASSMIDI", ex);
            }

            if (_fontHandle != 0)
            {
                BassMidi.FontFree(_fontHandle);
            }

            if (_streamHandle != 0)
            {
                Bass.StreamFree(_streamHandle);
            }

            Bass.Free();
            AppLog.Info("BassMidiSynthesizer disposed");
        }
    }
}
