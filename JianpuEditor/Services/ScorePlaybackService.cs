using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Models;
using Newtonsoft.Json;

namespace JianpuEditor.Services
{
    public sealed class ScorePlaybackService : IScorePlaybackService
    {
        private const int MinBpm = 30;
        private const int MaxBpm = 300;
        private const int DefaultBpm = 120;

        private readonly IMidiOutput _synthesizer;
        private readonly IMidiDdspSynthesisService _ddspService;
        private readonly IDdspAudioPlayer _ddspAudioPlayer;
        private readonly Timer _timer;
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly HashSet<long> _activeNotes = new HashSet<long>();

        private List<PlaybackTimelineEvent> _timeline = new List<PlaybackTimelineEvent>();
        private int _nextEventIndex;
        private double _totalQuarterLength;
        private double _playbackStartQuarter;
        private int _currentBpm = DefaultBpm;
        private bool _isPlaying;

        private HashSet<int> _ddspEligibleChannels = new HashSet<int>();
        private string _ddspCacheKey;
        private MidiDdspRenderResult _ddspRenderResult;
        private System.Threading.CancellationTokenSource _ddspRenderCts;

        public ScorePlaybackService(
            IMidiOutput midiOutput = null,
            IMidiDdspSynthesisService ddspService = null,
            IDdspAudioPlayer ddspAudioPlayer = null)
        {
            _synthesizer = midiOutput ?? new WindowsMidiSynthesizer();
            _ddspService = ddspService ?? new MidiDdspSynthesisService();
            _ddspAudioPlayer = ddspAudioPlayer ?? new BassDdspAudioPlayer();
            _timer = new Timer { Interval = 15 };
            _timer.Tick += OnTimerTick;
            AppLog.Info("ScorePlaybackService initialized");
        }

        public bool IsPlaying => _isPlaying;

        public double PositionQuarter { get; private set; }

        public double TotalQuarterLength => _totalQuarterLength;

        public event Action<double> PositionChanged;

        public event Action PlaybackFinished;

        public event Action<Exception> PlaybackError;

        public event Action<string> RenderingStatusChanged;

        public void Prepare(JianpuScore score, double startQuarter = 0)
        {
            try
            {
                StopInternal(resetPosition: false);
                LoadTimeline(score);
                PositionQuarter = ClampQuarter(startQuarter, _totalQuarterLength);
                _playbackStartQuarter = PositionQuarter;
                SkipTimelineTo(PositionQuarter);
                PositionChanged?.Invoke(PositionQuarter);
                AppLog.Info(
                    "Playback prepared: startQuarter=" + PositionQuarter.ToString("0.###") +
                    ", totalQuarter=" + _totalQuarterLength.ToString("0.###"));
            }
            catch (Exception ex)
            {
                AppLog.Exception("Prepare failed", ex);
                throw;
            }
        }

        public void Play(JianpuScore score, int bpm, double startQuarter = 0)
        {
            if (score == null)
            {
                throw new ArgumentNullException(nameof(score));
            }

            try
            {
                StopInternal(resetPosition: false);
                LoadTimeline(score);
                _currentBpm = ClampBpm(bpm);
                PositionQuarter = ClampQuarter(startQuarter, _totalQuarterLength);
                _playbackStartQuarter = PositionQuarter;
                SkipTimelineTo(PositionQuarter);

                StartPlaybackConsideringDdsp(score);
            }
            catch (Exception ex)
            {
                StopInternal(resetPosition: false);
                AppLog.Exception("Play failed", ex);
                throw;
            }
        }

