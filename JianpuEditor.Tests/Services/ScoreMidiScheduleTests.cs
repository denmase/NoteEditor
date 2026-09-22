using System.Linq;
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
        public void Build_ChordPlaybackStyleBlock_OneSustainedHitPerChordTone()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.MeasureWithChords(
                    new[] { "C" },
                    new[] { 0d },
                    ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4)));
            var chordToneCount = ChordParser.ToBlockChordMidiNotes("C").Count;

            var schedule = ScoreMidiSchedule.Build(score);
            var chordEvents = schedule.Notes.Where(n => n.Channel == ScoreMidiSchedule.ChordChannel).ToList();

            Assert.Equal(chordToneCount, chordEvents.Count);
            Assert.All(chordEvents, e => Assert.Equal(0.0, e.StartQuarter, 3));
            Assert.All(chordEvents, e => Assert.Equal(4.0, e.DurationQuarter, 3));
        }

        [Fact]
        public void Build_ChordPlaybackStyleComping_ReStrikesOnEveryBeat()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.MeasureWithChords(
                    new[] { "C" },
                    new[] { 0d },
                    ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4)));
            score.ChordPlaybackStyle = ChordPlaybackStyle.Comping;
            var chordToneCount = ChordParser.ToBlockChordMidiNotes("C").Count;

            var schedule = ScoreMidiSchedule.Build(score);
            var chordEvents = schedule.Notes.Where(n => n.Channel == ScoreMidiSchedule.ChordChannel).ToList();

            // A 4-beat chord re-struck every beat produces 4 re-strikes, each with every chord tone.
            Assert.Equal(chordToneCount * 4, chordEvents.Count);
            var starts = chordEvents.Select(e => e.StartQuarter).Distinct().OrderBy(s => s).ToList();
            Assert.Equal(new[] { 0.0, 1.0, 2.0, 3.0 }, starts);
            // Detached (gated), not sustained for the full beat -- otherwise it would just be Block.
            Assert.All(chordEvents, e => Assert.True(e.DurationQuarter < 1.0));
        }

        [Fact]
        public void Build_ChordPlaybackStyleArpeggio_BreaksChordIntoOneNoteAtATime()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.MeasureWithChords(
                    new[] { "C" },
                    new[] { 0d },
                    ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4)));
            score.ChordPlaybackStyle = ChordPlaybackStyle.Arpeggio;

            var schedule = ScoreMidiSchedule.Build(score);
            var chordEvents = schedule.Notes.Where(n => n.Channel == ScoreMidiSchedule.ChordChannel).ToList();

            // 4 beats / 0.5-beat steps = 8 single-note events, not chord-tone-count-per-step.
            Assert.Equal(8, chordEvents.Count);
            Assert.All(chordEvents, e => Assert.True(e.DurationQuarter <= 0.5));
            var starts = chordEvents.Select(e => e.StartQuarter).OrderBy(s => s).ToList();
            var expectedStarts = new[] { 0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5 };
            for (var i = 0; i < expectedStarts.Length; i++)
            {
                Assert.Equal(expectedStarts[i], starts[i], 3);
            }
        }

        [Fact]
        public void Build_ChordPlaybackStyleStrum_StaggersEachTonesOnset()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.MeasureWithChords(
                    new[] { "C" },
                    new[] { 0d },
                    ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4)));
            score.ChordPlaybackStyle = ChordPlaybackStyle.Strum;
            var chordToneCount = ChordParser.ToBlockChordMidiNotes("C").Count;

            var schedule = ScoreMidiSchedule.Build(score);
            var chordEvents = schedule.Notes
                .Where(n => n.Channel == ScoreMidiSchedule.ChordChannel)
                .OrderBy(n => n.StartQuarter)
                .ToList();

            Assert.Equal(chordToneCount, chordEvents.Count);
            // Every tone's onset is later than the previous one -- a real (if tiny) stagger.
            for (var i = 1; i < chordEvents.Count; i++)
            {
                Assert.True(chordEvents[i].StartQuarter > chordEvents[i - 1].StartQuarter);
            }

            // Pitches ascend low to high (typical downstrum), even though ToBlockChordMidiNotes
            // doesn't return them in pitch order for a slash chord.
            for (var i = 1; i < chordEvents.Count; i++)
            {
                Assert.True(chordEvents[i].MidiNote >= chordEvents[i - 1].MidiNote);
            }
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
        public void Build_CrescendoHairpinWithExplicitEndMarking_RampsVelocityBetweenTheTwoLevels()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(
                    ScoreTestHelper.Note(1),
                    ScoreTestHelper.Note(2),
                    ScoreTestHelper.Note(3),
                    ScoreTestHelper.Note(4)));
            DynamicMarkingService.TrySetForNote(score.Measures[0], 0, "p");
            DynamicMarkingService.TrySetForNote(score.Measures[0], 3, "ff");
            score.Hairpins.Add(new JianpuHairpin
            {
                StartMeasureIndex = 0,
                StartNoteIndex = 0,
                EndMeasureIndex = 0,
                EndNoteIndex = 3,
                IsCrescendo = true
            });

            var schedule = ScoreMidiSchedule.Build(score);
            var melodyNotes = schedule.Notes
                .Where(note => note.Channel == ScoreMidiSchedule.MelodyChannel)
                .OrderBy(note => note.StartQuarter)
                .ToList();

            Assert.Equal(DynamicMarkingPlaybackService.PianoVelocity, melodyNotes[0].Velocity);
            Assert.Equal(DynamicMarkingPlaybackService.FortissimoVelocity, melodyNotes[3].Velocity);
            // Strictly increasing in between -- a real ramp, not a step straight from p to ff.
            Assert.True(melodyNotes[1].Velocity > melodyNotes[0].Velocity);
            Assert.True(melodyNotes[2].Velocity > melodyNotes[1].Velocity);
            Assert.True(melodyNotes[3].Velocity > melodyNotes[2].Velocity);
        }

        [Fact]
        public void Build_DiminuendoHairpinWithNoExplicitEndMarking_AppliesNominalDrop()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2)));
            score.Hairpins.Add(new JianpuHairpin
            {
                StartMeasureIndex = 0,
                StartNoteIndex = 0,
                EndMeasureIndex = 0,
                EndNoteIndex = 1,
                IsCrescendo = false
            });

            var schedule = ScoreMidiSchedule.Build(score);
            var melodyNotes = schedule.Notes
                .Where(note => note.Channel == ScoreMidiSchedule.MelodyChannel)
                .OrderBy(note => note.StartQuarter)
                .ToList();

            Assert.Equal(ScoreMidiSchedule.MelodyVelocity, melodyNotes[0].Velocity);
            Assert.Equal(
                ScoreMidiSchedule.MelodyVelocity - DynamicMarkingPlaybackService.NominalHairpinVelocityDelta,
                melodyNotes[1].Velocity);
        }

        [Fact]
        public void Build_NoHairpins_SchedulesByteIdenticalVelocityToBeforeHairpinsExisted()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3)));

            var schedule = ScoreMidiSchedule.Build(score);
            var melodyNotes = schedule.Notes.Where(note => note.Channel == ScoreMidiSchedule.MelodyChannel).ToList();

            Assert.All(melodyNotes, note => Assert.Equal(ScoreMidiSchedule.MelodyVelocity, note.Velocity));
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

        [Fact]
        public void Build_NoExtraVoices_SchedulesEveryNoteOnTheMelodyChannel()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(
                    ScoreTestHelper.Note(1),
                    ScoreTestHelper.Note(2),
                    ScoreTestHelper.Note(3),
                    ScoreTestHelper.Note(4)));

            var schedule = ScoreMidiSchedule.Build(score);

            Assert.Equal(4, schedule.Notes.Count);
            Assert.All(schedule.Notes, note => Assert.Equal(ScoreMidiSchedule.MelodyChannel, note.Channel));
        }

        [Fact]
        public void Build_SatbMeasure_SchedulesEachExtraVoiceOnItsOwnChannelStartingTogether()
        {
            var measure = ScoreTestHelper.Measure(
                ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4));
            measure.ExtraVoices.Add(new JianpuVoice
            {
                Role = "Alto",
                Notes = { ScoreTestHelper.Note(5), ScoreTestHelper.Note(5), ScoreTestHelper.Note(5), ScoreTestHelper.Note(5) }
            });
            measure.ExtraVoices.Add(new JianpuVoice
            {
                Role = "Tenor",
                Notes = { ScoreTestHelper.Note(3), ScoreTestHelper.Note(3), ScoreTestHelper.Note(3), ScoreTestHelper.Note(3) }
            });
            var score = ScoreTestHelper.CreateScore(measure);

            var schedule = ScoreMidiSchedule.Build(score);
            var alto = schedule.Notes.Where(note => note.Channel == ScoreMidiSchedule.ExtraVoiceChannelBase + 0).OrderBy(n => n.StartQuarter).ToList();
            var tenor = schedule.Notes.Where(note => note.Channel == ScoreMidiSchedule.ExtraVoiceChannelBase + 1).OrderBy(n => n.StartQuarter).ToList();

            Assert.Equal(4, alto.Count);
            Assert.Equal(4, tenor.Count);
            Assert.Equal(0d, alto[0].StartQuarter);
            Assert.Equal(1d, alto[1].StartQuarter);
            Assert.Equal(0d, tenor[0].StartQuarter);
            Assert.Equal(2, schedule.ExtraVoiceChannelCount);
        }

        [Fact]
        public void Build_NoExtraVoices_ReportsZeroExtraVoiceChannels()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));

            var schedule = ScoreMidiSchedule.Build(score);

            Assert.Equal(0, schedule.ExtraVoiceChannelCount);
        }

        [Fact]
        public void Build_ExtraVoiceMissingFromALaterMeasure_ResyncsInsteadOfDrifting()
        {
            var m0 = ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2));
            m0.ExtraVoices.Add(new JianpuVoice { Role = "Alto", Notes = { ScoreTestHelper.Note(5), ScoreTestHelper.Note(5) } });
            var m1 = ScoreTestHelper.Measure(ScoreTestHelper.Note(3), ScoreTestHelper.Note(4)); // no Alto content here
            var m2 = ScoreTestHelper.Measure(ScoreTestHelper.Note(5), ScoreTestHelper.Note(6));
            m2.ExtraVoices.Add(new JianpuVoice { Role = "Alto", Notes = { ScoreTestHelper.Note(1), ScoreTestHelper.Note(1) } });
            var score = ScoreTestHelper.CreateScore(m0, m1, m2);

            var schedule = ScoreMidiSchedule.Build(score);
            var alto = schedule.Notes
                .Where(note => note.Channel == ScoreMidiSchedule.ExtraVoiceChannelBase + 0)
                .OrderBy(note => note.StartQuarter)
                .ToList();

            Assert.Equal(new[] { 0d, 1d, 4d, 5d }, alto.Select(note => note.StartQuarter).ToArray());
        }
    }
}
