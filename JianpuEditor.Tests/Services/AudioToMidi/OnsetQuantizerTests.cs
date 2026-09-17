using System.Collections.Generic;
using JianpuEditor.Services.AudioToMidi;
using Xunit;

namespace JianpuEditor.Tests.Services.AudioToMidi
{
    public sealed class OnsetQuantizerTests
    {
        [Fact]
        public void Quantize_SnapsOnsetsToNearestGridPositionAtGivenTempo()
        {
            // 120bpm -> beat period 0.5s, sixteenth grid = 0.125s.
            var notes = new List<TranscribedNote>
            {
                new TranscribedNote(0.53, 1.03, 60, 0.8f),
                new TranscribedNote(1.06, 1.56, 62, 0.8f)
            };

            var result = OnsetQuantizer.Quantize(notes, bpm: 120.0, gridQuarterNotes: 0.25);

            Assert.Equal(0.5, result[0].Start, 3);
            Assert.Equal(1.0, result[1].Start, 3);
        }

        [Fact]
        public void Quantize_PreservesNoteDuration()
        {
            var notes = new List<TranscribedNote> { new TranscribedNote(0.53, 1.23, 60, 0.8f) };

            var result = OnsetQuantizer.Quantize(notes, bpm: 120.0, gridQuarterNotes: 0.25);

            Assert.Equal(0.7, result[0].End - result[0].Start, 3);
        }

        [Fact]
        public void Quantize_ZeroGrid_LeavesOnsetsUnchanged()
        {
            var notes = new List<TranscribedNote> { new TranscribedNote(0.531, 1.017, 60, 0.8f) };

            var result = OnsetQuantizer.Quantize(notes, bpm: 120.0, gridQuarterNotes: 0.0);

            Assert.Equal(0.531, result[0].Start, 3);
        }
    }
}
