using JianpuEditor.Models;
using JianpuEditor.Services;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public class MidiExportServiceTests
    {
        [Fact]
        public void Export_WritesValidMidiHeader()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.MeasureWithChords(
                    new[] { "C", "G" },
                    new[] { 0d, 2d },
                    ScoreTestHelper.Note(1),
                    ScoreTestHelper.Note(2),
                    ScoreTestHelper.Note(3),
                    ScoreTestHelper.Note(4)));
            var path = Path.Combine(Path.GetTempPath(), "jianpu-export-" + Guid.NewGuid() + ".mid");

            try
            {
                MidiExportService.Export(score, path);

                var bytes = File.ReadAllBytes(path);
                Assert.True(bytes.Length > 14);
                Assert.Equal("MThd", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
                Assert.Equal("MTrk", System.Text.Encoding.ASCII.GetString(bytes, 14, 4));
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
        public void Export_ThrowsForNullScore()
        {
            Assert.Throws<ArgumentNullException>(() => MidiExportService.Export(null, "test.mid"));
        }

        [Fact]
        public void Export_WritesProgramChangeForMelodyAndChordInstruments()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.MeasureWithChords(
                    new[] { "C" },
                    new[] { 0d },
                    ScoreTestHelper.Note(1),
                    ScoreTestHelper.Note(2)));
            score.MelodyInstrument = 40;
            score.ChordInstrument = 24;
            var path = Path.Combine(Path.GetTempPath(), "jianpu-export-instrument-" + Guid.NewGuid() + ".mid");

            try
            {
                MidiExportService.Export(score, path);

                var bytes = File.ReadAllBytes(path);
                Assert.True(ContainsSequence(bytes, new byte[] { 0xC0, 40 }), "Expected a Program Change to instrument 40 on channel 0 (melody).");
                Assert.True(ContainsSequence(bytes, new byte[] { 0xC1, 24 }), "Expected a Program Change to instrument 24 on channel 1 (chords).");
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
        public void Export_SatbScore_WritesProgramChangeForEveryExtraVoiceChannel()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2));
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Alto", Notes = { ScoreTestHelper.Note(5), ScoreTestHelper.Note(5) } });
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Tenor", Notes = { ScoreTestHelper.Note(3), ScoreTestHelper.Note(3) } });
            var score = ScoreTestHelper.CreateScore(measure);
            var path = Path.Combine(Path.GetTempPath(), "jianpu-export-satb-instrument-" + Guid.NewGuid() + ".mid");

            try
            {
                MidiExportService.Export(score, path);

                var bytes = File.ReadAllBytes(path);
                Assert.True(
                    ContainsSequence(bytes, new byte[] { 0xC2, ScoreMidiSchedule.DefaultExtraVoiceInstrument }),
                    "Expected a Program Change on channel 2 (first extra voice) to the default extra-voice instrument.");
                Assert.True(
                    ContainsSequence(bytes, new byte[] { 0xC3, ScoreMidiSchedule.DefaultExtraVoiceInstrument }),
                    "Expected a Program Change on channel 3 (second extra voice) to the default extra-voice instrument.");
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private static bool ContainsSequence(byte[] haystack, byte[] needle)
        {
            for (var i = 0; i <= haystack.Length - needle.Length; i++)
            {
                var match = true;
                for (var j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
