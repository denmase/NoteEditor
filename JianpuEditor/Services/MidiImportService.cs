using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JianpuEditor.Models;
using JianpuEditor.Rendering;

namespace JianpuEditor.Services
{
    public static class MidiImportService
    {
        private const double QuantizeGrid = 0.25;
        private const double DurationEpsilon = 0.02;
        private const int DrumChannel = 9;

        private static readonly int[] MajorScaleOffsets = { 0, 2, 4, 5, 7, 9, 11 };
        private static readonly string[] TonicNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        public static void ImportToJianpuFile(string midiPath, string jianpuPath)
        {
            var score = Import(midiPath);
            ScoreFileService.Save(score, jianpuPath);
        }

        /// <summary>
        /// Lists every track in the MIDI file with enough information (name, note count, how
        /// polyphonic it is) for a user to pick the actual melody track themselves, plus which one
        /// <see cref="Import"/> would pick automatically. Real-world multi-track files often don't
        /// have a track that's unambiguously "the melody" -- a busy chordal accompaniment can easily
        /// have more raw notes than the actual tune -- so this is meant to be shown to the user
        /// before import, not just relied on silently.
        /// </summary>
        public static IReadOnlyList<MidiTrackInfo> GetTrackInfos(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path is required.", nameof(path));
            }

            var file = MidiFileReader.Read(path);
            var recommended = file.Tracks.Count > 0 ? SelectMelodyTrackIndex(file) : -1;
            var infos = new List<MidiTrackInfo>();
            for (var i = 0; i < file.Tracks.Count; i++)
            {
                var track = file.Tracks[i];
                var nonDrumNotes = track.NoteOnEvents.Where(item => item.Channel != DrumChannel).ToList();
                if (nonDrumNotes.Count == 0)
                {
                    continue;
                }

                infos.Add(new MidiTrackInfo
                {
                    Index = i,
                    Name = track.Name,
                    NoteCount = nonDrumNotes.Count,
                    MaxSimultaneousNotes = GetMaxSimultaneousNotes(track),
                    IsRecommended = i == recommended
                });
            }

            return infos;
        }

        public static JianpuScore Import(string path, int? trackIndex = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path is required.", nameof(path));
            }

            var file = MidiFileReader.Read(path);
            ParsedTrack track;
            if (trackIndex.HasValue)
            {
                if (trackIndex.Value < 0 || trackIndex.Value >= file.Tracks.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(trackIndex), "Track index is out of range for this MIDI file.");
                }

