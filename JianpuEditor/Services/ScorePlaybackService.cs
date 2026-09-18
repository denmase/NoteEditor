using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Models;

namespace JianpuEditor.Services
{
    public sealed class ScorePlaybackService : IScorePlaybackService
    {
        private const int MinBpm = 30;
        private const int MaxBpm = 300;
        private const int DefaultBpm = 120;

        private readonly IMidiOutput _synthesizer;
        private readonly Timer _timer;
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly HashSet<long> _activeNotes = new HashSet<long>();

        private List<PlaybackTimelineEvent> _timeline = new List<PlaybackTimelineEvent>();
        private int _nextEventIndex;
        private double _totalQuarterLength;
        private double _playbackStartQuarter;
        private int _currentBpm = DefaultBpm;
        private bool _isPlaying;

        public ScorePlaybackService(IMidiOutput midiOutput = null)
        {
            _synthesizer = midiOutput ?? new WindowsMidiSynthesizer();
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

                _isPlaying = true;
                _stopwatch.Restart();
                _timer.Start();
                PositionChanged?.Invoke(PositionQuarter);
                AppLog.Info(
                    "Playback started: bpm=" + _currentBpm +
                    ", startQuarter=" + PositionQuarter.ToString("0.###") +
                    ", totalQuarter=" + _totalQuarterLength.ToString("0.###") +
                    ", events=" + _timeline.Count);
            }
            catch (Exception ex)
            {
                StopInternal(resetPosition: false);
                AppLog.Exception("Play failed", ex);
                throw;
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
            _timeline = BuildTimeline(schedule.Notes);
            _nextEventIndex = 0;

            var melodyInstrument = GeneralMidiInstruments.Clamp(score.MelodyInstrument);
            var chordInstrument = GeneralMidiInstruments.Clamp(score.ChordInstrument);
            _synthesizer.ProgramChange(ScoreMidiSchedule.MelodyChannel, melodyInstrument);
            _synthesizer.ProgramChange(ScoreMidiSchedule.ChordChannel, chordInstrument);

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

        // Does *not* dispose _synthesizer: it's the shared IMidiOutput singleton (the real
        // BASS/VST audio hardware handle), not something this instance owns -- now that this
        // service is scoped per document tab, disposing it here would kill audio for every other
        // open tab the moment any one tab closes. The root IServiceProvider disposes it exactly
        // once, at real application shutdown (see Program.cs).
        public void Dispose()
        {
            StopInternal(resetPosition: true);
            _timer.Dispose();
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
