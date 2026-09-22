using System;
using System.Collections.Generic;
using System.Linq;
using JianpuEditor.Models;
using JianpuEditor.Rendering;

namespace JianpuEditor.Services
{
    public sealed class ScheduledMidiNote
    {
        public double StartQuarter { get; set; }

        public double DurationQuarter { get; set; }

        public int MidiNote { get; set; }

        public int Channel { get; set; }

        public int Velocity { get; set; }
    }

    public sealed class ScoreMidiSchedule
    {
        public const int MelodyChannel = 0;
        public const int ChordChannel = 1;
        public const int MelodyVelocity = 90;
        public const int ChordVelocity = 72;
        public const int DefaultMeasureBeats = 4;
        public const int DefaultTonicMidi = 60;

        private static readonly int[] MajorScaleOffsets = { 0, 2, 4, 5, 7, 9, 11 };

        public IList<ScheduledMidiNote> Notes { get; private set; } = new List<ScheduledMidiNote>();

        public double TotalQuarterLength { get; private set; }

        public static ScoreMidiSchedule Build(JianpuScore score)
        {
            if (score == null)
            {
                throw new ArgumentNullException(nameof(score));
            }

            var schedule = new ScoreMidiSchedule();
            var playOrder = SegnoCodaPlaybackExpander.ApplyNavigation(
                score.Measures,
                RepeatPlaybackExpander.Expand(score.Measures, score.Voltas));
            var melody = BuildMelodyNotes(score, playOrder);
            var chords = BuildChordNotes(score, playOrder);
            var notes = new List<ScheduledMidiNote>(melody.Count + chords.Count);
            notes.AddRange(melody);
            notes.AddRange(chords);
            schedule.Notes = notes;
            schedule.TotalQuarterLength = ComputeTotalQuarterLength(score, playOrder);
            return schedule;
        }

        /// <summary>Total playback length in quarter-note units, following repeats and volta
        /// skips (see <see cref="RepeatPlaybackExpander"/>) rather than one straight pass through
        /// <c>Measures</c> -- a score with a repeat sign genuinely takes longer to play than its
        /// raw measure count would suggest.</summary>
        public static double ComputeTotalQuarterLength(JianpuScore score)
        {
            if (score?.Measures == null)
            {
                return 0;
            }

            var playOrder = SegnoCodaPlaybackExpander.ApplyNavigation(
                score.Measures,
                RepeatPlaybackExpander.Expand(score.Measures, score.Voltas));
            return ComputeTotalQuarterLength(score, playOrder);
        }

        private static double ComputeTotalQuarterLength(JianpuScore score, IReadOnlyList<int> playOrder)
        {
            var total = 0.0;
            var measures = score?.Measures;
            if (measures == null)
            {
                return 0;
            }

            foreach (var measureIndex in playOrder)
            {
                var measure = measures[measureIndex];
                MelodyChordService.NormalizeMeasure(measure);
                total += GetMeasureDurationUnits(measure);
            }

            return total;
        }

        public static double GetMeasureDurationUnits(JianpuMeasure measure)
        {
            var duration = 0.0;
            var notes = measure?.MelodyNotes;
            if (notes != null)
            {
                foreach (var note in notes)
                {
                    duration += JianpuRenderer.GetDurationUnits(note);
                }
            }

            return duration > 0 ? duration : DefaultMeasureBeats;
        }

