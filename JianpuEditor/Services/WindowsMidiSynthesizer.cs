using System;
using System.Runtime.InteropServices;
using JianpuEditor.Core.Abstractions;

namespace JianpuEditor.Services
{
    internal sealed class WindowsMidiSynthesizer : IMidiOutput
    {
        private const int MidiMapper = -1;
        private const int CallbackNull = 0;
        private const int MmsyserrNoerror = 0;

        private IntPtr _handle = IntPtr.Zero;
        private int _deviceId = MidiMapper;
        private bool _disposed;
        private string _deviceName = "Not opened";

        public void NoteOn(int channel, int note, int velocity)
        {
            SendShortMessage("NoteOn", 0x90 | (channel & 0x0F), note, velocity);
        }

        public void NoteOff(int channel, int note)
        {
            SendShortMessage("NoteOff", 0x80 | (channel & 0x0F), note, 0);
        }

        public void ProgramChange(int channel, int program)
        {
            SendShortMessage("ProgramChange", 0xC0 | (channel & 0x0F), program & 0x7F, 0);
        }

        public void AllNotesOff()
        {
            if (_handle == IntPtr.Zero)
            {
                return;
            }

            for (var channel = 0; channel < 16; channel++)
            {
                SendShortMessage("AllNotesOff", 0xB0 | channel, 123, 0);
            }
        }

        private void EnsureOpen()
        {
            if (_handle != IntPtr.Zero)
            {
                return;
            }

            AppLog.Info("Opening MIDI output device...");
            var lastError = MmsyserrNoerror;
            if (TryOpenDevice(MidiMapper, "MIDI Mapper", out lastError))
            {
                return;
            }

            var deviceCount = midiOutGetNumDevs();
            AppLog.Info("Failed to open MIDI Mapper, error code=" + lastError + ", local device count=" + deviceCount);
            for (var deviceId = 0; deviceId < deviceCount; deviceId++)
            {
                var name = GetDeviceName(deviceId);
                if (TryOpenDevice(deviceId, name, out lastError))
                {
                    return;
                }

                AppLog.Error("Failed to open MIDI device: id=" + deviceId + ", name=" + name + ", error=" + lastError);
            }

            throw new InvalidOperationException(
                "Unable to open MIDI output device. Error code=" + lastError +
                ". Please make sure a MIDI synthesizer is enabled on this system (e.g. Microsoft GS Wavetable Synth)." +
                " Log: " + AppLog.LogFilePath);
        }

        private bool TryOpenDevice(int deviceId, string deviceLabel, out int errorCode)
        {
            errorCode = midiOutOpen(out var handle, deviceId, IntPtr.Zero, IntPtr.Zero, CallbackNull);
            if (errorCode != MmsyserrNoerror)
            {
                return false;
            }

            _handle = handle;
            _deviceId = deviceId;
            _deviceName = deviceLabel;
            AppLog.Info("MIDI output device opened: id=" + deviceId + ", name=" + deviceLabel);
            return true;
        }

        private static string GetDeviceName(int deviceId)
        {
            var caps = new MidiOutCaps();
            var size = Marshal.SizeOf(caps);
            var result = midiOutGetDevCaps(deviceId, ref caps, size);
            if (result != MmsyserrNoerror)
            {
                return "Device " + deviceId;
            }

            return (caps.szPname ?? string.Empty).Trim();
        }

        private void SendShortMessage(string action, int status, int data1, int data2)
        {
            if (_disposed)
            {
                return;
            }

            EnsureOpen();
            var message = status | ((data1 & 0x7F) << 8) | ((data2 & 0x7F) << 16);
            var result = midiOutShortMsg(_handle, message);
            if (result != MmsyserrNoerror)
            {
                var text = string.Format(
                    "Failed to send MIDI message: action={0}, device={1}, status=0x{2:X2}, note={3}, velocity={4}, error={5}",
                    action,
                    _deviceName,
                    status,
                    data1,
                    data2,
                    result);
                AppLog.Error(text);
                throw new InvalidOperationException(text + ". Log: " + AppLog.LogFilePath);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_handle != IntPtr.Zero)
            {
                try
                {
                    AllNotesOff();
                }
                catch (Exception ex)
                {
                    AppLog.Exception("Failed to send AllNotesOff before closing MIDI", ex);
                }

                var result = midiOutClose(_handle);
                if (result != MmsyserrNoerror)
                {
                    AppLog.Error("Failed to close MIDI device: error=" + result + ", device=" + _deviceName);
                }
                else
                {
                    AppLog.Info("MIDI output device closed: " + _deviceName);
                }

                _handle = IntPtr.Zero;
            }
        }

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern int midiOutGetDevCaps(int uDeviceID, ref MidiOutCaps lpCaps, int uSize);

        [DllImport("winmm.dll")]
        private static extern int midiOutGetNumDevs();

        [DllImport("winmm.dll")]
        private static extern int midiOutOpen(
            out IntPtr lphMidiOut,
            int uDeviceID,
            IntPtr dwCallback,
            IntPtr dwInstance,
            int dwFlags);

        [DllImport("winmm.dll")]
        private static extern int midiOutShortMsg(IntPtr hMidiOut, int dwMsg);

        [DllImport("winmm.dll")]
        private static extern int midiOutClose(IntPtr hMidiOut);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MidiOutCaps
        {
            public ushort wMid;
            public ushort wPid;
            public uint vDriverVersion;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;

            public ushort wTechnology;
            public ushort wVoices;
            public ushort wNotes;
            public ushort wChannelMask;
            public uint dwSupport;
        }
    }
}