                track = file.Tracks[trackIndex.Value];
            }
            else
            {
                track = SelectMelodyTrack(file);
            }

            var notes = ExtractNotes(track, file.TicksPerQuarter);
            if (notes.Count == 0)
            {
                throw new InvalidOperationException("The MIDI file does not contain any melody notes that can be imported.");
            }

            var bpm = track.TempoChanges.Count > 0
                ? track.TempoChanges[0].Bpm
                : 120;
            var timeSignature = track.TimeSignatureChanges.Count > 0
                ? track.TimeSignatureChanges[0].Numerator + "/" + track.TimeSignatureChanges[0].Denominator
                : "4/4";
            var tonicMidi = DetectTonicMidi(notes);
            var keySignature = KeySignatureService.FormatKeySignature(tonicMidi % 12);
            var measures = BuildMeasures(notes, tonicMidi, ScoreMidiSchedule.DefaultMeasureBeats);
            measures = MeasureNormalizationService.NormalizeMeasures(measures, ScoreMidiSchedule.DefaultMeasureBeats);

            var hasPlayableNote = measures.Any(measure =>
                measure.MelodyNotes != null && measure.MelodyNotes.Any(note => note.Type != NoteType.Rest));
            if (!hasPlayableNote)
            {
                throw new InvalidOperationException(
                    "This track's notes are too far outside the melodic range to import (e.g. a bass line pitched several octaves below the detected key). Try a different track.");
            }

            return new JianpuScore
            {
                Title = Path.GetFileNameWithoutExtension(path) ?? "MIDI Import",
                KeySignature = keySignature,
                TimeSignature = timeSignature,
                Tempo = TempoMarkingService.FromBpm(bpm),
                Bpm = bpm,
                Composer = string.Empty,
                Measures = measures,
                Ties = new List<JianpuTie>()
            };
        }

        private static readonly string[] MelodyNameHints = { "melody", "vocal", "lead", "voice", "solo", "tune" };

        private static ParsedTrack SelectMelodyTrack(MidiFileData file)
        {
            var index = SelectMelodyTrackIndex(file);
            return index >= 0 ? file.Tracks[index] : file.Tracks[0];
        }

        /// <summary>
        /// Picks the track most likely to be the melody. Raw note count alone is a poor signal --
        /// a busy chordal accompaniment routinely has more note-on events than the actual tune (seen
        /// in real-world files: a "Strings" backing track with 305 notes across up to 6 simultaneous
        /// notes, versus the real 82-note, genuinely monophonic vocal line it accompanies). Score by
        /// notes-per-simultaneous-voice instead, so a busy monophonic line beats a sparser chordal
        /// one, and let an unambiguous name (e.g. "Melody", "Vocal") override the numeric score
        /// entirely when present.
        /// </summary>
        private static int SelectMelodyTrackIndex(MidiFileData file)
        {
            if (file.Tracks.Count == 1)
            {
                return file.Tracks[0].NoteOnEvents.Any(item => item.Channel != DrumChannel) ? 0 : -1;
            }

            var bestIndex = -1;
            var bestHasNameHint = false;
            var bestScore = double.MinValue;
            for (var i = 0; i < file.Tracks.Count; i++)
            {
                var track = file.Tracks[i];
                var count = track.NoteOnEvents.Count(item => item.Channel != DrumChannel);
                if (count == 0)
                {
                    continue;
                }

                var hasNameHint = MelodyNameHints.Any(hint =>
                    track.Name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0);
                var maxSimultaneous = Math.Max(1, GetMaxSimultaneousNotes(track));
                var score = (double)count / maxSimultaneous;

                // A name hint always wins over one without, regardless of score; among tracks that
                // agree on name-hint status, the higher density score wins.
                if (bestIndex < 0
                    || (hasNameHint && !bestHasNameHint)
                    || (hasNameHint == bestHasNameHint && score > bestScore))
                {
                    bestIndex = i;
                    bestHasNameHint = hasNameHint;
                    bestScore = score;
                }
            }

            return bestIndex;
        }

        private static int GetMaxSimultaneousNotes(ParsedTrack track)
        {
            var active = 0;
            var max = 0;
            foreach (var evt in track.Events.Where(item => item.Channel != DrumChannel).OrderBy(item => item.Ticks))
            {
                if (evt.Type == MidiTrackEventType.NoteOn && evt.Velocity > 0)
                {
                    active++;
                    max = Math.Max(max, active);
                }
                else if (evt.Type == MidiTrackEventType.NoteOff
                    || (evt.Type == MidiTrackEventType.NoteOn && evt.Velocity == 0))
                {
                    active = Math.Max(0, active - 1);
                }
            }

            return max;
        }

        private static List<ImportedNote> ExtractNotes(ParsedTrack track, int ticksPerQuarter)
        {
            var active = new Dictionary<NoteKey, NoteOnEvent>();
            var notes = new List<ImportedNote>();

            foreach (var evt in track.Events.OrderBy(item => item.Ticks))
            {
                if (evt.Type == MidiTrackEventType.NoteOn && evt.Velocity > 0 && evt.Channel != DrumChannel)
                {
                    active[new NoteKey(evt.Channel, evt.NoteNumber)] = new NoteOnEvent
                    {
                        Ticks = evt.Ticks,
                        Channel = evt.Channel,
                        NoteNumber = evt.NoteNumber,
                        Velocity = evt.Velocity
                    };
                    continue;
                }

                if (evt.Type == MidiTrackEventType.NoteOff
                    || (evt.Type == MidiTrackEventType.NoteOn && evt.Velocity == 0))
                {
                    var key = new NoteKey(evt.Channel, evt.NoteNumber);
                    if (!active.TryGetValue(key, out var start))
                    {
                        continue;
                    }

                    active.Remove(key);
                    var durationTicks = Math.Max(1, evt.Ticks - start.Ticks);
                    notes.Add(new ImportedNote
                    {
                        Channel = start.Channel,
                        MidiNote = start.NoteNumber,
                        StartQuarter = QuantizeOnset(start.Ticks / (double)ticksPerQuarter),
                        DurationQuarter = QuantizeDuration(durationTicks / (double)ticksPerQuarter)
                    });
                }
            }

            return notes
                .OrderBy(item => item.StartQuarter)
                .ThenBy(item => item.MidiNote)
                .ToList();
        }

        private static double QuantizeDuration(double quarterLength)
        {
            if (quarterLength <= DurationEpsilon)
            {
                return QuantizeGrid;
            }

            var quantized = Math.Round(quarterLength / QuantizeGrid) * QuantizeGrid;
            return Math.Max(QuantizeGrid, quantized);
        }

        // Snaps a note's onset to the nearest sixteenth-note position. A source file's raw
        // tick timing (whether a human performance or audio-transcribed onset noise) has no
        // reason to land exactly on the grid jianpu notation requires; left uncorrected, that
        // jitter accumulates into the running cursor in BuildMeasures and shows up as spurious
        // near-zero-length rests once the gap exceeds DurationEpsilon. Durations are already
        // force-snapped the same way (see ApplyDurationUnits), so this brings onsets in line
        // with the same rigid grid the rest of the pipeline already assumes.
        private static double QuantizeOnset(double quarterPosition)
        {
            return Math.Round(quarterPosition / QuantizeGrid) * QuantizeGrid;
        }

        // Krumhansl-Kessler key profiles (Krumhansl & Kessler, 1982): the relative perceived
        // "fit" of each scale degree (index = semitones above the tonic) in a major/minor
        // context. A tonic's relative major (e.g. C for A minor) shares the exact same set of
        // pitch classes, so counting scale membership alone -- the previous approach here --
        // can never tell them apart and will always report the relative major, tie-broken only
        // by which tonic happens to be tried first. Correlating the piece's actual pitch-class
        // usage against these profiles (the standard technique for this problem) distinguishes
        // them by how the piece actually emphasizes its scale degrees, not just which notes it
        // uses.
        private static readonly double[] MajorKeyProfile =
            { 6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88 };

        private static readonly double[] MinorKeyProfile =
            { 6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17 };

        private static int DetectTonicMidi(IReadOnlyList<ImportedNote> notes)
        {
            var histogram = new double[12];
            foreach (var note in notes)
            {
                var pitchClass = ((note.MidiNote % 12) + 12) % 12;
                histogram[pitchClass] += Math.Max(0.01, note.DurationQuarter);
            }

            if (histogram.Sum() <= 0)
            {
                return ScoreMidiSchedule.DefaultTonicMidi;
            }

            var bestTonic = ScoreMidiSchedule.DefaultTonicMidi;
            var bestCorrelation = double.NegativeInfinity;
            for (var tonic = 0; tonic < 12; tonic++)
            {
                var majorCorrelation = CorrelateWithProfile(histogram, MajorKeyProfile, tonic);
                var minorCorrelation = CorrelateWithProfile(histogram, MinorKeyProfile, tonic);
                var best = Math.Max(majorCorrelation, minorCorrelation);
                if (best > bestCorrelation)
                {
                    bestCorrelation = best;
                    bestTonic = 60 + tonic;
                }
            }

            return bestTonic;
        }

        private static double CorrelateWithProfile(double[] histogram, double[] profile, int tonic)
        {
            var rotated = new double[12];
            for (var pitchClass = 0; pitchClass < 12; pitchClass++)
            {
                rotated[pitchClass] = profile[((pitchClass - tonic) % 12 + 12) % 12];
            }

            var meanHistogram = histogram.Average();
            var meanProfile = rotated.Average();
            var numerator = 0.0;
            var histogramVariance = 0.0;
            var profileVariance = 0.0;
            for (var i = 0; i < 12; i++)
            {
                var dh = histogram[i] - meanHistogram;
                var dp = rotated[i] - meanProfile;
                numerator += dh * dp;
                histogramVariance += dh * dh;
                profileVariance += dp * dp;
            }

            var denominator = Math.Sqrt(histogramVariance * profileVariance);
            return denominator > 0 ? numerator / denominator : 0.0;
        }

        private static List<JianpuMeasure> BuildMeasures(
            IReadOnlyList<ImportedNote> notes,
            int tonicMidi,
            int measureBeats)
        {
            var measures = new List<JianpuMeasure>();
            var current = CreateMeasure();
            var measureStart = 0.0;
            var cursor = 0.0;

            foreach (var group in GroupNotesByStart(notes))
            {
                if (group.Count == 0)
                {
                    continue;
                }

                var groupStart = group[0].StartQuarter;
                if (groupStart > cursor + DurationEpsilon)
                {
                    AppendRests(current, groupStart - cursor, ref measureStart, measureBeats, measures, ref current, ref cursor);
                }

                var jianpuNotes = new List<JianpuNote>();
                var durationUnits = 0.0;
                foreach (var note in group)
                {
                    if (!TryMidiToJianpu(
                            note.MidiNote,
                            tonicMidi,
                            out var pitch,
                            out var octave,
                            out var accidental,
                            out _))
                    {
                        continue;
                    }

                    var jianpuNote = CreateNote(pitch, accidental, octave, note.DurationQuarter);
                    if (jianpuNote == null)
                    {
                        continue;
                    }

                    jianpuNotes.Add(jianpuNote);
                    durationUnits = Math.Max(durationUnits, JianpuRenderer.GetDurationUnits(jianpuNote));
                }

                if (jianpuNotes.Count == 0)
                {
                    continue;
                }

                foreach (var jianpuNote in jianpuNotes)
                {
                    ApplyDurationUnits(jianpuNote, durationUnits);
                }

                var measureUsed = cursor - measureStart;
                if (measureUsed + durationUnits > measureBeats + DurationEpsilon)
                {
                    AppendRests(current, measureBeats - measureUsed, ref measureStart, measureBeats, measures, ref current, ref cursor);
                }

                var beatPosition = cursor - measureStart;
                var chord = MelodyChordService.CreateChord(beatPosition, jianpuNotes);
                MelodyChordService.AppendChord(current, chord);
                cursor += durationUnits;
            }

            if (current.MelodyNotes.Count > 0)
            {
                measures.Add(current);
            }

            if (measures.Count == 0)
            {
                measures.Add(CreateMeasure());
            }

            return measures;
        }

        private static List<List<ImportedNote>> GroupNotesByStart(IReadOnlyList<ImportedNote> notes)
        {
            var groups = new List<List<ImportedNote>>();
            if (notes == null || notes.Count == 0)
            {
                return groups;
            }

            List<ImportedNote> current = null;
            double? currentStart = null;
            foreach (var note in notes)
            {
                if (current == null || Math.Abs(note.StartQuarter - currentStart.Value) > DurationEpsilon)
                {
                    current = new List<ImportedNote> { note };
                    groups.Add(current);
                    currentStart = note.StartQuarter;
                }
                else
                {
                    current.Add(note);
                }
            }

            return groups;
        }

        private static void AppendRests(
            JianpuMeasure measure,
            double gap,
            ref double measureStart,
            int measureBeats,
            List<JianpuMeasure> measures,
            ref JianpuMeasure current,
            ref double cursor)
        {
            while (gap > DurationEpsilon)
            {
                var measureUsed = cursor - measureStart;
                var room = measureBeats - measureUsed;
                if (room <= DurationEpsilon)
                {
                    measures.Add(current);
                    current = CreateMeasure();
                    measureStart = cursor;
                    room = measureBeats;
                }

                var restDuration = Math.Min(gap, room);
                var rest = CreateRest(restDuration);
                if (rest == null)
                {
                    break;
                }

                MelodyChordService.AppendChord(
                    current,
                    MelodyChordService.CreateChord(
                        cursor - measureStart,
                        new[] { rest }));
                var units = JianpuRenderer.GetDurationUnits(rest);
                cursor += units;
                gap -= units;
            }
        }

        private static JianpuMeasure CreateMeasure()
        {
            return new JianpuMeasure
            {
                MelodyNotes = new List<JianpuNote>(),
                Chords = new List<JianpuChord>(),
                LyricText = " "
            };
        }

        private static JianpuNote CreateNote(
            double pitch,
            AccidentalKind accidental,
            int octave,
            double durationUnits)
        {
            var note = new JianpuNote
            {
                Type = NoteType.Note,
                Pitch = pitch,
                Accidental = accidental,
                Octave = octave
            };
            return ApplyDurationUnits(note, durationUnits) ? note : null;
        }

        private static JianpuNote CreateRest(double durationUnits)
        {
            var note = new JianpuNote { Type = NoteType.Rest, Pitch = 0, Octave = 0 };
            return ApplyDurationUnits(note, durationUnits) ? note : null;
        }

        internal static bool TryMidiToJianpu(
            int midiNote,
            int tonicMidi,
            out double pitch,
            out int octave,
            out AccidentalKind accidental,
            out int semitoneError)
        {
            pitch = 1;
            octave = 0;
            accidental = AccidentalKind.None;
            semitoneError = 127;
            var bestError = 127;
            var bestDegree = 1;
            var bestCandidate = tonicMidi;
            int? sharpDegree = null;
            var sharpOctave = 0;
            int? flatDegree = null;
            var flatOctave = 0;

            for (var octaveDot = -1; octaveDot <= 1; octaveDot++)
            {
                for (var degree = 1; degree <= 7; degree++)
                {
                    var candidate = tonicMidi + MajorScaleOffsets[degree - 1] + octaveDot * 12;
                    var error = Math.Abs(candidate - midiNote);
                    if (error < bestError)
                    {
                        bestError = error;
                        bestDegree = degree;
                        bestCandidate = candidate;
                        octave = octaveDot;
                        semitoneError = error;
                        sharpDegree = null;
                        flatDegree = null;
                    }

                    if (error != 1)
                    {
                        continue;
                    }

                    if (midiNote > candidate)
                    {
                        sharpDegree = degree;
                        sharpOctave = octaveDot;
                    }
                    else if (midiNote < candidate)
                    {
                        flatDegree = degree;
                        flatOctave = octaveDot;
                    }
                }
            }

            if (bestError == 0)
            {
                pitch = bestDegree;
                accidental = AccidentalKind.None;
                return true;
            }

            if (bestError == 1)
            {
                if (sharpDegree.HasValue && flatDegree.HasValue)
                {
                    var lowerDegree = Math.Min(sharpDegree.Value, flatDegree.Value - 1);
                    if (PreferFlatAccidental(lowerDegree))
                    {
                        accidental = AccidentalKind.Flat;
                        pitch = (flatDegree.Value - 1) + JianpuPitchCodec.AccidentalFraction;
                        octave = flatOctave;
                    }
                    else
                    {
                        accidental = AccidentalKind.Sharp;
                        pitch = sharpDegree.Value + JianpuPitchCodec.AccidentalFraction;
                        octave = sharpOctave;
                    }
                }
                else if (sharpDegree.HasValue)
                {
                    accidental = AccidentalKind.Sharp;
                    pitch = sharpDegree.Value + JianpuPitchCodec.AccidentalFraction;
                    octave = sharpOctave;
                }
                else if (flatDegree.HasValue)
                {
                    accidental = AccidentalKind.Flat;
                    pitch = (flatDegree.Value - 1) + JianpuPitchCodec.AccidentalFraction;
                    octave = flatOctave;
                }
                else if (midiNote > bestCandidate)
                {
                    accidental = AccidentalKind.Sharp;
                    pitch = bestDegree + JianpuPitchCodec.AccidentalFraction;
                }
                else
                {
                    accidental = AccidentalKind.Flat;
                    pitch = (bestDegree - 1) + JianpuPitchCodec.AccidentalFraction;
                }

                return true;
            }

            return false;
        }

        private static bool PreferFlatAccidental(int lowerDegree)
        {
            return lowerDegree == 2 || lowerDegree == 4 || lowerDegree == 6;
        }

        // The set of durations (in quarter-note units) jianpu notation can actually
        // represent: sixteenth through whole note via underlines/dashes/dots.
        private static readonly double[] ValidDurationUnits = { 0.25, 0.5, 0.75, 1.0, 1.5, 2.0, 3.0, 4.0 };

        internal static bool ApplyDurationUnits(JianpuNote note, double units)
        {
            if (note == null || units <= 0)
            {
                return false;
            }

            note.Underlines = 0;
            note.Dashes = 0;
            note.Dotted = false;

            // Snap to the closest notatable duration instead of only matching near-exact
            // values -- a duration that doesn't land close to any of them (routine for
            // audio-transcribed notes, whose real-world timing has no relationship to any
            // notated grid) used to silently fall through to a fixed eighth note regardless
            // of how far off that was, which is what caused most of a transcribed melody to
            // come out as rests: the truncated duration left an unaccounted gap that the
            // next step filled in with a rest.
            var snapped = units;
            if (!ValidDurationUnits.Any(c => Math.Abs(units - c) < DurationEpsilon))
            {
                snapped = ValidDurationUnits.OrderBy(c => Math.Abs(c - units)).First();
            }

            if (Math.Abs(snapped - 0.25) < DurationEpsilon)
            {
                note.Underlines = 2;
                return true;
            }

            if (Math.Abs(snapped - 0.5) < DurationEpsilon)
            {
                note.Underlines = 1;
                return true;
            }

            if (Math.Abs(snapped - 0.75) < DurationEpsilon)
            {
                note.Underlines = 1;
                note.Dotted = true;
                return true;
            }

            if (Math.Abs(snapped - 1.0) < DurationEpsilon)
            {
                return true;
            }

            if (Math.Abs(snapped - 1.5) < DurationEpsilon)
            {
                note.Dotted = true;
                return true;
            }

            note.Dashes = (int)Math.Round(snapped, MidpointRounding.AwayFromZero) - 1;
            return true;
        }

        private sealed class ImportedNote
        {
            public int Channel { get; set; }

            public int MidiNote { get; set; }

            public double StartQuarter { get; set; }

            public double DurationQuarter { get; set; }
        }

        private readonly struct NoteKey : IEquatable<NoteKey>
        {
            public NoteKey(int channel, int noteNumber)
            {
                Channel = channel;
                NoteNumber = noteNumber;
            }

            public int Channel { get; }

            public int NoteNumber { get; }

            public bool Equals(NoteKey other)
            {
                return Channel == other.Channel && NoteNumber == other.NoteNumber;
            }

            public override bool Equals(object obj)
            {
                return obj is NoteKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Channel * 397) ^ NoteNumber;
                }
            }
        }

        private sealed class NoteOnEvent
        {
            public long Ticks { get; set; }

            public int Channel { get; set; }

            public int NoteNumber { get; set; }

            public int Velocity { get; set; }
        }

        private enum MidiTrackEventType
        {
            NoteOn,
            NoteOff,
            Meta
        }

        private sealed class MidiTrackEvent
        {
            public long Ticks { get; set; }

            public MidiTrackEventType Type { get; set; }

            public int Channel { get; set; }

            public int NoteNumber { get; set; }

            public int Velocity { get; set; }
        }

        private sealed class TempoChange
        {
            public long Ticks { get; set; }

            public int Bpm { get; set; }
        }

        private sealed class TimeSignatureChange
        {
            public long Ticks { get; set; }

            public int Numerator { get; set; }

            public int Denominator { get; set; }
        }

        private sealed class ParsedTrack
        {
            public string Name { get; set; } = string.Empty;

            public List<MidiTrackEvent> Events { get; } = new List<MidiTrackEvent>();

            public List<NoteOnEvent> NoteOnEvents { get; } = new List<NoteOnEvent>();

            public List<TempoChange> TempoChanges { get; } = new List<TempoChange>();

            public List<TimeSignatureChange> TimeSignatureChanges { get; } = new List<TimeSignatureChange>();
        }

        private sealed class MidiFileData
        {
            public int Format { get; set; }

            public int TicksPerQuarter { get; set; }

            public List<ParsedTrack> Tracks { get; } = new List<ParsedTrack>();
        }

        private static class MidiFileReader
        {
            public static MidiFileData Read(string path)
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
                {
                    var header = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    if (header != "MThd")
                    {
                        throw new InvalidOperationException("Not a valid MIDI file (missing MThd).");
                    }

                    var headerLength = ReadInt32Be(reader);
                    if (headerLength < 6)
                    {
                        throw new InvalidOperationException("The MIDI file header is corrupted.");
                    }

                    var format = ReadInt16Be(reader);
                    var trackCount = ReadInt16Be(reader);
                    var division = ReadInt16Be(reader);
                    if (headerLength > 6)
                    {
                        reader.ReadBytes(headerLength - 6);
                    }

                    if ((division & 0x8000) != 0)
                    {
                        throw new NotSupportedException("Spike does not yet support MIDI files using SMPTE timecode format.");
                    }

                    var data = new MidiFileData
                    {
                        Format = format,
                        TicksPerQuarter = division
                    };

                    for (var i = 0; i < trackCount; i++)
                    {
                        data.Tracks.Add(ReadTrack(reader));
                    }

                    return data;
                }
            }

            private static ParsedTrack ReadTrack(BinaryReader reader)
            {
                var marker = Encoding.ASCII.GetString(reader.ReadBytes(4));
                if (marker != "MTrk")
                {
                    throw new InvalidOperationException("The MIDI track chunk is corrupted (missing MTrk).");
                }

                var trackLength = ReadInt32Be(reader);
                var end = reader.BaseStream.Position + trackLength;
                var track = new ParsedTrack();
                long absoluteTicks = 0;
                byte? runningStatus = null;

                while (reader.BaseStream.Position < end)
                {
                    var delta = ReadVarLength(reader);
                    absoluteTicks += delta;
                    if (reader.BaseStream.Position >= end)
                    {
                        break;
                    }

                    var status = reader.ReadByte();
                    if (status < 0x80)
                    {
                        if (!runningStatus.HasValue)
                        {
                            throw new InvalidOperationException("The MIDI event stream is corrupted (missing status byte).");
                        }

                        reader.BaseStream.Position--;
                        status = runningStatus.Value;
                    }
                    else
                    {
                        if (status < 0xF0)
                        {
                            runningStatus = status;
                        }
                    }

                    if (status == 0xFF)
                    {
                        var metaType = reader.ReadByte();
                        var length = ReadVarLength(reader);
                        if (metaType == 0x51 && length == 3)
                        {
                            var b1 = reader.ReadByte();
                            var b2 = reader.ReadByte();
                            var b3 = reader.ReadByte();
                            var usPerQuarter = (b1 << 16) | (b2 << 8) | b3;
                            if (usPerQuarter > 0)
                            {
                                track.TempoChanges.Add(new TempoChange
                                {
                                    Ticks = absoluteTicks,
                                    Bpm = Math.Max(30, Math.Min(300, 60_000_000 / usPerQuarter))
                                });
                            }
                        }
                        else if (metaType == 0x58 && length == 4)
                        {
                            var numerator = reader.ReadByte();
                            var denominatorPower = reader.ReadByte();
                            reader.ReadBytes(2); // clocks-per-click, 32nds-per-quarter -- not needed here
                            if (numerator > 0 && denominatorPower <= 8)
                            {
                                track.TimeSignatureChanges.Add(new TimeSignatureChange
                                {
                                    Ticks = absoluteTicks,
                                    Numerator = numerator,
                                    Denominator = 1 << denominatorPower
                                });
                            }
                        }
                        else if ((metaType == 0x03 || metaType == 0x04) && string.IsNullOrEmpty(track.Name))
                        {
                            // 0x03 = track name, 0x04 = instrument name -- either is a useful label
                            // for the track picker; keep whichever comes first.
                            track.Name = Encoding.ASCII.GetString(reader.ReadBytes(length)).Trim();
                        }
                        else
                        {
                            reader.ReadBytes(length);
                        }

                        continue;
                    }

                    if (status == 0xF0 || status == 0xF7)
                    {
                        var length = ReadVarLength(reader);
                        reader.ReadBytes(length);
                        continue;
                    }

                    var eventType = status & 0xF0;
                    var channel = status & 0x0F;
                    if (eventType == 0x90)
                    {
                        var note = reader.ReadByte();
                        var velocity = reader.ReadByte();
                        var evt = new MidiTrackEvent
                        {
                            Ticks = absoluteTicks,
                            Type = MidiTrackEventType.NoteOn,
                            Channel = channel,
                            NoteNumber = note,
                            Velocity = velocity
                        };
                        track.Events.Add(evt);
                        if (velocity > 0)
                        {
                            track.NoteOnEvents.Add(new NoteOnEvent
                            {
                                Ticks = absoluteTicks,
                                Channel = channel,
                                NoteNumber = note,
                                Velocity = velocity
                            });
                        }
                    }
                    else if (eventType == 0x80)
                    {
                        var note = reader.ReadByte();
                        reader.ReadByte();
                        track.Events.Add(new MidiTrackEvent
                        {
                            Ticks = absoluteTicks,
                            Type = MidiTrackEventType.NoteOff,
                            Channel = channel,
                            NoteNumber = note,
                            Velocity = 0
                        });
                    }
                    else if (eventType == 0xA0 || eventType == 0xB0 || eventType == 0xE0)
                    {
                        reader.ReadBytes(2);
                    }
                    else if (eventType == 0xC0 || eventType == 0xD0)
                    {
                        reader.ReadByte();
                    }
                }

                return track;
            }

            private static int ReadInt16Be(BinaryReader reader)
            {
                var b1 = reader.ReadByte();
                var b2 = reader.ReadByte();
                return (b1 << 8) | b2;
            }

            private static int ReadInt32Be(BinaryReader reader)
            {
                var b1 = reader.ReadByte();
                var b2 = reader.ReadByte();
                var b3 = reader.ReadByte();
                var b4 = reader.ReadByte();
                return (b1 << 24) | (b2 << 16) | (b3 << 8) | b4;
            }

            private static int ReadVarLength(BinaryReader reader)
            {
                var value = 0;
                while (true)
                {
                    var b = reader.ReadByte();
                    value = (value << 7) | (b & 0x7F);
                    if ((b & 0x80) == 0)
                    {
                        return value;
                    }
                }
            }
        }
    }
}