        private static List<ScheduledMidiNote> BuildMelodyNotes(JianpuScore score, IReadOnlyList<int> playOrder)
        {
            var tonicMidi = ParseTonicMidi(score.KeySignature);
            var suppressed = BuildTieEndSet(score.Ties);
            var tieExtensionCache = new Dictionary<NotePosition, double>();
            var hairpinOverrides = BuildHairpinVelocityOverrides(score);
            var events = new List<ScheduledMidiNote>();
            var quarterTime = 0.0;
            var currentVelocity = MelodyVelocity;
            // Tracks the event(s) for whatever melody note is currently sounding, so a later
            // continuation-dot slot ("." holding the previous pitch -- see JianpuNote.IsContinuation)
            // can extend them instead of starting a new note-on. Cleared on silence (a true rest, or
            // a slot with nothing playable) since there is nothing left for a dot to hold.
            var soundingEventIndices = new List<int>();

            var measures = score.Measures ?? new List<JianpuMeasure>();
            foreach (var measureIndex in playOrder)
            {
                var measure = measures[measureIndex];
                MelodyChordService.NormalizeMeasure(measure);
                DynamicMarkingService.NormalizeMeasure(measure);
                var notes = measure.MelodyNotes;
                if (notes == null)
                {
                    continue;
                }

                for (var noteIndex = 0; noteIndex < notes.Count; noteIndex++)
                {
                    var position = new NotePosition(measureIndex, noteIndex);
                    var dynamicMarking = DynamicMarkingService.GetMarkingForNote(measure, noteIndex);
                    if (dynamicMarking != null)
                    {
                        currentVelocity = DynamicMarkingPlaybackService.ResolveVelocity(dynamicMarking.Text, currentVelocity);
                    }

                    // A hairpin's interpolated level wins over -- and becomes -- the step-function
                    // level from here on, the same way an explicit discrete marking would: a
                    // crescendo/diminuendo with nothing marked after it holds at the level it
                    // reached rather than snapping back.
                    if (hairpinOverrides.TryGetValue(position, out var hairpinVelocity))
                    {
                        currentVelocity = hairpinVelocity;
                    }

                    var slotNote = notes[noteIndex];
                    var duration = JianpuRenderer.GetDurationUnits(slotNote);

                    if (suppressed.Contains(position))
                    {
                        // Tied-to note: GetTieExtension already folded this slot's duration into the
                        // tie start's own event, so soundingEventIndices correctly still points there
                        // -- a continuation dot right after a tie should keep extending that same
                        // original note-on, not this (unscheduled) slot.
                        quarterTime += duration;
                        continue;
                    }

                    if (slotNote.Type == NoteType.Rest && slotNote.IsContinuation)
                    {
                        foreach (var eventIndex in soundingEventIndices)
                        {
                            events[eventIndex].DurationQuarter += duration;
                        }

                        quarterTime += duration;
                        continue;
                    }

                    var chordNotes = MelodyChordService.GetNotesAtSlot(measure, noteIndex);
                    var playableNotes = chordNotes
                        .Where(note => note.Type == NoteType.Note && JianpuPitchCodec.IsValidMelodyPitch(note))
                        .ToList();
                    if (playableNotes.Count == 0)
                    {
                        soundingEventIndices.Clear();
                        quarterTime += duration;
                        continue;
                    }

                    var totalDuration = duration + GetTieExtension(score, position, tieExtensionCache);
                    soundingEventIndices = new List<int>();
                    if (playableNotes.Count == 1)
                    {
                        // Only a same-measure lookahead: real notation draws a glissando between
                        // two adjacent written notes, so a glissando on the last note of a measure
                        // (nothing left to slide toward within this slot's own context) simply has
                        // no playback effect rather than reaching into the next measure/repeat.
                        var nextNote = noteIndex + 1 < notes.Count ? notes[noteIndex + 1] : null;
                        var scheduled = OrnamentPlaybackService.ScheduleMelodyNote(
                            measure,
                            playableNotes[0],
                            noteIndex,
                            quarterTime,
                            totalDuration,
                            tonicMidi,
                            MelodyChannel,
                            currentVelocity,
                            nextNote);
                        events.AddRange(scheduled);
                        // A plain note schedules one event; an ornamented one (grace note, trill,
                        // turn, mordent...) can expand into several laid out in time order, so the
                        // LAST one is whichever is still sounding when this slot ends -- that's the
                        // one a later continuation dot should extend.
                        if (scheduled.Count > 0)
                        {
                            soundingEventIndices.Add(events.Count - 1);
                        }
                    }
                    else
                    {
                        foreach (var note in playableNotes)
                        {
                            events.Add(new ScheduledMidiNote
                            {
                                StartQuarter = quarterTime,
                                DurationQuarter = totalDuration,
                                MidiNote = ToMelodyMidiNote(note, tonicMidi),
                                Channel = MelodyChannel,
                                Velocity = currentVelocity
                            });
                            soundingEventIndices.Add(events.Count - 1);
                        }
                    }

                    quarterTime += duration;
                }
            }

            return events;
        }

