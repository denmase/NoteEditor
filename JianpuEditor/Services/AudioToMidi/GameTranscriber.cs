using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
    /// well under 1.5GB.
    ///
    /// Each chunk is padded with <see cref="OverlapSeconds"/> of the *real* neighboring
    /// audio (not silence) on both sides, so the model has genuine acoustic context
    /// right up to the seam -- a held note that continues past a chunk boundary is
    /// still fully audible to the chunk that starts it. To avoid then reporting that
    /// same note twice (once truncated from each side), only notes whose onset falls
    /// within a chunk's own "core" span [chunkStart, chunkStart+chunkLength) are kept
    /// from that chunk's output; a note that starts inside chunk N's core but sustains
    /// into the overlap fringe is still recorded in full by chunk N (its real end time,
    /// since the audio there is real, not padding), while chunk N+1's own detection of
    /// that same tail (from its leading overlap) is dropped as the duplicate it is.
    /// An earlier version zero-padded between chunks, which reliably split any note
    /// straddling a boundary into two truncated pieces (reproduced and fixed after a
    /// user report that boundary notes came out shorter than they should).
    ///
    /// After all chunks are assembled, <see cref="MergeAdjacentSamePitch"/> always runs
    /// (GAME sometimes reports one sustained pitch as several back-to-back identical-pitch
    /// fragments), and <see cref="SmoothVibrato"/> optionally absorbs a short note that
    /// dips/rises and immediately returns to its surrounding pitch -- vibrato mistaken for
    /// a distinct note -- controlled by <see cref="GameSettings.VibratoSmoothingSeconds"/>.
    /// This is a partial mitigation for a narrow, verifiable pattern (a brief round-trip
    /// back to the same pitch), not a general "off-key note" fix: genuine chromatic
    /// content (passing tones, deliberate runs) moves on to a different pitch and is left
    /// alone by design, and a sustained mistracked pitch lasting longer than the threshold
    /// is real transcription inaccuracy this doesn't address.
    /// </remarks>
    internal sealed class GameTranscriber
    {
        private const double ChunkSeconds = 24.0;
        private const double OverlapSeconds = 4.0; // real-audio context borrowed from each neighboring chunk
        private const float DefaultAmplitude = 0.8f; // GAME has no per-note confidence/velocity signal

        public List<TranscribedNote> Transcribe(string audioPath, GameSettings settings, IProgress<string> progress = null)
        {
            settings = settings ?? GameSettings.CreateDefault();
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
                var overlapSamples = (int)(OverlapSeconds * model.SampleRate);
                var chunkSamples = (int)(ChunkSeconds * model.SampleRate);
                var totalChunks = Math.Max(1, (int)Math.Ceiling((double)samples.Length / chunkSamples));

                var notes = new List<TranscribedNote>();
                var chunkIndex = 0;
                for (var chunkStart = 0; chunkStart < samples.Length; chunkStart += chunkSamples)
                {
                    chunkIndex++;
                    progress?.Report($"Transcribing (vocal) chunk {chunkIndex} of {totalChunks}...");

                    var chunkLength = Math.Min(chunkSamples, samples.Length - chunkStart);
                    var coreStartSeconds = (double)chunkStart / model.SampleRate;
                    var coreEndSeconds = (double)(chunkStart + chunkLength) / model.SampleRate;

                    // Window = [chunkStart - overlap, chunkStart + chunkLength + overlap),
                    // clamped to the real sample array; only falls back to zero at the
                    // true start/end of the whole clip, where there is no neighbor to
                    // borrow real audio from anyway.
                    var windowStart = Math.Max(0, chunkStart - overlapSamples);
                    var windowEnd = Math.Min(samples.Length, chunkStart + chunkLength + overlapSamples);
                    var leadingZeros = Math.Max(0, overlapSamples - chunkStart);
                    var padded = new float[leadingZeros + (windowEnd - windowStart)];
                    Array.Copy(samples, windowStart, padded, leadingZeros, windowEnd - windowStart);

                    var chunkOffsetSeconds = (double)(chunkStart - overlapSamples) / model.SampleRate;
                    var gameNotes = model.Infer(
                        padded, (float)padded.Length / model.SampleRate,
                        segThreshold: settings.SegThreshold, segRadiusFrames: settings.SegRadiusFrames, estThreshold: settings.EstThreshold, ts: ts);

                    foreach (var n in gameNotes)
                    {
                        var start = n.Start + chunkOffsetSeconds;
                        var end = n.End + chunkOffsetSeconds;
                        if (end <= 0)
                        {
                            continue; // fell entirely in the leading pad
                        }
                        if (start < coreStartSeconds || start >= coreEndSeconds)
                        {
                            continue; // onset belongs to a neighboring chunk's core -- that chunk owns it
                        }
                        notes.Add(new TranscribedNote(Math.Max(0, start), end, (int)Math.Round(n.Pitch), DefaultAmplitude));
                    }
                }
                return MergeAdjacentSamePitch(SmoothVibrato(MergeAdjacentSamePitch(notes), settings.VibratoSmoothingSeconds));
            }
        }

        // GAME occasionally reports one sustained pitch as several back-to-back fragments
        // (identical pitch, near-zero gap) -- always safe to collapse into a single note.
        private static List<TranscribedNote> MergeAdjacentSamePitch(List<TranscribedNote> notes)
        {
            const double gapTolerance = 0.02;
            var sorted = notes.OrderBy(n => n.Start).ToList();
            var merged = new List<TranscribedNote>();
            foreach (var n in sorted)
            {
                if (merged.Count > 0 && merged[merged.Count - 1].Pitch == n.Pitch && n.Start - merged[merged.Count - 1].End <= gapTolerance)
                {
                    var prev = merged[merged.Count - 1];
                    merged[merged.Count - 1] = new TranscribedNote(prev.Start, n.End, prev.Pitch, Math.Max(prev.Amplitude, n.Amplitude));
                }
                else
                {
                    merged.Add(n);
                }
            }
            return merged;
        }

        // Absorbs a short note that dips or rises away from a pitch and immediately
        // returns to that exact same pitch -- the signature of vibrato mis-segmented as a
        // separate note -- into the note it interrupts. A note that moves on to a
        // genuinely different pitch (a passing tone, a chromatic run) is left untouched,
        // since its neighbors won't agree on the same pitch on both sides.
        private static List<TranscribedNote> SmoothVibrato(List<TranscribedNote> notes, double maxSmoothedSeconds)
        {
            if (maxSmoothedSeconds <= 0)
            {
                return notes;
            }

            const int maxSemitoneDepth = 2;
            const double gapTolerance = 0.02;
            var result = new List<TranscribedNote>();
            var i = 0;
            while (i < notes.Count)
            {
                if (result.Count > 0 && i < notes.Count - 1)
                {
                    var before = result[result.Count - 1];
                    var current = notes[i];
                    var after = notes[i + 1];
                    var isVibratoBlip =
                        before.Pitch == after.Pitch
                        && current.Pitch != before.Pitch
                        && Math.Abs(current.Pitch - before.Pitch) <= maxSemitoneDepth
                        && current.End - current.Start <= maxSmoothedSeconds
                        && current.Start - before.End <= gapTolerance
                        && after.Start - current.End <= gapTolerance;

                    if (isVibratoBlip)
                    {
                        result[result.Count - 1] = new TranscribedNote(before.Start, after.End, before.Pitch, Math.Max(before.Amplitude, after.Amplitude));
                        i += 2;
                        continue;
                    }
                }

                result.Add(notes[i]);
                i++;
            }
            return result;
        }
    }
}
