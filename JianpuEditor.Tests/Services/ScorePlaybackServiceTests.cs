using JianpuEditor.Core.Abstractions;
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

        [Fact]
        public void Play_NotDdspConfigured_NeverRendersAndPlaysEverythingLive()
        {
            var midiOutput = new FakeMidiOutput();
            var ddspService = new FakeMidiDdspSynthesisService { IsConfigured = false };
            var ddspPlayer = new FakeDdspAudioPlayer();
            using var playback = new ScorePlaybackService(midiOutput, ddspService, ddspPlayer);
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2)));
            score.MelodyInstrument = 40; // Violin -- would be DDSP-eligible if configured

            playback.Play(score, bpm: 120);

            Assert.Equal(0, ddspService.RenderCallCount);
            // Notes only actually fire via the WinForms Timer's Tick (not observable from a plain
            // unit test with no message pump); the program change is sent synchronously by Play
            // itself, and is enough to confirm the melody channel stayed on the live path.
            Assert.Contains((ScoreMidiSchedule.MelodyChannel, 40), midiOutput.ProgramChanges);
        }

        [Fact]
        public void Play_MelodyInstrumentDdspEligible_ExcludesItFromLiveNotesAndRendersInstead()
        {
            var midiOutput = new FakeMidiOutput();
            var ddspService = new FakeMidiDdspSynthesisService { IsConfigured = true };
            ddspService.SupportedPrograms.Add(40); // Violin
            var renderedSamples = new float[] { 0.1f, 0.2f, 0.3f };
            ddspService.NextResult = new MidiDdspRenderResult
            {
                Samples = renderedSamples,
                SampleRate = 16000,
                RenderedChannels = new[] { ScoreMidiSchedule.MelodyChannel }
            };
            var ddspPlayer = new FakeDdspAudioPlayer();
            using var playback = new ScorePlaybackService(midiOutput, ddspService, ddspPlayer);
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2)));
            score.MelodyInstrument = 40; // Violin
            score.ChordInstrument = 0; // Piano -- stays live

            playback.Play(score, bpm: 120);

            Assert.True(ddspPlayer.WaitForLoadSamplesCount(1, TimeSpan.FromSeconds(5)), "render+load did not complete in time");
            Assert.Equal(1, ddspService.RenderCallCount);
            Assert.Empty(midiOutput.NotesOn); // melody was the only content, and it's DDSP-rendered now
            Assert.Same(renderedSamples, ddspPlayer.LoadedSamples);
            Assert.Equal(16000, ddspPlayer.SampleRate);
            Assert.NotNull(ddspPlayer.LastPlayStartSeconds);
            Assert.Equal(0d, ddspPlayer.LastPlayStartSeconds.Value);
        }

        [Fact]
        public void Play_SameScoreContentTwice_ReusesThePreviousRenderInsteadOfRenderingAgain()
        {
            var midiOutput = new FakeMidiOutput();
            var ddspService = new FakeMidiDdspSynthesisService { IsConfigured = true };
            ddspService.SupportedPrograms.Add(40);
            ddspService.NextResult = new MidiDdspRenderResult
            {
                Samples = new float[] { 0.5f },
                SampleRate = 16000,
                RenderedChannels = new[] { ScoreMidiSchedule.MelodyChannel }
            };
            var ddspPlayer = new FakeDdspAudioPlayer();
            using var playback = new ScorePlaybackService(midiOutput, ddspService, ddspPlayer);
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1)));
            score.MelodyInstrument = 40;

            playback.Play(score, bpm: 120);
            Assert.True(ddspPlayer.WaitForLoadSamplesCount(1, TimeSpan.FromSeconds(5)), "first render+load did not complete in time");
            playback.Play(score, bpm: 120);
            Assert.True(ddspPlayer.WaitForLoadSamplesCount(2, TimeSpan.FromSeconds(5)), "second Play's load did not complete in time");

            Assert.Equal(1, ddspService.RenderCallCount);
            Assert.Equal(2, ddspPlayer.LoadSamplesCallCount);
        }

        [Fact]
        public void Seek_RepositionsDdspAudioPlayerToMatchingSeconds()
        {
            var midiOutput = new FakeMidiOutput();
            var ddspPlayer = new FakeDdspAudioPlayer();
            using var playback = new ScorePlaybackService(midiOutput, new FakeMidiDdspSynthesisService(), ddspPlayer);
            var score = ScoreTestHelper.CreateScore(
                ScoreTestHelper.Measure(ScoreTestHelper.Note(1), ScoreTestHelper.Note(2), ScoreTestHelper.Note(3), ScoreTestHelper.Note(4)));

            playback.Play(score, bpm: 120);
            playback.Seek(2); // 2 quarters at 120bpm = 1 second

            Assert.NotNull(ddspPlayer.LastSeekSeconds);
            Assert.Equal(1d, ddspPlayer.LastSeekSeconds.Value);
        }
    }
}
