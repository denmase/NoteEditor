using System;
using System.Collections.Generic;
using System.IO;

namespace JianpuEditor.Services.AudioToMidi
{
    /// <summary>
    /// STEM audio -> notes using openvpi's GAME ONNX model. Vocal-only (monophonic
    /// singing voice), but produces a clean, non-overlapping note sequence where
    /// basic-pitch (a general instrument model) tends to double harmonics into
    /// spurious simultaneous "notes" on real vocal recordings.
    /// </summary>
    /// <remarks>
    /// GAME's encoder has no internal windowing -- it runs self-attention over the
    /// whole clip at once, and memory/time scale roughly quadratically with duration
    /// (measured empirically: ~1.3GB at 30s, ~2.9GB at 45s; a full 3-minute clip fed
    /// whole was OOM-killed). <see cref="ChunkSeconds"/> keeps each inference call
    /// well under 1.5GB. A note that happens to fall exactly on a chunk boundary can
    /// be split into two -- a known limitation of this simple fixed-size chunking,
    /// not fixed here.
    /// </remarks>
    internal sealed class GameTranscriber
    {
        private const double ChunkSeconds = 24.0;
        private const double PadSeconds = 1.0; // avoids a short-clip edge artifact found during prototyping
        private const float SegThreshold = 0.2f;
        private const long SegRadiusFrames = 2;
        private const float EstThreshold = 0.2f;
        private const float DefaultAmplitude = 0.8f; // GAME has no per-note confidence/velocity signal

        public List<TranscribedNote> Transcribe(string audioPath, IProgress<string> progress = null)
        {
            var modelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Models", "game");
            if (!Directory.Exists(modelDir))
            {
                throw new DirectoryNotFoundException("GAME transcription model not found. Expected at: " + modelDir);
            }

            progress?.Report("Loading vocal model...");
            using (var model = new GameOnnxModel(modelDir))
            {
                progress?.Report("Decoding audio...");
                var samples = AudioDecoder.DecodeToMono(audioPath, model.SampleRate);
                if (samples.Length == 0)
                {
                    throw new InvalidOperationException("Decoded audio contained no samples: " + audioPath);
                }

                var ts = GameOnnxModel.DefaultTs();
                var padSamples = (int)(PadSeconds * model.SampleRate);
                var chunkSamples = (int)(ChunkSeconds * model.SampleRate);
                var totalChunks = Math.Max(1, (int)Math.Ceiling((double)samples.Length / chunkSamples));

                var notes = new List<TranscribedNote>();
                var chunkIndex = 0;
                for (var chunkStart = 0; chunkStart < samples.Length; chunkStart += chunkSamples)
                {
                    chunkIndex++;
                    progress?.Report($"Transcribing (vocal) chunk {chunkIndex} of {totalChunks}...");

                    var chunkLength = Math.Min(chunkSamples, samples.Length - chunkStart);
                    var padded = new float[padSamples * 2 + chunkLength];
                    Array.Copy(samples, chunkStart, padded, padSamples, chunkLength);

                    var chunkOffsetSeconds = (double)chunkStart / model.SampleRate - PadSeconds;
                    var gameNotes = model.Infer(
                        padded, (float)padded.Length / model.SampleRate,
                        segThreshold: SegThreshold, segRadiusFrames: SegRadiusFrames, estThreshold: EstThreshold, ts: ts);

                    foreach (var n in gameNotes)
                    {
                        var start = n.Start + chunkOffsetSeconds;
                        var end = n.End + chunkOffsetSeconds;
                        if (end <= 0)
                        {
                            continue; // fell entirely in the leading pad
                        }
                        notes.Add(new TranscribedNote(Math.Max(0, start), end, (int)Math.Round(n.Pitch), DefaultAmplitude));
                    }
                }
                return notes;
            }
        }
    }
}