        /// <summary>
        /// Starts the live, per-note timeline immediately when no part of this score needs
        /// MIDI-DDSP rendering (the overwhelmingly common case -- no render delay, same as
        /// before this existed). Otherwise renders in the background (MIDI-DDSP can only render a
        /// whole part upfront, not stream note-by-note) and only starts playback -- of both the
        /// live timeline and the rendered audio, together -- once that finishes, reusing the
        /// previous render when nothing DDSP-relevant has changed since.
        /// </summary>
        private void StartPlaybackConsideringDdsp(JianpuScore score)
        {
            _ddspRenderCts?.Cancel();
            _ddspRenderCts = null;

            if (_ddspEligibleChannels.Count == 0)
            {
                ApplyDdspRenderResult(MidiDdspRenderResult.Empty);
                BeginLivePlayback();
                return;
            }

            var cacheKey = ComputeDdspCacheKey(score);
            if (cacheKey == _ddspCacheKey && _ddspRenderResult != null)
            {
                ApplyDdspRenderResult(_ddspRenderResult);
                BeginLivePlayback();
                return;
            }

            var cts = new System.Threading.CancellationTokenSource();
            _ddspRenderCts = cts;
            var uiContext = System.Threading.SynchronizationContext.Current;
            RenderingStatusChanged?.Invoke("Rendering neural instrument audio…");
            AppLog.Info("MIDI-DDSP render starting for " + _ddspEligibleChannels.Count + " channel(s)");

            _ddspService.RenderAsync(score, cts.Token).ContinueWith(task =>
            {
                void Continue()
                {
                    if (cts.IsCancellationRequested)
                    {
                        return;
                    }

                    RenderingStatusChanged?.Invoke(null);
                    if (task.IsFaulted)
                    {
                        AppLog.Exception("MIDI-DDSP render task failed", task.Exception);
                        ApplyDdspRenderResult(MidiDdspRenderResult.Empty);
                    }
                    else if (!task.IsCanceled)
                    {
                        _ddspRenderResult = task.Result;
                        _ddspCacheKey = cacheKey;
                        ApplyDdspRenderResult(_ddspRenderResult);
                        AppLog.Info("MIDI-DDSP render finished: hasAudio=" + _ddspRenderResult.HasAudio);
                    }

                    BeginLivePlayback();
                }

                if (uiContext != null)
                {
                    uiContext.Post(_ => Continue(), null);
                }
                else
                {
                    Continue();
                }
            }, TaskScheduler.Default);
        }

        private void ApplyDdspRenderResult(MidiDdspRenderResult result)
        {
            _ddspAudioPlayer.LoadSamples(result?.HasAudio == true ? result.Samples : null, result?.SampleRate ?? 0);
        }

        private void BeginLivePlayback()
        {
            _isPlaying = true;
            _stopwatch.Restart();
            _timer.Start();
            _ddspAudioPlayer.Play(SecondsFromQuarter(PositionQuarter));
            PositionChanged?.Invoke(PositionQuarter);
            AppLog.Info(
                "Playback started: bpm=" + _currentBpm +
                ", startQuarter=" + PositionQuarter.ToString("0.###") +
                ", totalQuarter=" + _totalQuarterLength.ToString("0.###") +
                ", events=" + _timeline.Count);
        }

        private double SecondsFromQuarter(double quarter)
        {
            return 60.0 / _currentBpm * quarter;
        }

        private HashSet<int> ComputeDdspEligibleChannels(JianpuScore score, ScoreMidiSchedule schedule)
        {
            var channels = new HashSet<int>();
            if (!_ddspService.IsConfigured)
            {
                return channels;
            }

            if (_ddspService.IsInstrumentSupported(score.MelodyInstrument))
            {
                channels.Add(ScoreMidiSchedule.MelodyChannel);
            }

            if (_ddspService.IsInstrumentSupported(score.ChordInstrument))
            {
                channels.Add(ScoreMidiSchedule.ChordChannel);
            }

            for (var voiceIndex = 0; voiceIndex < schedule.ExtraVoiceChannelCount; voiceIndex++)
            {
                if (_ddspService.IsInstrumentSupported(ScoreMidiSchedule.GetExtraVoiceInstrument(score, voiceIndex)))
                {
                    channels.Add(ScoreMidiSchedule.ExtraVoiceChannelBase + voiceIndex);
                }
            }

            return channels;
        }

        private static string ComputeDdspCacheKey(JianpuScore score)
        {
            var json = JsonConvert.SerializeObject(score);
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
                return Convert.ToBase64String(bytes);
            }
        }

        public void StopPlayback()
        {
            AppLog.Info("Playback stopped: positionQuarter=" + PositionQuarter.ToString("0.###"));
            StopInternal(resetPosition: false);
        }

