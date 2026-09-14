using System;
using System.IO;
using JianpuEditor.Core.Abstractions;
using ManagedBass;
using ManagedBass.Midi;
using ManagedBass.Vst;

namespace JianpuEditor.Services
{
    /// <summary>
    /// Hosts a VST2 instrument plugin (via BASSVST) as the playback engine, as an alternative to
    /// the bundled SoundFont. Both the melody and chord parts are sent to this one plugin instance
    /// on separate MIDI channels (0 and 1), the same way BassMidiSynthesizer uses one SoundFont for both.
    /// </summary>
    internal sealed class BassVstSynthesizer : IMidiOutput
    {
        private const int OutputChannels = 2;
        private const int SampleRate = 44100;
        private const int MidiChannelCount = 16;

        private readonly int _vstHandle;
        private bool _disposed;

        public BassVstSynthesizer(string vstPluginPath)
        {
            if (string.IsNullOrWhiteSpace(vstPluginPath) || !File.Exists(vstPluginPath))
            {
                throw new FileNotFoundException("VST plugin file not found.", vstPluginPath);
            }

            if (!Bass.Init())
            {
                throw new InvalidOperationException("Failed to initialize BASS audio output. Error: " + Bass.LastError);
            }

            _vstHandle = BassVst.ChannelCreate(SampleRate, OutputChannels, vstPluginPath, BassFlags.Default);
            if (_vstHandle == 0)
            {
                Bass.Free();
                throw new InvalidOperationException("Failed to load VST plugin: " + vstPluginPath + ". Error: " + Bass.LastError);
            }

            if (!Bass.ChannelPlay(_vstHandle))
            {
                AppLog.Error("Failed to start BASSVST channel playback. Error: " + Bass.LastError);
            }

            AppLog.Info("BassVstSynthesizer initialized: " + vstPluginPath);
        }

        public void NoteOn(int channel, int note, int velocity)
        {
            BassVst.ProcessEvent(_vstHandle, channel, (int)MidiEventType.Note, note | (velocity << 8));
        }

        public void NoteOff(int channel, int note)
        {
            BassVst.ProcessEvent(_vstHandle, channel, (int)MidiEventType.Note, note);
        }

        public void ProgramChange(int channel, int program)
        {
            BassVst.ProcessEvent(_vstHandle, channel, (int)MidiEventType.Program, program);
        }

        public void AllNotesOff()
        {
            for (var channel = 0; channel < MidiChannelCount; channel++)
            {
                BassVst.ProcessEvent(_vstHandle, channel, (int)MidiEventType.NotesOff, 0);
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
                AppLog.Exception("Failed to send AllNotesOff before closing BASSVST", ex);
            }

            if (_vstHandle != 0)
            {
                BassVst.ChannelFree(_vstHandle);
            }

            Bass.Free();
            AppLog.Info("BassVstSynthesizer disposed");
        }
    }
}
