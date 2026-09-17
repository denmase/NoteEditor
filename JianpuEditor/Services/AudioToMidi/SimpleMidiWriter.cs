using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JianpuEditor.Services.AudioToMidi
{
    /// <summary>
    /// Writes a single-track Standard MIDI File from transcribed (start, end, pitch,
    /// velocity) note events, so the result can be handed to the app's existing
    /// <see cref="IMidiImportService"/> pipeline (melody-track detection, chord grouping,
    /// etc.) instead of building a JianpuScore from scratch. Byte-format mirrors
    /// <see cref="MidiExportService"/> for consistency with the rest of the codebase.
    /// </summary>
    internal static class SimpleMidiWriter
    {
        private const int TicksPerQuarter = 480;

        public static void Write(
            string path,
            IReadOnlyList<(double Start, double End, int Pitch, float Velocity)> notes,
            double bpm,
            int timeSignatureNumerator = 4,
            int timeSignatureDenominator = 4)
        {
            var ticksPerSecond = TicksPerQuarter * (bpm / 60.0);

            var events = new List<(long Ticks, bool IsNoteOn, int Pitch, int Velocity)>();
            foreach (var note in notes)
            {
                var startTicks = (long)Math.Round(note.Start * ticksPerSecond);
                var endTicks = Math.Max(startTicks + 1, (long)Math.Round(note.End * ticksPerSecond));
                var velocity = Math.Max(1, Math.Min(127, (int)Math.Round(note.Velocity * 127)));
                events.Add((startTicks, true, note.Pitch, velocity));
                events.Add((endTicks, false, note.Pitch, 0));
            }

            // Note-offs before note-ons at the same tick, so a repeated pitch with no gap
            // still produces a valid off/on pair instead of two overlapping on events.
            events.Sort((a, b) =>
            {
                var cmp = a.Ticks.CompareTo(b.Ticks);
                return cmp != 0 ? cmp : a.IsNoteOn.CompareTo(b.IsNoteOn);
            });

            var microsecondsPerQuarter = (int)Math.Round(60_000_000 / bpm);

            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(Encoding.ASCII.GetBytes("MTrk"));
                var dataStart = stream.Position;
                WriteInt32Be(writer, 0);

                WriteVarLength(writer, 0);
                writer.Write((byte)0xFF);
                writer.Write((byte)0x51);
                writer.Write((byte)0x03);
                writer.Write((byte)((microsecondsPerQuarter >> 16) & 0xFF));
                writer.Write((byte)((microsecondsPerQuarter >> 8) & 0xFF));
                writer.Write((byte)(microsecondsPerQuarter & 0xFF));

                WriteVarLength(writer, 0);
                writer.Write((byte)0xFF);
                writer.Write((byte)0x58);
                writer.Write((byte)0x04);
                writer.Write((byte)timeSignatureNumerator);
                writer.Write((byte)Math.Round(Math.Log(Math.Max(1, timeSignatureDenominator), 2)));
                writer.Write((byte)24); // MIDI clocks per metronome click, the standard default
                writer.Write((byte)8);  // notated 32nd notes per quarter note, the standard default

                var lastTick = 0L;
                foreach (var evt in events)
                {
                    WriteVarLength(writer, (int)Math.Max(0, evt.Ticks - lastTick));
                    lastTick = evt.Ticks;
                    writer.Write((byte)((evt.IsNoteOn ? 0x90 : 0x80) | 0x00));
                    writer.Write((byte)evt.Pitch);
                    writer.Write((byte)evt.Velocity);
                }

                WriteVarLength(writer, 0);
                writer.Write(new byte[] { 0xFF, 0x2F, 0x00 });

                var dataEnd = stream.Position;
                var trackLength = (int)(dataEnd - dataStart - 4);
                stream.Position = dataStart;
                WriteInt32Be(writer, trackLength);
            }

            using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            using var fileWriter = new BinaryWriter(fileStream, Encoding.UTF8);
            fileWriter.Write(Encoding.ASCII.GetBytes("MThd"));
            WriteInt32Be(fileWriter, 6);
            WriteInt16Be(fileWriter, 1);
            WriteInt16Be(fileWriter, 1);
            WriteInt16Be(fileWriter, TicksPerQuarter);
            fileWriter.Write(stream.ToArray());
        }

        private static void WriteInt16Be(BinaryWriter writer, int value)
        {
            writer.Write((byte)((value >> 8) & 0xFF));
            writer.Write((byte)(value & 0xFF));
        }

        private static void WriteInt32Be(BinaryWriter writer, int value)
        {
            writer.Write((byte)((value >> 24) & 0xFF));
            writer.Write((byte)((value >> 16) & 0xFF));
            writer.Write((byte)((value >> 8) & 0xFF));
            writer.Write((byte)(value & 0xFF));
        }

        private static void WriteVarLength(BinaryWriter writer, int value)
        {
            var buffer = new List<byte> { (byte)(value & 0x7F) };
            value >>= 7;
            while (value > 0)
            {
                buffer.Insert(0, (byte)(0x80 | (value & 0x7F)));
                value >>= 7;
            }

            foreach (var b in buffer)
            {
                writer.Write(b);
            }
        }
    }
}
