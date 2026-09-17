using System;
using System.Collections.Generic;

namespace JianpuEditor.Services.AudioToMidi
{
    /// <summary>
    /// Snaps transcribed notes' onsets to a fixed beat-fraction grid using the already-estimated
    /// tempo, before <see cref="AudioImportService"/> writes them out as a temporary MIDI file.
    /// </summary>
    /// <remarks>
    /// A transcribed onset's raw timing has no reason to land on any notatable grid. Left
    /// uncorrected, that jitter can exceed the small gap tolerance
    /// <c>MidiImportService.BuildMeasures</c> allows between the end of one note and the start of
    /// the next, and shows up as a spurious rest wedged between two notes that should be
    /// adjacent. <see cref="MidiImportService"/> also re-snaps onsets to the nearest sixteenth
    /// when it reads the MIDI file back in (a safety net that covers hand-authored MIDI import
    /// too), so quantizing here mainly matters for a grid coarser than a sixteenth, or for
    /// getting a cleaner signal into <see cref="TimeSignatureEstimator"/>, which runs on these
    /// same onsets before the tempo/quantization round-trip through MIDI.
    /// </remarks>
    internal static class OnsetQuantizer
    {
        /// <param name="notes">Notes to quantize, in seconds.</param>
        /// <param name="bpm">The already-estimated tempo, used to convert the beat-fraction grid
        /// into seconds.</param>
        /// <param name="gridQuarterNotes">Grid size in quarter-note units (e.g. 0.25 for the
        /// nearest sixteenth). 0 or negative disables quantization and returns
        /// <paramref name="notes"/> unchanged.</param>
        public static List<TranscribedNote> Quantize(IReadOnlyList<TranscribedNote> notes, double bpm, double gridQuarterNotes)
        {
            var result = new List<TranscribedNote>(notes.Count);
            if (gridQuarterNotes <= 0 || bpm <= 0)
            {
                result.AddRange(notes);
                return result;
            }

            var gridSeconds = gridQuarterNotes * (60.0 / bpm);
            foreach (var note in notes)
            {
                var quantizedStart = Math.Round(note.Start / gridSeconds) * gridSeconds;
                var duration = note.End - note.Start;
                result.Add(new TranscribedNote(quantizedStart, quantizedStart + duration, note.Pitch, note.Amplitude));
            }

            return result;
        }
    }
}