        /// <summary>Resolves every <see cref="JianpuHairpin"/> into a per-note velocity override,
        /// keyed by score position (measure+note index) rather than elapsed playback time -- like
        /// <see cref="DynamicMarkingService"/>'s step-function markings, a hairpin's effect is a
        /// property of *where* a note sits in the score, so it reapplies identically on every pass
        /// through a repeated section instead of only affecting whichever pass happens to reach it
        /// first. Interpolation is by note ordinal within the span rather than by elapsed quarter-
        /// time, which keeps this a simple structural (not playback-order-dependent) pre-pass: the
        /// same span always contains the same notes regardless of how repeats later re-visit it.
        /// The start level is whatever the discrete step-function would already be at that position;
        /// the end level is an explicit marking at the end note if one exists, otherwise a nominal
        /// <see cref="DynamicMarkingPlaybackService.NominalHairpinVelocityDelta"/> nudge in the
        /// hairpin's direction.</summary>
        private static Dictionary<NotePosition, int> BuildHairpinVelocityOverrides(JianpuScore score)
        {
            var overrides = new Dictionary<NotePosition, int>();
            var hairpins = score?.Hairpins;
            var measures = score?.Measures;
            if (hairpins == null || hairpins.Count == 0 || measures == null)
            {
                return overrides;
            }

            var velocityAtPosition = new Dictionary<NotePosition, int>();
            var runningVelocity = MelodyVelocity;
            for (var measureIndex = 0; measureIndex < measures.Count; measureIndex++)
            {
                var measure = measures[measureIndex];
                DynamicMarkingService.NormalizeMeasure(measure);
                var notes = measure.MelodyNotes;
                if (notes == null)
                {
                    continue;
                }

                for (var noteIndex = 0; noteIndex < notes.Count; noteIndex++)
                {
                    var marking = DynamicMarkingService.GetMarkingForNote(measure, noteIndex);
                    if (marking != null)
                    {
                        runningVelocity = DynamicMarkingPlaybackService.ResolveVelocity(marking.Text, runningVelocity);
                    }

                    velocityAtPosition[new NotePosition(measureIndex, noteIndex)] = runningVelocity;
                }
            }

            foreach (var hairpin in hairpins)
            {
                var startPosition = new NotePosition(hairpin.StartMeasureIndex, hairpin.StartNoteIndex);
                if (!velocityAtPosition.TryGetValue(startPosition, out var startVelocity))
                {
                    continue;
                }

                var endVelocity = ResolveHairpinEndVelocity(score, hairpin, startVelocity);
                var span = GetPositionsBetween(measures, startPosition, new NotePosition(hairpin.EndMeasureIndex, hairpin.EndNoteIndex));
                for (var i = 0; i < span.Count; i++)
                {
                    var t = span.Count <= 1 ? 1.0 : (double)i / (span.Count - 1);
                    var velocity = (int)Math.Round(startVelocity + (endVelocity - startVelocity) * t);
                    overrides[span[i]] = Math.Max(1, Math.Min(127, velocity));
                }
            }

            return overrides;
        }

        private static int ResolveHairpinEndVelocity(JianpuScore score, JianpuHairpin hairpin, int startVelocity)
        {
            if (hairpin.EndMeasureIndex >= 0 && hairpin.EndMeasureIndex < score.Measures.Count)
            {
                var endMeasure = score.Measures[hairpin.EndMeasureIndex];
                DynamicMarkingService.NormalizeMeasure(endMeasure);
                var endMarking = DynamicMarkingService.GetMarkingForNote(endMeasure, hairpin.EndNoteIndex);
                if (endMarking != null)
                {
                    return DynamicMarkingPlaybackService.ResolveVelocity(endMarking.Text, startVelocity);
                }
            }

            var nominalDelta = hairpin.IsCrescendo
                ? DynamicMarkingPlaybackService.NominalHairpinVelocityDelta
                : -DynamicMarkingPlaybackService.NominalHairpinVelocityDelta;
            return Math.Max(1, Math.Min(127, startVelocity + nominalDelta));
        }

        private static List<NotePosition> GetPositionsBetween(IList<JianpuMeasure> measures, NotePosition start, NotePosition end)
        {
            var positions = new List<NotePosition>();
            for (var measureIndex = start.MeasureIndex; measureIndex <= end.MeasureIndex && measureIndex < measures.Count; measureIndex++)
            {
                var notes = measures[measureIndex].MelodyNotes;
                if (notes == null)
                {
                    continue;
                }

                var fromNote = measureIndex == start.MeasureIndex ? start.NoteIndex : 0;
                var toNote = measureIndex == end.MeasureIndex ? end.NoteIndex : notes.Count - 1;
                for (var noteIndex = fromNote; noteIndex <= toNote && noteIndex < notes.Count; noteIndex++)
                {
                    positions.Add(new NotePosition(measureIndex, noteIndex));
                }
            }

            return positions;
        }

