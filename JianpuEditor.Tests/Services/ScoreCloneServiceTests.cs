using JianpuEditor.Models;
using JianpuEditor.Services;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public class ScoreCloneServiceTests
    {
        [Fact]
        public void Clone_CreatesIndependentCopy()
        {
            var score = new JianpuScore
            {
                Title = "Test",
                Measures =
                {
                    new JianpuMeasure
                    {
                        LyricText = "lyrics",
                        MelodyNotes =
                        {
                            new JianpuNote { Pitch = 1, Type = NoteType.Note }
                        }
                    }
                },
                Ties =
                {
                    new JianpuTie { StartMeasureIndex = 0, StartNoteIndex = 0, EndMeasureIndex = 0, EndNoteIndex = 1 }
                }
            };

            var clone = ScoreCloneService.Clone(score);

            Assert.NotSame(score, clone);
            Assert.Equal("Test", clone.Title);
            Assert.NotSame(score.Measures, clone.Measures);
            Assert.Equal("lyrics", clone.Measures[0].LyricText);
            Assert.NotSame(score.Measures[0].MelodyNotes, clone.Measures[0].MelodyNotes);
            Assert.Equal(1, clone.Measures[0].MelodyNotes[0].Pitch);

            score.Title = "Changed";
            score.Measures[0].LyricText = "changed";
            score.Measures[0].MelodyNotes[0].Pitch = 7;

            Assert.Equal("Test", clone.Title);
            Assert.Equal("lyrics", clone.Measures[0].LyricText);
            Assert.Equal(1, clone.Measures[0].MelodyNotes[0].Pitch);
        }

        [Fact]
        public void AreEquivalent_DetectsEqualClonesAndDifferentScores()
        {
            var original = new JianpuScore { Title = "A" };
            var clone = ScoreCloneService.Clone(original);

            Assert.True(ScoreCloneService.AreEquivalent(original, clone));
            Assert.False(ScoreCloneService.AreEquivalent(original, new JianpuScore { Title = "B" }));
        }
    }
}
