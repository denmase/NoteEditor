using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Models;

namespace JianpuEditor.Services.AudioToMidi
{
    /// <summary>
    /// Audio-to-MIDI transcription for a single-instrument (STEM) recording: BASS decodes
    /// and resamples the input file, basic-pitch's ONNX model estimates pitches, and the
    /// resulting notes are written as a temporary MIDI file and handed to the existing
    /// <see cref="IMidiImportService"/> pipeline -- reusing its MIDI-to-score conversion
    /// rather than building a <see cref="JianpuScore"/> from scratch.
    /// </summary>
    /// <remarks>
    /// This is a prototype: precision was validated only against synthesized audio with
    /// known ground truth (see the audio-to-MIDI spike), not yet against real recordings.
    /// Known limitation: a note re-struck at the same pitch with no silence gap (legato
    /// repeats) can be missed -- a general hard case for this class of model, not specific
    /// to this integration.
    /// </remarks>
    internal sealed class AudioImportService : IAudioImportService
    {
        private const float OnsetThreshold = 0.5f;
        private const float FrameThreshold = 0.3f;
        private const int MinNoteLenFrames = 11; // basic-pitch's own default (~127.7ms)
        private const double MergeGapSeconds = 0.06; // bridges a spurious re-onset mid-sustain
        private const float MinAmplitude = 0.35f; // drops low-confidence/harmonic-bleed detections

        private readonly IMidiImportService _midiImportService;

        public AudioImportService(IMidiImportService midiImportService)
        {
            _midiImportService = midiImportService ?? throw new ArgumentNullException(nameof(midiImportService));
        }

        public JianpuScore Import(string audioPath)
        {
            if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath))
            {
                throw new FileNotFoundException("Audio file not found.", audioPath);
            }

            var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Models", "nmp.onnx");
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException("Audio transcription model not found. Expected at: " + modelPath, modelPath);
            }

            var samples = AudioDecoder.DecodeToMono(audioPath, BasicPitchModel.AudioSampleRate);
            if (samples.Length == 0)
            {
                throw new InvalidOperationException("Decoded audio contained no samples: " + audioPath);
            }

            List<NoteDecoder.TimedNote> timedNotes;
            using (var model = new BasicPitchModel(modelPath))
            {
                var output = model.RunInference(samples);
                var rawNotes = NoteDecoder.DecodeFrames(output.Note, output.Onset, OnsetThreshold, FrameThreshold, MinNoteLenFrames);
                timedNotes = NoteDecoder.ToTimedNotes(rawNotes, output.Note.GetLength(0));
            }

            var notes = MergeAndFilter(timedNotes);
            if (notes.Count == 0)
            {
                throw new InvalidOperationException("No notes were detected in this audio file.");
            }

            var tempMidiPath = Path.Combine(Path.GetTempPath(), "audio-import-" + Guid.NewGuid().ToString("N") + ".mid");
            try
            {
                SimpleMidiWriter.Write(tempMidiPath, notes.Select(n => (n.Start, n.End, n.Pitch, n.Amplitude)).ToList());
                return _midiImportService.Import(tempMidiPath, trackIndex: null);
            }
            finally
            {
                try
                {
                    File.Delete(tempMidiPath);
                }
                catch (IOException)
                {
                    // Best-effort cleanup; a leftover temp file isn't worth failing the import over.
                }
            }
        }

        private static List<NoteDecoder.TimedNote> MergeAndFilter(List<NoteDecoder.TimedNote> notes)
        {
            var sorted = notes.OrderBy(n => n.Pitch).ThenBy(n => n.Start).ToList();
            var merged = new List<NoteDecoder.TimedNote>();
            foreach (var ev in sorted)
            {
                if (merged.Count > 0 && merged[^1].Pitch == ev.Pitch && ev.Start - merged[^1].End <= MergeGapSeconds)
                {
                    var prev = merged[^1];
                    merged[^1] = new NoteDecoder.TimedNote(prev.Start, Math.Max(prev.End, ev.End), ev.Pitch, Math.Max(prev.Amplitude, ev.Amplitude));
                }
                else
                {
                    merged.Add(ev);
                }
            }
            return merged.Where(n => n.Amplitude >= MinAmplitude).OrderBy(n => n.Start).ToList();
        }
    }
}
