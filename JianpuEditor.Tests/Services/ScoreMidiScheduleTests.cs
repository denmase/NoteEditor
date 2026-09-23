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
        public void Build_ChordNotes_AddsADedicatedBassNoteOneOctaveBelowTheChord()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.MeasureWithChords(
                    new[] { "C" },
                    new[] { 0d },
                    ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4)));
            var rawNotes = ChordParser.ToBlockChordMidiNotes("C");

            var schedule = ScoreMidiSchedule.Build(score);
            var chordEvents = schedule.Notes.Where(n => n.Channel == ScoreMidiSchedule.ChordChannel).ToList();

            // The raw triad plus one dedicated bass note.
            Assert.Equal(rawNotes.Count + 1, chordEvents.Count);
            var lowest = chordEvents.Min(e => e.MidiNote);
            Assert.Equal(rawNotes.Min() - 12, lowest);
            // The bass note is louder than the rest -- it's meant to be the most audible element.
            // Guaranteed regardless of humanization jitter: the boost (10) exceeds twice the
            // jitter range (2*4=8), so the bass's worst case still beats the others' best case.
            var bassEvent = chordEvents.Single(e => e.MidiNote == lowest);
            Assert.True(bassEvent.Velocity > chordEvents.Where(e => e.MidiNote != lowest).Max(e => e.Velocity));
        }

        [Fact]
        public void Build_ConsecutiveChords_VoiceLeadingKeepsTheSecondChordCloseToTheFirst()
        {
            // Every chord's *raw* root-position voicing comes from the same fixed octave
            // (ChordParser.ToBlockChordMidiNotes' rootOctave default), so C's raw center sits at
            // roughly MIDI 60+3.7 and B's at roughly MIDI 71+3.7 -- an 11-semitone jump, audibly
            // a register change, if nothing corrected it. A real accompanist wouldn't jump nearly
            // an octave just because the chord symbol changed from C to B; voice leading should
            // pull B down an octave to sit close to where C left off instead.
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.MeasureWithChords(
                    new[] { "C", "B" },
                    new[] { 0d, 2d },
                    ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4)));

            var schedule = ScoreMidiSchedule.Build(score);
            var chordEvents = schedule.Notes
                .Where(n => n.Channel == ScoreMidiSchedule.ChordChannel)
                .OrderBy(n => n.StartQuarter)
                .ToList();

            var firstChordCenter = chordEvents.Where(e => e.StartQuarter < 2.0).Average(e => e.MidiNote);
            var secondChordCenter = chordEvents.Where(e => e.StartQuarter >= 2.0).Average(e => e.MidiNote);

            // The raw (un-voice-led) gap here is 11 semitones; voice leading should bring it well
            // under half that by shifting B down an octave to land close to C instead.
            Assert.True(System.Math.Abs(secondChordCenter - firstChordCenter) < 6);
        }

        [Fact]
        public void Build_ChordPlaybackStyleBlock_OneSustainedHitPerTone_AccentedWithJitter()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.MeasureWithChords(
                    new[] { "C" },
                    new[] { 0d },
                    ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4)));
            var rawNotes = ChordParser.ToBlockChordMidiNotes("C");

            var schedule = ScoreMidiSchedule.Build(score);
            var chordEvents = schedule.Notes.Where(n => n.Channel == ScoreMidiSchedule.ChordChannel).ToList();

            Assert.Equal(rawNotes.Count + 1, chordEvents.Count);
            Assert.All(chordEvents, e => Assert.Equal(0.0, e.StartQuarter, 3));
            Assert.All(chordEvents, e => Assert.Equal(4.0, e.DurationQuarter, 3));
            // Every note is a full (accented) attack -- Block never has a "secondary" re-strike.
            Assert.All(chordEvents, e => Assert.InRange(e.Velocity, 1, 127));
        }

        [Fact]
        public void Build_ChordPlaybackStyleComping_4_4_AlternatesBassAndChordEveryBeat()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.MeasureWithChords(
                    new[] { "C" },
                    new[] { 0d },
                    ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4)));
            score.ChordPlaybackStyle = ChordPlaybackStyle.Comping;
            score.TimeSignature = "4/4";
            var rawNotes = ChordParser.ToBlockChordMidiNotes("C");
            var bassPitch = rawNotes.Min() - 12;

            var schedule = ScoreMidiSchedule.Build(score);
            var chordEvents = schedule.Notes.Where(n => n.Channel == ScoreMidiSchedule.ChordChannel).ToList();

            // Beats 0 and 2: bass alone. Beats 1 and 3: the chord tones, bass excluded.
            var beat0 = chordEvents.Where(e => System.Math.Abs(e.StartQuarter - 0.0) < 0.001).ToList();
            var beat1 = chordEvents.Where(e => System.Math.Abs(e.StartQuarter - 1.0) < 0.001).ToList();
            var beat2 = chordEvents.Where(e => System.Math.Abs(e.StartQuarter - 2.0) < 0.001).ToList();
            var beat3 = chordEvents.Where(e => System.Math.Abs(e.StartQuarter - 3.0) < 0.001).ToList();

            Assert.Single(beat0);
            Assert.Equal(bassPitch, beat0[0].MidiNote);
            Assert.Equal(rawNotes.Count, beat1.Count);
            Assert.DoesNotContain(beat1, e => e.MidiNote == bassPitch);
            Assert.Single(beat2);
            Assert.Equal(bassPitch, beat2[0].MidiNote);
            Assert.Equal(rawNotes.Count, beat3.Count);

            // Detached (gated), not sustained for the full beat -- otherwise it would just be Block.
            Assert.All(chordEvents, e => Assert.True(e.DurationQuarter < 1.0));
        }

        [Fact]
        public void Build_ChordPlaybackStyleComping_3_4_UsesOomPahPahNotBoomChick()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.MeasureWithChords(
                    new[] { "C" },
                    new[] { 0d },
                    ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3)));
            score.ChordPlaybackStyle = ChordPlaybackStyle.Comping;
            score.TimeSignature = "3/4";
            var rawNotes = ChordParser.ToBlockChordMidiNotes("C");
            var bassPitch = rawNotes.Min() - 12;

            var schedule = ScoreMidiSchedule.Build(score);
            var chordEvents = schedule.Notes.Where(n => n.Channel == ScoreMidiSchedule.ChordChannel).ToList();

            var beat0 = chordEvents.Where(e => System.Math.Abs(e.StartQuarter - 0.0) < 0.001).ToList();
            var beat1 = chordEvents.Where(e => System.Math.Abs(e.StartQuarter - 1.0) < 0.001).ToList();
            var beat2 = chordEvents.Where(e => System.Math.Abs(e.StartQuarter - 2.0) < 0.001).ToList();

            // "Oom" (bass alone) then "pah-pah" (chord, chord) -- only one bass hit per 3-beat
            // cycle, not one every other beat the way 4/4's boom-chick would.
            Assert.Single(beat0);
            Assert.Equal(bassPitch, beat0[0].MidiNote);
            Assert.Equal(rawNotes.Count, beat1.Count);
            Assert.DoesNotContain(beat1, e => e.MidiNote == bassPitch);
            Assert.Equal(rawNotes.Count, beat2.Count);
            Assert.DoesNotContain(beat2, e => e.MidiNote == bassPitch);
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
        public void Build_ChordPlaybackStyleArpeggio_AccentsTheStartOfEachUpDownCycle()
        {
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.MeasureWithChords(
                    new[] { "C" },
                    new[] { 0d },
                    ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4)));
            score.ChordPlaybackStyle = ChordPlaybackStyle.Arpeggio;

            var schedule = ScoreMidiSchedule.Build(score);
            var chordEvents = schedule.Notes
                .Where(n => n.Channel == ScoreMidiSchedule.ChordChannel)
                .OrderBy(n => n.StartQuarter)
                .ToList();

            // Step 0 starts a new up-down cycle (and lands on the bass note) so it's accented;
            // step 1 is a secondary re-strike. Guaranteed regardless of jitter: accent velocity
            // (82) plus the bass boost (10), even at worst-case jitter (-4), still beats secondary
            // velocity (62) at its best-case jitter (+4): 88 > 66.
            Assert.True(chordEvents[0].Velocity > chordEvents[1].Velocity);
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
            var rawNotes = ChordParser.ToBlockChordMidiNotes("C");

            var schedule = ScoreMidiSchedule.Build(score);
            var chordEvents = schedule.Notes
                .Where(n => n.Channel == ScoreMidiSchedule.ChordChannel)
                .OrderBy(n => n.StartQuarter)
                .ToList();

            Assert.Equal(rawNotes.Count + 1, chordEvents.Count);
            // Every tone's onset is later than the previous one -- a real (if tiny) stagger.
            for (var i = 1; i < chordEvents.Count; i++)
            {
                Assert.True(chordEvents[i].StartQuarter > chordEvents[i - 1].StartQuarter);
            }

            // Pitches ascend low to high (typical downstrum), starting with the new bass note.
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

        [Fact]
        public void GetExtraVoiceInstrument_NoOverride_FallsBackToDefault()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));

            Assert.Equal(ScoreMidiSchedule.DefaultExtraVoiceInstrument, ScoreMidiSchedule.GetExtraVoiceInstrument(score, 0));
        }

        [Fact]
        public void GetExtraVoiceInstrument_Overridden_ReturnsClampedOverride()
        {
            var score = ScoreTestHelper.CreateScore(ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));
            score.ExtraVoiceInstruments.Add(40); // Violin
            score.ExtraVoiceInstruments.Add(999); // out of range, should clamp

            Assert.Equal(40, ScoreMidiSchedule.GetExtraVoiceInstrument(score, 0));
            Assert.Equal(127, ScoreMidiSchedule.GetExtraVoiceInstrument(score, 1));
            // A slot beyond the explicit list still falls back to the default.
            Assert.Equal(ScoreMidiSchedule.DefaultExtraVoiceInstrument, ScoreMidiSchedule.GetExtraVoiceInstrument(score, 2));
        }

        [Fact]
        public void GetExtraVoiceRoleLabels_ReturnsFirstLabelSeenPerSlot()
        {
            var m0 = ScoreTestHelper.Measure(ScoreTestHelper.Note(1));
            m0.ExtraVoices.Add(new JianpuVoice { Role = "Alto", Notes = { ScoreTestHelper.Note(1) } });
            var m1 = ScoreTestHelper.Measure(ScoreTestHelper.Note(2));
            m1.ExtraVoices.Add(new JianpuVoice { Role = "Alto", Notes = { ScoreTestHelper.Note(1) } });
            m1.ExtraVoices.Add(new JianpuVoice { Role = "Tenor", Notes = { ScoreTestHelper.Note(1) } });
            var score = ScoreTestHelper.CreateScore(m0, m1);

            var labels = ScoreMidiSchedule.GetExtraVoiceRoleLabels(score);

            Assert.Equal(new[] { "Alto", "Tenor" }, labels);
        }
    }
}
