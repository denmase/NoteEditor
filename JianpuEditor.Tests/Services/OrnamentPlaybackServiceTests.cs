using JianpuEditor.Models;
using JianpuEditor.Services;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public sealed class OrnamentPlaybackServiceTests
    {
        [Fact]
        public void ScheduleMelodyNote_GraceNote_PlaysNeighborBeforeMain()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(3));
            OrnamentService.TryAddOrnament(measure, 0, OrnamentType.GraceNote);

            var events = OrnamentPlaybackService.ScheduleMelodyNote(
                measure,
                measure.MelodyNotes[0],
                0,
                0,
                1,
                ScoreMidiSchedule.DefaultTonicMidi,
                ScoreMidiSchedule.MelodyChannel,
                ScoreMidiSchedule.MelodyVelocity);

            Assert.Equal(2, events.Count);
            Assert.Equal(0, events[0].StartQuarter, 3);
            Assert.True(events[0].DurationQuarter < 1);
            Assert.Equal(ScoreMidiSchedule.ToMelodyMidiNote(ScoreTestHelper.Note(2), ScoreMidiSchedule.DefaultTonicMidi), events[0].MidiNote);
            Assert.True(events[1].StartQuarter > events[0].StartQuarter);
            Assert.Equal(ScoreMidiSchedule.ToMelodyMidiNote(ScoreTestHelper.Note(3), ScoreMidiSchedule.DefaultTonicMidi), events[1].MidiNote);
        }

        [Fact]
        public void ScheduleMelodyNote_Trill_AlternatesUpperNeighbor()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(3));
            OrnamentService.TryAddOrnament(measure, 0, OrnamentType.Trill);

            var events = OrnamentPlaybackService.ScheduleMelodyNote(
                measure,
                measure.MelodyNotes[0],
                0,
                0,
                1,
                ScoreMidiSchedule.DefaultTonicMidi,
                ScoreMidiSchedule.MelodyChannel,
                ScoreMidiSchedule.MelodyVelocity);

            Assert.True(events.Count >= 4);
            Assert.Equal(
                ScoreMidiSchedule.ToMelodyMidiNote(ScoreTestHelper.Note(3), ScoreMidiSchedule.DefaultTonicMidi),
                events[0].MidiNote);
            Assert.Equal(
                ScoreMidiSchedule.ToMelodyMidiNote(ScoreTestHelper.Note(4), ScoreMidiSchedule.DefaultTonicMidi),
                events[1].MidiNote);
        }

        [Fact]
        public void ScheduleMelodyNote_Fermata_ExtendsDuration()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1));
            OrnamentService.TryAddOrnament(measure, 0, OrnamentType.Fermata);

            var events = OrnamentPlaybackService.ScheduleMelodyNote(
                measure,
                measure.MelodyNotes[0],
                0,
                0,
                2,
                ScoreMidiSchedule.DefaultTonicMidi,
                ScoreMidiSchedule.MelodyChannel,
                ScoreMidiSchedule.MelodyVelocity);

            Assert.Single(events);
            Assert.Equal(3, events[0].DurationQuarter, 3);
        }

        [Fact]
        public void ScheduleMelodyNote_Staccato_ShortensDurationWithoutMovingStart()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1));
            OrnamentService.TryAddOrnament(measure, 0, OrnamentType.Staccato);

            var events = OrnamentPlaybackService.ScheduleMelodyNote(
                measure,
                measure.MelodyNotes[0],
                0,
                0,
                2,
                ScoreMidiSchedule.DefaultTonicMidi,
                ScoreMidiSchedule.MelodyChannel,
                ScoreMidiSchedule.MelodyVelocity);

            Assert.Single(events);
            Assert.Equal(0, events[0].StartQuarter, 3);
            Assert.Equal(1, events[0].DurationQuarter, 3);
        }

        [Fact]
        public void ScheduleMelodyNote_Accent_BoostsVelocityWithoutChangingDuration()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1));
            OrnamentService.TryAddOrnament(measure, 0, OrnamentType.Accent);

            var events = OrnamentPlaybackService.ScheduleMelodyNote(
                measure,
                measure.MelodyNotes[0],
                0,
                0,
                2,
                ScoreMidiSchedule.DefaultTonicMidi,
                ScoreMidiSchedule.MelodyChannel,
                ScoreMidiSchedule.MelodyVelocity);

            Assert.Single(events);
            Assert.Equal(2, events[0].DurationQuarter, 3);
            Assert.Equal(ScoreMidiSchedule.MelodyVelocity + OrnamentPlaybackService.AccentVelocityBoost, events[0].Velocity);
        }

        [Fact]
        public void ScheduleMelodyNote_Tenuto_AppliesSmallerVelocityBoostThanAccent()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1));
            OrnamentService.TryAddOrnament(measure, 0, OrnamentType.Tenuto);

            var events = OrnamentPlaybackService.ScheduleMelodyNote(
                measure,
                measure.MelodyNotes[0],
                0,
                0,
                2,
                ScoreMidiSchedule.DefaultTonicMidi,
                ScoreMidiSchedule.MelodyChannel,
                ScoreMidiSchedule.MelodyVelocity);

            Assert.Single(events);
            Assert.Equal(ScoreMidiSchedule.MelodyVelocity + OrnamentPlaybackService.TenutoVelocityBoost, events[0].Velocity);
            Assert.True(OrnamentPlaybackService.TenutoVelocityBoost < OrnamentPlaybackService.AccentVelocityBoost);
        }

        [Fact]
        public void ScheduleMelodyNote_StaccatoAndTrillTogether_ShortensEverySegment()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(3));
            OrnamentService.TryAddOrnament(measure, 0, OrnamentType.Trill);
            OrnamentService.TryAddOrnament(measure, 0, OrnamentType.Staccato);

            var events = OrnamentPlaybackService.ScheduleMelodyNote(
                measure,
                measure.MelodyNotes[0],
                0,
                0,
                1,
                ScoreMidiSchedule.DefaultTonicMidi,
                ScoreMidiSchedule.MelodyChannel,
                ScoreMidiSchedule.MelodyVelocity);

            Assert.True(events.Count >= 4);
            var expectedSegmentDuration = 1.0 / events.Count * OrnamentPlaybackService.StaccatoDurationMultiplier;
            Assert.Equal(expectedSegmentDuration, events[0].DurationQuarter, 3);
        }

        [Fact]
        public void ScheduleMelodyNote_VelocityBoost_NeverExceedsMidiMaximum()
        {
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1));
            OrnamentService.TryAddOrnament(measure, 0, OrnamentType.Accent);
            OrnamentService.TryAddOrnament(measure, 0, OrnamentType.Tenuto);

            var events = OrnamentPlaybackService.ScheduleMelodyNote(
                measure,
                measure.MelodyNotes[0],
                0,
                0,
                1,
                ScoreMidiSchedule.DefaultTonicMidi,
                ScoreMidiSchedule.MelodyChannel,
                120);

            Assert.Single(events);
            Assert.Equal(127, events[0].Velocity);
        }

        [Fact]
        public void Build_IncludesOrnamentExpandedMelodyNotes()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(ScoreTestHelper.Note(3)));
            OrnamentService.TryAddOrnament(score.Measures[0], 0, OrnamentType.Trill);

            var schedule = ScoreMidiSchedule.Build(score);
            var melody = schedule.Notes.Where(note => note.Channel == ScoreMidiSchedule.MelodyChannel).ToList();

            Assert.True(melody.Count >= 4);
        }
    }
}
