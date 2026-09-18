using System;
using System.IO;
using JianpuEditor.Core.Abstractions;
using ManagedBass;
using ManagedBass.Midi;
using ManagedBass.Vst;

namespace JianpuEditor.Services
{
    /// <summary>
    /// Hosts one or two VST2 instrument plugins (via BASSVST) as the playback engine, as an
    /// alternative to the bundled SoundFont. Most VST2 instruments present a single sound
    /// regardless of MIDI channel (unlike a GM SoundFont, which is multi-timbral across channels),
    /// so genuinely independent melody/chord instruments need two separately loaded plugin
    /// instances -- one per part, routed by <see cref="ScoreMidiSchedule.MelodyChannel"/> /
    /// <see cref="ScoreMidiSchedule.ChordChannel"/>. When only a melody plugin is configured (or
    /// the chord plugin path is the same file), both parts share that one loaded instance instead,
    /// matching the single-plugin behavior this class originally had.
    /// </summary>
    internal sealed class BassVstSynthesizer : IMidiOutput
    {
        private const int OutputChannels = 2;
        private const int SampleRate = 44100;
        private const int MidiChannelCount = 16;

        private readonly int _melodyHandle;
        private readonly int _chordHandle;
        private readonly bool _sharedHandle;
        private bool _disposed;

        public string EngineName { get; }

        public BassVstSynthesizer(string melodyPluginPath, string chordPluginPath)
        {
            if (string.IsNullOrWhiteSpace(melodyPluginPath) || !File.Exists(melodyPluginPath))
            {
                throw new FileNotFoundException("VST plugin file not found.", melodyPluginPath);
            }

            _sharedHandle = string.IsNullOrWhiteSpace(chordPluginPath)
                || string.Equals(Path.GetFullPath(chordPluginPath), Path.GetFullPath(melodyPluginPath), StringComparison.OrdinalIgnoreCase);

            if (!_sharedHandle && !File.Exists(chordPluginPath))
            {
                throw new FileNotFoundException("VST plugin file not found.", chordPluginPath);
            }

            if (!Bass.Init())
            {
                throw new InvalidOperationException("Failed to initialize BASS audio output. Error: " + Bass.LastError);
            }

            _melodyHandle = LoadPlugin(melodyPluginPath);
            _chordHandle = _sharedHandle ? _melodyHandle : LoadPlugin(chordPluginPath);

            EngineName = _sharedHandle
                ? "VST2: " + Path.GetFileName(melodyPluginPath)
                : "VST2: " + Path.GetFileName(melodyPluginPath) + " / " + Path.GetFileName(chordPluginPath);
            AppLog.Info("BassVstSynthesizer initialized: melody=" + melodyPluginPath + ", chords=" + (_sharedHandle ? "(same)" : chordPluginPath));
        }

        private static int LoadPlugin(string pluginPath)
        {
            var handle = BassVst.ChannelCreate(SampleRate, OutputChannels, pluginPath, BassFlags.Default);
            if (handle == 0)
            {
                var error = Bass.LastError;
                Bass.Free();
                throw new InvalidOperationException("Failed to load VST plugin: " + pluginPath + ". Error: " + error);
            }

            if (!Bass.ChannelPlay(handle))
            {
                AppLog.Error("Failed to start BASSVST channel playback for '" + pluginPath + "'. Error: " + Bass.LastError);
            }

            return handle;
        }

        private int HandleFor(int channel)
        {
            return channel == ScoreMidiSchedule.ChordChannel ? _chordHandle : _melodyHandle;
        }

        public void NoteOn(int channel, int note, int velocity)
        {
            BassVst.ProcessEvent(HandleFor(channel), channel, (int)MidiEventType.Note, note | (velocity << 8));
        }

        public void NoteOff(int channel, int note)
        {
            BassVst.ProcessEvent(HandleFor(channel), channel, (int)MidiEventType.Note, note);
        }

        public void ProgramChange(int channel, int program)
        {
            BassVst.ProcessEvent(HandleFor(channel), channel, (int)MidiEventType.Program, program);
        }

        public void AllNotesOff()
        {
            AllNotesOff(_melodyHandle);
            if (!_sharedHandle)
            {
                AllNotesOff(_chordHandle);
            }
        }

        private static void AllNotesOff(int handle)
        {
            for (var channel = 0; channel < MidiChannelCount; channel++)
            {
                BassVst.ProcessEvent(handle, channel, (int)MidiEventType.NotesOff, 0);
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

            if (_melodyHandle != 0)
            {
                BassVst.ChannelFree(_melodyHandle);
            }

            if (!_sharedHandle && _chordHandle != 0)
            {
                BassVst.ChannelFree(_chordHandle);
            }

            Bass.Free();
            AppLog.Info("BassVstSynthesizer disposed");
        }
    }
}