        private static List<ScheduledMidiNote> BuildChordNotes(JianpuScore score, IReadOnlyList<int> playOrder)
        {
            var events = new List<ScheduledMidiNote>();
            var measures = score.Measures ?? new List<JianpuMeasure>();
            var measureStart = 0.0;

            foreach (var measureIndex in playOrder)
            {
                var measure = measures[measureIndex];
                var measureDuration = GetMeasureDurationUnits(measure);
                var chordSymbols = ChordParser.ExtractScheduledChords(measure);
                if (chordSymbols.Count > 0)
                {
                    for (var chordIndex = 0; chordIndex < chordSymbols.Count; chordIndex++)
                    {
                        var chord = chordSymbols[chordIndex];
                        var chordStart = measureStart + chord.BeatPosition;
                        var chordEnd = chordIndex + 1 < chordSymbols.Count
                            ? measureStart + chordSymbols[chordIndex + 1].BeatPosition
                            : measureStart + measureDuration;
                        var chordDuration = Math.Max(0.01, chordEnd - chordStart);
                        var midiNotes = ChordParser.ToBlockChordMidiNotes(chord.Symbol);
                        foreach (var midiNote in midiNotes)
                        {
                            events.Add(new ScheduledMidiNote
                            {
                                StartQuarter = chordStart,
                                DurationQuarter = chordDuration,
                                MidiNote = midiNote,
                                Channel = ChordChannel,
                                Velocity = ChordVelocity
                            });
                        }
                    }
                }

                measureStart += measureDuration;
            }

            return events;
        }

        private static HashSet<NotePosition> BuildTieEndSet(IList<JianpuTie> ties)
        {
            var set = new HashSet<NotePosition>();
            if (ties == null)
            {
                return set;
            }

            foreach (var tie in ties)
            {
                set.Add(new NotePosition(tie.EndMeasureIndex, tie.EndNoteIndex));
            }

            return set;
        }

        private static double GetTieExtension(
            JianpuScore score,
            NotePosition start,
            IDictionary<NotePosition, double> cache)
        {
            if (cache.TryGetValue(start, out var cached))
            {
                return cached;
            }

            var extension = 0.0;
            var ties = score.Ties ?? new List<JianpuTie>();
            foreach (var tie in ties)
            {
                if (tie.StartMeasureIndex != start.MeasureIndex || tie.StartNoteIndex != start.NoteIndex)
                {
                    continue;
                }

                var endNote = GetNote(score, tie.EndMeasureIndex, tie.EndNoteIndex);
                if (endNote == null)
                {
                    continue;
                }

                var endPosition = new NotePosition(tie.EndMeasureIndex, tie.EndNoteIndex);
                var endDuration = JianpuRenderer.GetDurationUnits(endNote);
                extension += endDuration + GetTieExtension(score, endPosition, cache);
            }

            cache[start] = extension;
            return extension;
        }

        private static JianpuNote GetNote(JianpuScore score, int measureIndex, int noteIndex)
        {
            if (score.Measures == null || measureIndex < 0 || measureIndex >= score.Measures.Count)
            {
                return null;
            }

            var notes = score.Measures[measureIndex].MelodyNotes;
            if (notes == null || noteIndex < 0 || noteIndex >= notes.Count)
            {
                return null;
            }

            return notes[noteIndex];
        }

        public static int ToMelodyMidiNote(JianpuNote note, int tonicMidi)
        {
            return JianpuPitchCodec.ToMelodyMidiNote(note, tonicMidi);
        }

        private static int ParseTonicMidi(string keySignature)
        {
            if (!KeySignatureService.TryParseTonicPitchClass(keySignature, out var pitchClass))
            {
                return DefaultTonicMidi;
            }

            var octaveBase = DefaultTonicMidi - (DefaultTonicMidi % 12);
            return Math.Max(0, Math.Min(127, octaveBase + pitchClass));
        }

        private readonly struct NotePosition : IEquatable<NotePosition>
        {
            public NotePosition(int measureIndex, int noteIndex)
            {
                MeasureIndex = measureIndex;
                NoteIndex = noteIndex;
            }

            public int MeasureIndex { get; }

            public int NoteIndex { get; }

            public bool Equals(NotePosition other)
            {
                return MeasureIndex == other.MeasureIndex && NoteIndex == other.NoteIndex;
            }

            public override bool Equals(object obj)
            {
                return obj is NotePosition other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (MeasureIndex * 397) ^ NoteIndex;
                }
            }
        }
    }
}
