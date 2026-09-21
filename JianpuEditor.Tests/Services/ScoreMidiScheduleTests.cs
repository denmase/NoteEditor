using JianpuEditor.Models;
using JianpuEditor.Rendering;
using JianpuEditor.Services;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public class ScoreMidiScheduleTests
    {
        [Fact]
        public void GetMeasureDurationUnits_UsesNoteDurationsWhenPresent()
        {
            var measure = ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1, dashes: 1),
                ScoreTestHelper.Note(2, underlines: 1));

            var duration = ScoreMidiSchedule.GetMeasureDurationUnits(measure);

            Assert.Equal(2.5, duration, 3);
        }

        [Fact]
        public void GetMeasureDurationUnits_FallsBackToDefaultBeatsForEmptyMeasure()
        {
            var duration = ScoreMidiSchedule.GetMeasureDurationUnits(new JianpuMeasure());

            Assert.Equal(ScoreMidiSchedule.DefaultMeasureBeats, duration);
        }

        [Fact]
        public void Build_SchedulesMelodyAndChordNotes()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.MeasureWithChords(
                    new[] { "C" },
                    new[] { 0d },
                    ScoreTestHelper.Note(1),
                    ScoreTestHelper.Note(2),
                    ScoreTestHelper.Note(3),
                    ScoreTestHelper.Note(4)));

            var schedule = ScoreMidiSchedule.Build(score);

            Assert.True(schedule.TotalQuarterLength >= 4);
            Assert.Contains(schedule.Notes, note => note.Channel == ScoreMidiSchedule.MelodyChannel);
            Assert.Contains(schedule.Notes, note => note.Channel == ScoreMidiSchedule.ChordChannel);
        }

        [Fact]
        public void Build_PickupMeasure_SecondMeasureStartsRightAfterShortFirstMeasure()
        {
            // A pickup/anacrusis measure (fewer beats than the nominal time signature) isn't
            // padded or reflowed to a fixed beat count anywhere in the schedule-building path --
            // each measure's contribution to quarterTime comes purely from its own notes' actual
            // durations, so a short first measure just makes the next one start earlier.
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1, dashes: 1)),
                ScoreTestHelper.Measure(
                    ScoreTestHelper.Note(2),
                    ScoreTestHelper.Note(3),
                    ScoreTestHelper.Note(4),
                    ScoreTestHelper.Note(5)));

            var schedule = ScoreMidiSchedule.Build(score);
            var melodyNotes = schedule.Notes
                .Where(note => note.Channel == ScoreMidiSchedule.MelodyChannel)
                .OrderBy(note => note.StartQuarter)
                .ToList();

            Assert.Equal(0, melodyNotes[0].StartQuarter, 3);
            Assert.Equal(2, melodyNotes[0].DurationQuarter, 3);
            Assert.Equal(2, melodyNotes[1].StartQuarter, 3);
            Assert.Equal(6, schedule.TotalQuarterLength, 3);
        }

        [Fact]
        public void Build_NoDynamicMarkings_UsesDefaultMelodyVelocityForEveryNote()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2)));

            var schedule = ScoreMidiSchedule.Build(score);
            var melodyNotes = schedule.Notes.Where(note => note.Channel == ScoreMidiSchedule.MelodyChannel).ToList();

            Assert.All(melodyNotes, note => Assert.Equal(ScoreMidiSchedule.MelodyVelocity, note.Velocity));
        }

        [Fact]
        public void Build_DynamicMarking_AppliesVelocityFromThatNoteOnwardAcrossMeasures()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2)),
                ScoreTestHelper.Measure(ScoreTestHelper.Note(3)));
            DynamicMarkingService.TrySetForNote(score.Measures[0], 1, "pp");

            var schedule = ScoreMidiSchedule.Build(score);
            var melodyNotes = schedule.Notes
                .Where(note => note.Channel == ScoreMidiSchedule.MelodyChannel)
                .OrderBy(note => note.StartQuarter)
                .ToList();

            Assert.Equal(ScoreMidiSchedule.MelodyVelocity, melodyNotes[0].Velocity);
            Assert.Equal(DynamicMarkingPlaybackService.PianissimoVelocity, melodyNotes[1].Velocity);
            Assert.Equal(DynamicMarkingPlaybackService.PianissimoVelocity, melodyNotes[2].Velocity);
        }

        [Fact]
        public void Build_SecondDynamicMarking_OverridesTheFirstFromItsNoteOnward()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(
                    ScoreTestHelper.Note(1),
                    ScoreTestHelper.Note(2),
                    ScoreTestHelper.Note(3)));
            DynamicMarkingService.TrySetForNote(score.Measures[0], 0, "ff");
            DynamicMarkingService.TrySetForNote(score.Measures[0], 2, "pp");

            var schedule = ScoreMidiSchedule.Build(score);
            var melodyNotes = schedule.Notes
                .Where(note => note.Channel == ScoreMidiSchedule.MelodyChannel)
                .OrderBy(note => note.StartQuarter)
                .ToList();

            Assert.Equal(DynamicMarkingPlaybackService.FortissimoVelocity, melodyNotes[0].Velocity);
            Assert.Equal(DynamicMarkingPlaybackService.FortissimoVelocity, melodyNotes[1].Velocity);
            Assert.Equal(DynamicMarkingPlaybackService.PianissimoVelocity, melodyNotes[2].Velocity);
        }

        [Fact]
        public void Build_SuppressesTieEndNotes()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1, dashes: 1),
                ScoreTestHelper.Note(1)));
            score.Ties.Add(new JianpuTie
            {
                StartMeasureIndex = 0,
                StartNoteIndex = 0,
                EndMeasureIndex = 0,
                EndNoteIndex = 1
            });

            var schedule = ScoreMidiSchedule.Build(score);
            var melodyNotes = schedule.Notes.Where(note => note.Channel == ScoreMidiSchedule.MelodyChannel).ToList();

            Assert.Single(melodyNotes);
            Assert.Equal(3, melodyNotes[0].DurationQuarter, 3);
        }

        [Theory]
        [InlineData(0, 0, 1)]
        [InlineData(1, 0, 2)]
        [InlineData(0, 1, 0.5)]
        public void GetDurationUnits_MatchesRenderer(int dashes, int underlines, double expected)
        {
            var note = ScoreTestHelper.Note(1, dashes: dashes, underlines: underlines);

            Assert.Equal(expected, JianpuRenderer.GetDurationUnits(note), 3);
        }

        [Fact]
        public void Build_ContinuationDot_ExtendsThePrecedingNoteInsteadOfSchedulingItsOwnEvent()
        {
            // Real notasi angka rhythm from the "Pertolongan-Mu" jianpu sheet: an eighth note held
            // through the second half of the beat by a beamed continuation dot ("5 .", one full beat
            // total split 0.5+0.5), matching JianpuNote.IsContinuation's doc comment.
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(
                ScoreTestHelper.Note(5, underlines: 1),
                ScoreTestHelper.ContinuationDot(underlines: 1),
                ScoreTestHelper.Note(6, underlines: 1),
                ScoreTestHelper.Note(6, underlines: 1)));

            var schedule = ScoreMidiSchedule.Build(score);
            var melodyNotes = schedule.Notes
                .Where(note => note.Channel == ScoreMidiSchedule.MelodyChannel)
                .OrderBy(note => note.StartQuarter)
                .ToList();

            Assert.Equal(3, melodyNotes.Count);
            Assert.Equal(0, melodyNotes[0].StartQuarter, 3);
            Assert.Equal(1, melodyNotes[0].DurationQuarter, 3);
        }

        [Fact]
        public void Build_TwoContinuationDots_EachExtendTheSameOriginalNoteOn()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1),
                ScoreTestHelper.ContinuationDot(),
                ScoreTestHelper.ContinuationDot(),
                ScoreTestHelper.Note(2)));

            var schedule = ScoreMidiSchedule.Build(score);
            var melodyNotes = schedule.Notes
                .Where(note => note.Channel == ScoreMidiSchedule.MelodyChannel)
                .OrderBy(note => note.StartQuarter)
                .ToList();

            Assert.Equal(2, melodyNotes.Count);
            Assert.Equal(3, melodyNotes[0].DurationQuarter, 3);
            Assert.Equal(3, melodyNotes[1].StartQuarter, 3);
        }

        [Fact]
        public void Build_ContinuationDotAfterATrueRest_IsANoOpRatherThanCrashing()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(
                ScoreTestHelper.Rest(),
                ScoreTestHelper.ContinuationDot(),
                ScoreTestHelper.Note(3)));

            var schedule = ScoreMidiSchedule.Build(score);
            var melodyNotes = schedule.Notes
                .Where(note => note.Channel == ScoreMidiSchedule.MelodyChannel)
                .ToList();

            Assert.Single(melodyNotes);
            Assert.Equal(2, melodyNotes[0].StartQuarter, 3);
        }

        [Fact]
        public void Build_ContinuationDotAfterATiedNote_ExtendsTheOriginalTieStartEvent()
        {
            // The tied-to note (index 1) is suppressed from scheduling its own event -- a
            // continuation dot right after it (index 2) must still reach back to the original
            // note-on at index 0, not the suppressed slot.
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1),
                ScoreTestHelper.Note(1),
                ScoreTestHelper.ContinuationDot()));
            score.Ties.Add(new JianpuTie
            {
                StartMeasureIndex = 0,
                StartNoteIndex = 0,
                EndMeasureIndex = 0,
                EndNoteIndex = 1
            });

            var schedule = ScoreMidiSchedule.Build(score);
            var melodyNotes = schedule.Notes
                .Where(note => note.Channel == ScoreMidiSchedule.MelodyChannel)
                .ToList();

            Assert.Single(melodyNotes);
            Assert.Equal(3, melodyNotes[0].DurationQuarter, 3);
        }

        [Fact]
        public void ComputeTotalQuarterLength_RepeatedMeasure_CountsItTwice()
        {
            var measure0 = ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2));
            var measure1 = ScoreTestHelper.Measure(ScoreTestHelper.Note(3), ScoreTestHelper.Note(4));
            measure1.BarLineType = BarLineType.RepeatEnd;
            var score = ScoreTestHelper.CreateScore(measure0, measure1);

            var total = ScoreMidiSchedule.ComputeTotalQuarterLength(score);

            Assert.Equal(8, total, 3);
        }

        [Fact]
        public void Build_RepeatedSection_SchedulesEachNoteOnceForEveryPassAtTheRightTime()
        {
            var measure0 = ScoreTestHelper.Measure(ScoreTestHelper.Note(1));
            var measure1 = ScoreTestHelper.Measure(ScoreTestHelper.Note(2));
            measure1.BarLineType = BarLineType.RepeatEnd;
            var score = ScoreTestHelper.CreateScore(measure0, measure1);

            var schedule = ScoreMidiSchedule.Build(score);
            var melodyNotes = schedule.Notes
                .Where(note => note.Channel == ScoreMidiSchedule.MelodyChannel)
                .OrderBy(note => note.StartQuarter)
                .ToList();

            Assert.Equal(4, melodyNotes.Count);
            Assert.Equal(new[] { 0d, 1d, 2d, 3d }, melodyNotes.Select(note => note.StartQuarter).ToArray());
        }

        [Fact]
        public void Build_FirstAndSecondEndings_OnlyScheduleTheMatchingVoltaOnEachPass()
        {
            var measure0 = ScoreTestHelper.Measure(ScoreTestHelper.Note(1)); // A
            var measure1 = ScoreTestHelper.Measure(ScoreTestHelper.Note(2)); // 1st ending, repeats
            measure1.BarLineType = BarLineType.RepeatEnd;
            var measure2 = ScoreTestHelper.Measure(ScoreTestHelper.Note(3)); // 2nd ending
            var score = ScoreTestHelper.CreateScore(measure0, measure1, measure2);
            score.Voltas.Add(new JianpuVolta { StartMeasureIndex = 1, EndMeasureIndex = 1, Label = "1." });
            score.Voltas.Add(new JianpuVolta { StartMeasureIndex = 2, EndMeasureIndex = 2, Label = "2." });

            var schedule = ScoreMidiSchedule.Build(score);
            var melodyNotes = schedule.Notes
                .Where(note => note.Channel == ScoreMidiSchedule.MelodyChannel)
                .OrderBy(note => note.StartQuarter)
                .ToList();

            // Expected play order: A(1), 1st ending(2), A(1) again, 2nd ending(3) -- the 1st
            // ending (pitch 2) plays exactly once, replaced by the 2nd ending (pitch 3) on the
            // repeat, not played a second time itself.
            var tonicMidi = 60; // KeySignature "1=C" from ScoreTestHelper.CreateScore
            var pitches = melodyNotes.Select(note => note.MidiNote).ToArray();
            Assert.Equal(new[] { tonicMidi, tonicMidi + 2, tonicMidi, tonicMidi + 4 }, pitches);
            Assert.Equal(new[] { 0d, 1d, 2d, 3d }, melodyNotes.Select(note => note.StartQuarter).ToArray());
        }
    }
}