        public void Seek(double quarterBeat)
        {
            try
            {
                var wasPlaying = _isPlaying;
                _synthesizer.AllNotesOff();
                _activeNotes.Clear();
                _nextEventIndex = 0;

                PositionQuarter = ClampQuarter(quarterBeat, _totalQuarterLength);
                _playbackStartQuarter = PositionQuarter;
                SkipTimelineTo(PositionQuarter);
                _ddspAudioPlayer.Seek(SecondsFromQuarter(PositionQuarter));

                if (wasPlaying)
                {
                    _isPlaying = true;
                    _stopwatch.Restart();
                    if (!_timer.Enabled)
                    {
                        _timer.Start();
                    }
                }
                else
                {
                    _isPlaying = false;
                    _stopwatch.Reset();
                }

                PositionChanged?.Invoke(PositionQuarter);
                AppLog.Info("Playback seek: quarter=" + PositionQuarter.ToString("0.###") + ", playing=" + wasPlaying);
            }
            catch (Exception ex)
            {
                AppLog.Exception("Seek failed", ex);
                HandlePlaybackError(ex);
                throw;
            }
        }

        private void LoadTimeline(JianpuScore score)
        {
            var schedule = ScoreMidiSchedule.Build(score);
            _totalQuarterLength = schedule.TotalQuarterLength;
            _ddspEligibleChannels = ComputeDdspEligibleChannels(score, schedule);
            // Notes on a DDSP-eligible channel are rendered into the separate DDSP audio buffer
            // (see StartPlaybackConsideringDdsp) instead, so excluded here to avoid playing the
            // same part twice through both the live SoundFont channel and the neural render.
            var liveNotes = _ddspEligibleChannels.Count == 0
                ? schedule.Notes
                : schedule.Notes.Where(note => !_ddspEligibleChannels.Contains(note.Channel)).ToList();
            _timeline = BuildTimeline(liveNotes);
            _nextEventIndex = 0;

            var melodyInstrument = GeneralMidiInstruments.Clamp(score.MelodyInstrument);
            var chordInstrument = GeneralMidiInstruments.Clamp(score.ChordInstrument);
            _synthesizer.ProgramChange(ScoreMidiSchedule.MelodyChannel, melodyInstrument);
            _synthesizer.ProgramChange(ScoreMidiSchedule.ChordChannel, chordInstrument);
            for (var voiceIndex = 0; voiceIndex < schedule.ExtraVoiceChannelCount; voiceIndex++)
            {
                _synthesizer.ProgramChange(
                    ScoreMidiSchedule.ExtraVoiceChannelBase + voiceIndex,
                    ScoreMidiSchedule.GetExtraVoiceInstrument(score, voiceIndex));
            }

            AppLog.Info(
                "Playback timeline loaded: melody+chord notes=" + schedule.Notes.Count +
                ", timelineEvents=" + _timeline.Count +
                ", totalQuarter=" + _totalQuarterLength.ToString("0.###") +
                ", melodyInstrument=" + melodyInstrument +
                ", chordInstrument=" + chordInstrument);
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            if (!_isPlaying)
            {
                return;
            }

            try
            {
                var msPerQuarter = 60000.0 / _currentBpm;
                var elapsedQuarter = _stopwatch.Elapsed.TotalMilliseconds / msPerQuarter;
                var currentQuarter = _playbackStartQuarter + elapsedQuarter;
                if (currentQuarter >= _totalQuarterLength)
                {
                    ProcessTimelineUntil(_totalQuarterLength);
                    PositionQuarter = _totalQuarterLength;
                    PositionChanged?.Invoke(PositionQuarter);
                    StopInternal(resetPosition: false);
                    AppLog.Info("Playback finished");
                    PlaybackFinished?.Invoke();
                    return;
                }

                ProcessTimelineUntil(currentQuarter);
                PositionQuarter = currentQuarter;
                PositionChanged?.Invoke(PositionQuarter);
            }
            catch (Exception ex)
            {
                AppLog.Exception("OnTimerTick failed", ex);
                HandlePlaybackError(ex);
            }
        }

        private void HandlePlaybackError(Exception ex)
        {
            StopInternal(resetPosition: false);
            PlaybackError?.Invoke(ex);
        }

        private void ProcessTimelineUntil(double quarterTime)
        {
            while (_nextEventIndex < _timeline.Count && _timeline[_nextEventIndex].TimeQuarter <= quarterTime + 0.0001)
            {
                var evt = _timeline[_nextEventIndex++];
                if (evt.IsNoteOn)
                {
                    _synthesizer.NoteOn(evt.Channel, evt.MidiNote, evt.Velocity);
                    _activeNotes.Add(PackActiveNote(evt.Channel, evt.MidiNote));
                }
                else
                {
                    _synthesizer.NoteOff(evt.Channel, evt.MidiNote);
                    _activeNotes.Remove(PackActiveNote(evt.Channel, evt.MidiNote));
                }
            }
        }

