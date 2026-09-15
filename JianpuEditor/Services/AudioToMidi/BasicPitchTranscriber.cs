using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JianpuEditor.Services.AudioToMidi
{
    /// <summary>
    /// STEM audio -> notes using Spotify's basic-pitch ONNX model. Handles both
    /// monophonic and polyphonic (chords) instrument audio. See <see cref="GameTranscriber"/>
    /// for the vocal-specialist alternative.
    /// </summary>
    internal sealed class BasicPitchTranscriber
    {
        private const float OnsetThreshold = 0.5f;
        private const float FrameThreshold = 0.3f;
        private const int MinNoteLenFrames = 11; // basic-pitch's own default (~127.7ms)
        private const double MergeGapSeconds = 0.06; // bridges a spurious re-onset mid-sustain
        private const float MinAmplitude = 0.35f; // drops low-confidence/harmonic-bleed detections

        public List<TranscribedNote> Transcribe(string audioPath, IProgress<string> progress = null)
        {
            var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Models", "nmp.onnx");
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException("Audio transcription model not found. Expected at: " + modelPath, modelPath);
            }

            progress?.Report("Decoding audio...");
            var samples = AudioDecoder.DecodeToMono(audioPath, BasicPitchModel.AudioSampleRate);
            if (samples.Length == 0)
            {
                throw new InvalidOperationException("Decoded audio contained no samples: " + audioPath);
            }

            List<NoteDecoder.TimedNote> timedNotes;
            using (var model = new BasicPitchModel(modelPath))
            {
                var output = model.RunInference(samples, (window, totalWindows) =>
                    progress?.Report($"Transcribing (instrument) window {window} of {totalWindows}..."));
                var rawNotes = NoteDecoder.DecodeFrames(output.Note, output.Onset, OnsetThreshold, FrameThreshold, MinNoteLenFrames);
                timedNotes = NoteDecoder.ToTimedNotes(rawNotes, output.Note.GetLength(0));
            }

            return MergeAndFilter(timedNotes)
                .Select(n => new TranscribedNote(n.Start, n.End, n.Pitch, n.Amplitude))
                .ToList();
        }

        private static List<NoteDecoder.TimedNote> MergeAndFilter(List<NoteDecoder.TimedNote> notes)
        {
            var sorted = notes.OrderBy(n => n.Pitch).ThenBy(n => n.Start).ToList();
            var merged = new List<NoteDecoder.TimedNote>();
            foreach (var ev in sorted)
            {
                if (merged.Count > 0 && merged[merged.Count - 1].Pitch == ev.Pitch && ev.Start - merged[merged.Count - 1].End <= MergeGapSeconds)
                {
                    var prev = merged[merged.Count - 1];
                    merged[merged.Count - 1] = new NoteDecoder.TimedNote(prev.Start, Math.Max(prev.End, ev.End), ev.Pitch, Math.Max(prev.Amplitude, ev.Amplitude));
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
