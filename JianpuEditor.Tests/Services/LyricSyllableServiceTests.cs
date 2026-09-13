using JianpuEditor.Models;
using JianpuEditor.Services;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public class LyricSyllableServiceTests
    {
        [Fact]
        public void ImportLegacyLyricText_SplitsCharactersAcrossNotesWhenNoWhitespace()
        {
            var measure = ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1),
                ScoreTestHelper.Note(2),
                ScoreTestHelper.Note(3),
                ScoreTestHelper.Note(4));
            measure.LyricText = "Ruby";

            LyricSyllableService.ImportLegacyLyricText(measure);

            Assert.Equal(4, measure.LyricSyllables.Count);
            Assert.Equal("R", measure.LyricSyllables[0].Text);
            Assert.Equal(0, measure.LyricSyllables[0].NoteIndex);
            Assert.Equal("u", measure.LyricSyllables[1].Text);
            Assert.Equal(1, measure.LyricSyllables[1].NoteIndex);
            Assert.Equal("b", measure.LyricSyllables[2].Text);
            Assert.Equal("y", measure.LyricSyllables[3].Text);
            Assert.Equal("Ruby", measure.LyricText);
        }

        [Fact]
        public void ImportLegacyLyricText_SplitsWhitespaceSeparatedTokens()
        {
            var measure = ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1),
                ScoreTestHelper.Note(2),
                ScoreTestHelper.Note(3));
            measure.LyricText = "hello world";

            LyricSyllableService.ImportLegacyLyricText(measure);

            Assert.Equal(2, measure.LyricSyllables.Count);
            Assert.Equal("hello", measure.LyricSyllables[0].Text);
            Assert.Equal(0, measure.LyricSyllables[0].NoteIndex);
            Assert.Equal("world", measure.LyricSyllables[1].Text);
            Assert.Equal(1, measure.LyricSyllables[1].NoteIndex);
        }

        [Fact]
        public void GetDisplayText_UsesLyricTextWhenNoStructuredLyrics()
        {
            var measure = new JianpuMeasure { LyricText = "shines on the earth" };

            Assert.False(LyricSyllableService.HasStructuredLyrics(measure));
            Assert.Equal("shines on the earth", LyricSyllableService.GetDisplayText(measure));
            Assert.Equal("shines on the earth", LyricSyllableService.GetFallbackText(measure));
        }

        [Fact]
        public void GetDisplayText_JoinsStructuredSyllables()
        {
            var measure = ScoreTestHelper.MeasureWithLyrics(
                "Ruby",
                new[] { "R", "u", "b", "y" },
                new[] { 0, 1, 2, 3 },
                ScoreTestHelper.Note(1),
                ScoreTestHelper.Note(2),
                ScoreTestHelper.Note(3),
                ScoreTestHelper.Note(4));
            LyricSyllableService.NormalizeMeasure(measure);

            Assert.True(LyricSyllableService.HasStructuredLyrics(measure));
            Assert.Equal("Ruby", LyricSyllableService.GetDisplayText(measure));
            Assert.Equal("Ruby", LyricSyllableService.GetFallbackText(measure));
        }

        [Fact]
        public void NormalizeMeasure_SyncsBeatPositionFromNoteIndex()
        {
            var measure = ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1),
                ScoreTestHelper.Note(2, dashes: 1),
                ScoreTestHelper.Note(3));
            measure.LyricSyllables.Add(new LyricSyllable { Text = "la", NoteIndex = 2 });

            LyricSyllableService.NormalizeMeasure(measure);

            Assert.Equal(3, measure.LyricSyllables[0].BeatPosition);
        }

        [Fact]
        public void ResolveNoteIndex_FindsNoteFromBeatPosition()
        {
            var measure = ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1),
                ScoreTestHelper.Note(2, dashes: 1),
                ScoreTestHelper.Note(3));
            var syllable = new LyricSyllable { Text = "da", BeatPosition = 3 };

            Assert.Equal(2, LyricSyllableService.ResolveNoteIndex(measure, syllable));
        }

        [Fact]
        public void ImportLegacyLyricText_DoesNotOverwriteExistingSyllables()
        {
            var measure = ScoreTestHelper.MeasureWithLyrics(
                "old lyrics",
                new[] { "new" },
                new[] { 0 },
                ScoreTestHelper.Note(1),
                ScoreTestHelper.Note(2));

            LyricSyllableService.ImportLegacyLyricText(measure);

            Assert.Single(measure.LyricSyllables);
            Assert.Equal("new", measure.LyricSyllables[0].Text);
            Assert.Equal("old lyrics", measure.LyricText);
        }
    }
}
