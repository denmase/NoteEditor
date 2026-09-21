using JianpuEditor.Models;
using JianpuEditor.Services;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public sealed class JianpuPitchCodecTests
    {
        [Theory]
        [InlineData(61, 1.5, AccidentalKind.Sharp, "#1", 61)]
        [InlineData(63, 2.5, AccidentalKind.Flat, "b3", 63)]
        [InlineData(60, 1.0, AccidentalKind.None, "1", 60)]
        public void TryMidiToJianpu_MapsAccidentals(
            int midiNote,
            double expectedPitch,
            AccidentalKind expectedAccidental,
            string expectedDisplay,
            int expectedMidi)
        {
            Assert.True(MidiImportService.TryMidiToJianpu(
                midiNote,
                60,
                out var pitch,
                out _,
                out var accidental,
                out _));

            Assert.Equal(expectedPitch, pitch, 3);
            Assert.Equal(expectedAccidental, accidental);

            var note = new JianpuNote
            {
                Type = NoteType.Note,
                Pitch = pitch,
                Accidental = accidental
            };
            Assert.Equal(expectedDisplay, JianpuPitchCodec.GetPitchDisplayText(note));
            Assert.Equal(expectedMidi, JianpuPitchCodec.ToMelodyMidiNote(note, 60));
        }

        [Fact]
        public void ToMelodyMidiNote_NaturalPitch_UsesScaleDegree()
        {
            var note = new JianpuNote { Type = NoteType.Note, Pitch = 5 };
            Assert.Equal(67, JianpuPitchCodec.ToMelodyMidiNote(note, 60));
        }

        [Fact]
        public void GetAccidentalMark_NaturalSign_ReturnsNaturalGlyph()
        {
            var note = new JianpuNote { Type = NoteType.Note, Pitch = 4, Accidental = AccidentalKind.Natural };
            Assert.Equal("♮", JianpuPitchCodec.GetAccidentalMark(note));
        }

        [Fact]
        public void SetAccidentalPitch_Natural_SetsPlainIntegerDegree()
        {
            var note = new JianpuNote { Type = NoteType.Note, Pitch = 3.5, Accidental = AccidentalKind.Sharp };

            JianpuPitchCodec.SetAccidentalPitch(note, AccidentalKind.Natural, 4);

            Assert.Equal(AccidentalKind.Natural, note.Accidental);
            Assert.Equal(4, note.Pitch);
        }

        [Fact]
        public void SetAccidentalPitch_None_SetsPlainIntegerDegree()
        {
            var note = new JianpuNote { Type = NoteType.Note, Pitch = 2.5, Accidental = AccidentalKind.Flat };

            JianpuPitchCodec.SetAccidentalPitch(note, AccidentalKind.None, 3);

            Assert.Equal(AccidentalKind.None, note.Accidental);
            Assert.Equal(3, note.Pitch);
        }

        [Fact]
        public void IsValidMelodyPitch_NaturalWithIntegerPitch_IsValid()
        {
            var note = new JianpuNote { Type = NoteType.Note, Pitch = 5, Accidental = AccidentalKind.Natural };
            Assert.True(JianpuPitchCodec.IsValidMelodyPitch(note));
        }
    }
}
