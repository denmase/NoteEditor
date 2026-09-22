using JianpuEditor.Models;
using JianpuEditor.Services;
using JianpuEditor.Tests.Helpers;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public class ScorePlaybackServiceTests
    {
        [Fact]
        public void Play_SendsProgramChangeForMelodyAndChordInstrumentsBeforeNotes()
        {
            var midiOutput = new FakeMidiOutput();
            using var playback = new ScorePlaybackService(midiOutput);
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2)));
            score.MelodyInstrument = 40;
            score.ChordInstrument = 24;

            playback.Play(score, bpm: 120);

            Assert.Contains((ScoreMidiSchedule.MelodyChannel, 40), midiOutput.ProgramChanges);
            Assert.Contains((ScoreMidiSchedule.ChordChannel, 24), midiOutput.ProgramChanges);
        }

        [Fact]
        public void Play_SendsProgramChangeForEveryExtraVoiceChannel()
        {
            var midiOutput = new FakeMidiOutput();
            using var playback = new ScorePlaybackService(midiOutput);
            var measure = ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2));
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Alto", Notes = { ScoreTestHelper.Note(5), ScoreTestHelper.Note(5) } });
            measure.ExtraVoices.Add(new JianpuVoice { Role = "Tenor", Notes = { ScoreTestHelper.Note(3), ScoreTestHelper.Note(3) } });
            var score = ScoreTestHelper.CreateScore(measure);

            playback.Play(score, bpm: 120);

            Assert.Contains(
                (ScoreMidiSchedule.ExtraVoiceChannelBase + 0, ScoreMidiSchedule.DefaultExtraVoiceInstrument),
                midiOutput.ProgramChanges);
            Assert.Contains(
                (ScoreMidiSchedule.ExtraVoiceChannelBase + 1, ScoreMidiSchedule.DefaultExtraVoiceInstrument),
                midiOutput.ProgramChanges);
        }

        [Fact]
        public void Play_DefaultsToAcousticGrandPianoWhenInstrumentsNotSet()
        {
            var midiOutput = new FakeMidiOutput();
            using var playback = new ScorePlaybackService(midiOutput);
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));

            playback.Play(score, bpm: 120);

            Assert.Contains((ScoreMidiSchedule.MelodyChannel, 0), midiOutput.ProgramChanges);
            Assert.Contains((ScoreMidiSchedule.ChordChannel, 0), midiOutput.ProgramChanges);
        }

        [Fact]
        public void Prepare_AlsoSendsProgramChangeForCurrentInstruments()
        {
            var midiOutput = new FakeMidiOutput();
            using var playback = new ScorePlaybackService(midiOutput);
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));
            score.MelodyInstrument = 73;

            playback.Prepare(score);

            Assert.Contains((ScoreMidiSchedule.MelodyChannel, 73), midiOutput.ProgramChanges);
        }

        [Fact]
        public void Play_OutOfRangeInstrumentIsClampedBeforeSending()
        {
            var midiOutput = new FakeMidiOutput();
            using var playback = new ScorePlaybackService(midiOutput);
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));
            score.MelodyInstrument = 200;
            score.ChordInstrument = -5;

            playback.Play(score, bpm: 120);

            Assert.Contains((ScoreMidiSchedule.MelodyChannel, 127), midiOutput.ProgramChanges);
            Assert.Contains((ScoreMidiSchedule.ChordChannel, 0), midiOutput.ProgramChanges);
        }
    }
}