        private void SkipTimelineTo(double quarterTime)
        {
            _synthesizer.AllNotesOff();
            _activeNotes.Clear();
            _nextEventIndex = 0;
            while (_nextEventIndex < _timeline.Count && _timeline[_nextEventIndex].TimeQuarter <= quarterTime + 0.0001)
            {
                _nextEventIndex++;
            }
        }

        private void StopInternal(bool resetPosition)
        {
            _ddspRenderCts?.Cancel();
            _ddspRenderCts = null;
            RenderingStatusChanged?.Invoke(null);

            _timer.Stop();
            _stopwatch.Reset();
            _isPlaying = false;
            try
            {
                _synthesizer.AllNotesOff();
            }
            catch (Exception ex)
            {
                AppLog.Exception("StopInternal AllNotesOff failed", ex);
            }

            try
            {
                _ddspAudioPlayer.Stop();
            }
            catch (Exception ex)
            {
                AppLog.Exception("StopInternal DDSP audio stop failed", ex);
            }

            _activeNotes.Clear();
            _nextEventIndex = 0;
            if (resetPosition)
            {
                PositionQuarter = 0;
            }
        }

        private static List<PlaybackTimelineEvent> BuildTimeline(IList<ScheduledMidiNote> notes)
        {
            var events = new List<PlaybackTimelineEvent>(notes.Count * 2);
            foreach (var note in notes)
            {
                events.Add(new PlaybackTimelineEvent
                {
                    TimeQuarter = note.StartQuarter,
                    IsNoteOn = true,
                    MidiNote = note.MidiNote,
                    Channel = note.Channel,
                    Velocity = note.Velocity
                });
                events.Add(new PlaybackTimelineEvent
                {
                    TimeQuarter = note.StartQuarter + note.DurationQuarter,
                    IsNoteOn = false,
                    MidiNote = note.MidiNote,
                    Channel = note.Channel,
                    Velocity = 0
                });
            }

            events.Sort(CompareTimelineEvents);

            return events;
        }

        private static int CompareTimelineEvents(PlaybackTimelineEvent a, PlaybackTimelineEvent b)
        {
            var cmp = a.TimeQuarter.CompareTo(b.TimeQuarter);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = a.IsNoteOn.CompareTo(b.IsNoteOn);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = a.Channel.CompareTo(b.Channel);
            if (cmp != 0)
            {
                return cmp;
            }

            return a.MidiNote.CompareTo(b.MidiNote);
        }

        private static int ClampBpm(int bpm)
        {
            if (bpm <= 0)
            {
                return DefaultBpm;
            }

            return Math.Max(MinBpm, Math.Min(MaxBpm, bpm));
        }

        private static double ClampQuarter(double quarter, double total)
        {
            if (total <= 0)
            {
                return 0;
            }

            return Math.Max(0, Math.Min(total, quarter));
        }

        private static long PackActiveNote(int channel, int note)
        {
            return ((long)channel << 8) | (uint)note;
        }

        // Does *not* dispose _synthesizer or _ddspService: they're shared singletons (the real
        // BASS/VST audio hardware handle, and the cached MIDI-DDSP model respectively), not
        // something this instance owns -- now that this service is scoped per document tab,
        // disposing them here would kill audio (or force a costly model reload) for every other
        // open tab the moment any one tab closes. The root IServiceProvider disposes IMidiOutput
        // exactly once, at real application shutdown (see Program.cs); IMidiDdspSynthesisService
        // holds no unmanaged resources, so it needs no disposal at all. _ddspAudioPlayer, by
        // contrast, is constructed by (and scoped to) this instance like _timer, so it is disposed
        // here.
        public void Dispose()
        {
            StopInternal(resetPosition: true);
            _timer.Dispose();
            _ddspAudioPlayer.Dispose();
            AppLog.Info("ScorePlaybackService disposed");
        }

        private sealed class PlaybackTimelineEvent
        {
            public double TimeQuarter { get; set; }

            public bool IsNoteOn { get; set; }

            public int MidiNote { get; set; }

            public int Channel { get; set; }

            public int Velocity { get; set; }
        }
    }
}
