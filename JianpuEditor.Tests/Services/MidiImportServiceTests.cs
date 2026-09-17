using System.Text;
using JianpuEditor.Models;
using JianpuEditor.Rendering;
using JianpuEditor.Services;
using JianpuEditor.Services.AudioToMidi;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public sealed class MidiImportServiceTests
    {
        [Fact]
        public void Import_ExportedMelody_ProducesEditableScore()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(
                    ScoreTestHelper.Note(1),
                    ScoreTestHelper.Note(2),
                    ScoreTestHelper.Note(3),
                    ScoreTestHelper.Note(4)));
            score.Title = "Round Trip";
            score.KeySignature = "1=C";
            score.Bpm = 120;

            var path = Path.Combine(Path.GetTempPath(), "jianpu-import-" + Guid.NewGuid() + ".mid");
            try
            {
                MidiExportService.Export(score, path);
                var imported = MidiImportService.Import(path);

                Assert.Equal(Path.GetFileNameWithoutExtension(path), imported.Title);
                Assert.StartsWith("1=", imported.KeySignature);
                Assert.True(imported.Measures.Count > 0);
                Assert.Contains(imported.Measures, measure => measure.MelodyNotes.Count > 0);
                Assert.All(imported.Measures, measure =>
                    Assert.True(measure.MelodyNotes.Count > 0 || imported.Measures.Count == 1));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void Import_ExportedScore_PreservesPitchCount()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(
                    ScoreTestHelper.Note(3),
                    ScoreTestHelper.Note(4),
                    ScoreTestHelper.Note(5)));
            score.KeySignature = "1=C";

            var path = Path.Combine(Path.GetTempPath(), "jianpu-import-pitch-" + Guid.NewGuid() + ".mid");
            try
            {
                MidiExportService.Export(score, path);
                var imported = MidiImportService.Import(path);
                var noteCount = imported.Measures.Sum(measure =>
                    measure.MelodyNotes.Count(note => note.Type == NoteType.Note));

                Assert.Equal(3, noteCount);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void TryMidiToJianpu_MapsMiddleC()
        {
            Assert.True(MidiImportService.TryMidiToJianpu(
                60,
                60,
                out var pitch,
                out var octave,
                out var accidental,
                out var error));
            Assert.Equal(1, pitch);
            Assert.Equal(0, octave);
            Assert.Equal(AccidentalKind.None, accidental);
            Assert.Equal(0, error);
        }

        [Fact]
        public void Import_NormalizesMeasuresToFourBeats()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(
                    ScoreTestHelper.Note(1),
                    ScoreTestHelper.Note(2),
                    ScoreTestHelper.Note(3),
                    ScoreTestHelper.Note(4)),
                ScoreTestHelper.Measure(
                    ScoreTestHelper.Note(5),
                    ScoreTestHelper.Note(6),
                    ScoreTestHelper.Note(7)));
            score.KeySignature = "1=C";

            var path = Path.Combine(Path.GetTempPath(), "jianpu-import-normalize-" + Guid.NewGuid() + ".mid");
            try
            {
                MidiExportService.Export(score, path);
                var imported = MidiImportService.Import(path);

                Assert.True(imported.Measures.Count >= 2);
                foreach (var measure in imported.Measures)
                {
                    var beats = measure.MelodyNotes.Sum(note => JianpuRenderer.GetDurationUnits(note));
                    Assert.Equal(4, beats, 2);
                }
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void ApplyDurationUnits_EncodesCommonValues()
        {
            var quarter = new JianpuNote { Type = NoteType.Note, Pitch = 1 };
            Assert.True(MidiImportService.ApplyDurationUnits(quarter, 1.0));
            Assert.Equal(0, quarter.Underlines);
            Assert.Equal(0, quarter.Dashes);

            var eighth = new JianpuNote { Type = NoteType.Note, Pitch = 1 };
            Assert.True(MidiImportService.ApplyDurationUnits(eighth, 0.5));
            Assert.Equal(1, eighth.Underlines);

            var half = new JianpuNote { Type = NoteType.Note, Pitch = 1 };
            Assert.True(MidiImportService.ApplyDurationUnits(half, 2.0));
            Assert.Equal(1, half.Dashes);
        }

        [Fact]
        public void Import_ThrowsForMissingFile()
        {
            Assert.Throws<FileNotFoundException>(() =>
                MidiImportService.Import(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mid")));
        }

        [Theory]
        [InlineData(3, 4, "3/4")]
        [InlineData(6, 8, "6/8")]
        [InlineData(2, 4, "2/4")]
        public void Import_ReadsTimeSignatureMetaEventFromMidiFile(int numerator, int denominator, string expected)
        {
            var notes = new List<(double Start, double End, int Pitch, float Velocity)> { (0.0, 0.5, 60, 0.8f) };
            var path = Path.Combine(Path.GetTempPath(), "jianpu-import-timesig-" + Guid.NewGuid() + ".mid");
            try
            {
                SimpleMidiWriter.Write(path, notes, bpm: 120.0, numerator, denominator);
                var imported = MidiImportService.Import(path);

                Assert.Equal(expected, imported.TimeSignature);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void Import_DefaultsTimeSignatureTo4x4WhenNoMetaEventPresent()
        {
            var notes = new List<(double Start, double End, int Pitch, float Velocity)> { (0.0, 0.5, 60, 0.8f) };
            var path = Path.Combine(Path.GetTempPath(), "jianpu-import-notimesig-" + Guid.NewGuid() + ".mid");
            try
            {
                // SimpleMidiWriter always writes a time-signature meta-event; a plain
                // MidiExportService.Export currently doesn't, which is the more common
                // real-world "no time signature in this file at all" case to guard.
                var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));
                MidiExportService.Export(score, path);
                var imported = MidiImportService.Import(path);

                Assert.Equal("4/4", imported.TimeSignature);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void Import_MelodyOnNonZeroChannelAlongsideDrumTrack_IsImported()
        {
            // Regression test: real-world MIDI files (e.g. exported from a DAW) routinely put the
            // melody on a channel other than 0, and a separate track on the reserved drum channel (9).
            // MelodyChannel=0 is only this app's own export/playback convention, not a general MIDI
            // rule, so import must not assume the melody track's notes sit on channel 0.
            var bytes = BuildMinimalMultiTrackMidi();
            var path = Path.Combine(Path.GetTempPath(), "jianpu-import-nonzero-channel-" + Guid.NewGuid() + ".mid");
            try
            {
                File.WriteAllBytes(path, bytes);
                var imported = MidiImportService.Import(path);

                var noteCount = imported.Measures.Sum(measure =>
                    measure.MelodyNotes.Count(note => note.Type == NoteType.Note));
                Assert.Equal(4, noteCount);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void Import_OnsetJitterWithinASixteenth_DoesNotInsertSpuriousRest()
        {
            // Regression test: a note whose onset lands a little off-grid (routine for
            // audio-transcribed content, and possible in a human-performed MIDI file) used to
            // leave a real gap between the end of the previous note and this note's raw start
            // once that jitter exceeded the small epsilon BuildMeasures already tolerates --
            // which then got padded with a spurious sixteenth-note rest between the two notes.
            // Quantizing each note's onset to the nearest sixteenth before measures are built
            // closes that gap instead. (Trailing rests from measure normalization padding the
            // rest of the 4-beat measure are expected and not what this test is about --
            // it only checks that no rest was inserted *between* the two notes.)
            const int ticksPerQuarter = 480;
            var bytes = BuildTwoNoteMidiWithJitter(ticksPerQuarter, secondNoteStartTicks: 508); // ~29ms jitter at 120bpm
            var path = Path.Combine(Path.GetTempPath(), "jianpu-import-onset-jitter-" + Guid.NewGuid() + ".mid");
            try
            {
                File.WriteAllBytes(path, bytes);
                var imported = MidiImportService.Import(path);

                var melodyNotes = imported.Measures[0].MelodyNotes;
                var firstNoteIndex = melodyNotes.FindIndex(note => note.Type == NoteType.Note);
                var secondNoteIndex = melodyNotes.FindIndex(firstNoteIndex + 1, note => note.Type == NoteType.Note);
                Assert.Equal(firstNoteIndex + 1, secondNoteIndex);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private static byte[] BuildTwoNoteMidiWithJitter(int ticksPerQuarter, int secondNoteStartTicks)
        {
            var events = new List<byte>();

            // Note 1: starts at tick 0, lasts exactly one quarter note.
            events.AddRange(VariableLength(0));
            events.Add(0x90);
            events.Add(60);
            events.Add(100);
            events.AddRange(VariableLength(ticksPerQuarter));
            events.Add(0x80);
            events.Add(60);
            events.Add(0);

            // Note 2: starts slightly late (jitter), also lasts one quarter note.
            var delta = secondNoteStartTicks - ticksPerQuarter;
            events.AddRange(VariableLength(delta));
            events.Add(0x90);
            events.Add(62);
            events.Add(100);
            events.AddRange(VariableLength(ticksPerQuarter));
            events.Add(0x80);
            events.Add(62);
            events.Add(0);

            events.AddRange(VariableLength(0));
            events.Add(0xFF);
            events.Add(0x2F);
            events.Add(0x00);

            var track = BuildTrackChunk(events);

            var bytes = new List<byte>();
            bytes.AddRange(Encoding.ASCII.GetBytes("MThd"));
            bytes.AddRange(BigEndianUInt32(6));
            bytes.AddRange(BigEndianUInt16(0));
            bytes.AddRange(BigEndianUInt16(1));
            bytes.AddRange(BigEndianUInt16(ticksPerQuarter));
            bytes.AddRange(track);
            return bytes.ToArray();
        }

        private static byte[] BuildMinimalMultiTrackMidi()
        {
            const int ticksPerQuarter = 96;

            var drumTrack = BuildTrackChunk(BuildNoteEvents(channel: 9, notes: new[] { 36, 36, 36, 36 }, ticksPerQuarter));
            var melodyTrack = BuildTrackChunk(BuildNoteEvents(channel: 5, notes: new[] { 60, 62, 64, 65 }, ticksPerQuarter));

            var bytes = new List<byte>();
            bytes.AddRange(Encoding.ASCII.GetBytes("MThd"));
            bytes.AddRange(BigEndianUInt32(6));
            bytes.AddRange(BigEndianUInt16(1));
            bytes.AddRange(BigEndianUInt16(2));
            bytes.AddRange(BigEndianUInt16(ticksPerQuarter));
            bytes.AddRange(drumTrack);
            bytes.AddRange(melodyTrack);
            return bytes.ToArray();
        }

        private static List<byte> BuildNoteEvents(int channel, int[] notes, int ticksPerQuarter)
        {
            var events = new List<byte>();
            foreach (var note in notes)
            {
                events.AddRange(VariableLength(0));
                events.Add((byte)(0x90 | channel));
                events.Add((byte)note);
                events.Add(100);

                events.AddRange(VariableLength(ticksPerQuarter));
                events.Add((byte)(0x80 | channel));
                events.Add((byte)note);
                events.Add(0);
            }

            events.AddRange(VariableLength(0));
            events.Add(0xFF);
            events.Add(0x2F);
            events.Add(0x00);
            return events;
        }

        private static List<byte> BuildTrackChunk(List<byte> events)
        {
            var chunk = new List<byte>();
            chunk.AddRange(Encoding.ASCII.GetBytes("MTrk"));
            chunk.AddRange(BigEndianUInt32((uint)events.Count));
            chunk.AddRange(events);
            return chunk;
        }

        private static IEnumerable<byte> VariableLength(int value)
        {
            var buffer = new List<byte> { (byte)(value & 0x7F) };
            value >>= 7;
            while (value > 0)
            {
                buffer.Insert(0, (byte)((value & 0x7F) | 0x80));
                value >>= 7;
            }

            return buffer;
        }

        private static IEnumerable<byte> BigEndianUInt32(uint value) => new[]
        {
            (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value
        };

        private static IEnumerable<byte> BigEndianUInt16(int value) => new[]
        {
            (byte)(value >> 8), (byte)value
        };
    }
}
