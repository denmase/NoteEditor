using System.Linq;
using JianpuEditor.Models;
using JianpuEditor.Services;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public sealed class ChordVocabularyTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        public void AllCandidatesContainTheirDegree(int degree)
        {
            foreach (var roman in ChordVocabulary.DegreeToCandidates[degree])
            {
                var rootSemitone = ChordVocabulary.MajorScaleSemitones[(roman.Degree - 1) % 7] + roman.Accidental;
                var targetSemitone = ChordVocabulary.MajorScaleSemitones[(degree - 1) % 7];

                var contains = roman.Quality.GetIntervals()
                    .Any(interval => ((rootSemitone + interval) % 12 + 12) % 12 == targetSemitone);

                Assert.True(contains, "Candidate '" + roman + "' listed under degree " + degree + " does not contain that degree.");
            }
        }
    }
}
