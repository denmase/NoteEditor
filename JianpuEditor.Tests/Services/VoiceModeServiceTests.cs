using System.Linq;
using JianpuEditor.Models;
using JianpuEditor.Services;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    /// <summary>Backs the Edit &gt; Voices menu preset -- the minimal way to reach the new
    /// multi-voice rendering/playback path end to end (see ROADMAP.md) before per-note editing of
    /// the extra voices themselves exists.</summary>
    public sealed class VoiceModeServiceTests
    {
        [Fact]
        public void ApplySatb_PopulatesAltoTenorBassOnEveryMeasure()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4)));

            var changed = VoiceModeService.ApplySatb(score);

            Assert.True(changed);
            var roles = score.Measures[0].ExtraVoices.Select(voice => voice.Role).ToArray();
            Assert.Equal(new[] { "Alto", "Tenor", "Bass" }, roles);
        }

        [Fact]
        public void ApplySatb_RestDurationMatchesTheMeasuresOwnBeatCount()
        {
            // A 2-beat pickup measure: the rest-filled voices must total 2 beats too, not the
            // nominal 4, or ScoreMidiSchedule's cross-voice duration check would immediately flag
            // every freshly-created voice as mismatched.
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2)));

            VoiceModeService.ApplySatb(score);

            foreach (var voice in score.Measures[0].ExtraVoices)
            {
                var voiceMeasure = new JianpuMeasure { MelodyNotes = voice.Notes };
                Assert.Equal(2.0, ScoreMidiSchedule.GetMeasureDurationUnits(voiceMeasure), 3);
            }
        }

        [Fact]
        public void ApplySatb_AlreadySatb_DoesNotClobberExistingVoiceContent()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));
            VoiceModeService.ApplySatb(score);
            score.Measures[0].ExtraVoices[0].Notes[0] = ScoreTestHelper.Note(5);

            var changed = VoiceModeService.ApplySatb(score);

            Assert.False(changed);
            Assert.Equal(NoteType.Note, score.Measures[0].ExtraVoices[0].Notes[0].Type);
            Assert.Equal(5, score.Measures[0].ExtraVoices[0].Notes[0].Pitch);
        }

        [Fact]
        public void ApplySingle_ClearsExtraVoicesFromEveryMeasure()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));
            VoiceModeService.ApplySatb(score);

            var changed = VoiceModeService.ApplySingle(score);

            Assert.True(changed);
            Assert.Empty(score.Measures[0].ExtraVoices);
        }

        [Fact]
        public void ApplySingle_AlreadySingle_ReportsUnchanged()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));

            var changed = VoiceModeService.ApplySingle(score);

            Assert.False(changed);
        }

        [Fact]
        public void ApplySatb_SetsPrimaryVoiceLabelToSoprano()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));

            VoiceModeService.ApplySatb(score);

            Assert.Equal("Soprano", score.PrimaryVoiceLabel);
        }

        [Fact]
        public void ApplySatb_ExistingCustomLabel_IsNotClobbered()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));
            score.PrimaryVoiceLabel = "Lead";

            VoiceModeService.ApplySatb(score);

            Assert.Equal("Lead", score.PrimaryVoiceLabel);
        }

        [Fact]
        public void ApplySingle_ResetsPrimaryVoiceLabelToNull()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));
            VoiceModeService.ApplySatb(score);

            VoiceModeService.ApplySingle(score);

            Assert.Null(score.PrimaryVoiceLabel);
        }
    }
}
