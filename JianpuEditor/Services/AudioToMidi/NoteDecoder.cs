using System;
using System.Collections.Generic;
using System.Linq;

namespace JianpuEditor.Services.AudioToMidi
{
    /// <summary>
    /// Ports basic_pitch.note_creation's onset/frame decoding (output_to_notes_polyphonic,
    /// get_infered_onsets, model_frames_to_time) from Python to C#. Pitch bends and
    /// min/max frequency constraints are omitted -- not needed for Jianpu notation, which
    /// has no concept of a bent pitch.
    /// </summary>
    internal static class NoteDecoder
    {
        private const int MidiOffset = 21;
        private const int MaxFreqIdx = 87; // 88 bins, 0..87
        private const int EnergyTol = 11;

        public readonly struct Note
        {
            public Note(int startFrame, int endFrame, int pitch, float amplitude)
            {
                StartFrame = startFrame;
                EndFrame = endFrame;
                Pitch = pitch;
                Amplitude = amplitude;
            }

            public int StartFrame { get; }
            public int EndFrame { get; }
            public int Pitch { get; }
            public float Amplitude { get; }
        }

        public readonly struct TimedNote
        {
            public TimedNote(double start, double end, int pitch, float amplitude)
            {
                Start = start;
                End = end;
                Pitch = pitch;
                Amplitude = amplitude;
            }

            public double Start { get; }
            public double End { get; }
            public int Pitch { get; }
            public float Amplitude { get; }
        }

        public static List<Note> DecodeFrames(
            float[,] frames, float[,] onsets,
            float onsetThresh, float frameThresh, int minNoteLen,
            bool inferOnsets = true, bool melodiaTrick = true)
        {
            var nFrames = frames.GetLength(0);
            var bins = frames.GetLength(1);

            var effectiveOnsets = inferOnsets ? GetInferredOnsets(onsets, frames) : (float[,])onsets.Clone();

            // argrelmax along the time axis (per frequency column): strict local maxima.
            var peakThresh = new float[nFrames, bins];
            for (var f = 0; f < bins; f++)
            {
                for (var t = 1; t < nFrames - 1; t++)
                {
                    var v = effectiveOnsets[t, f];
                    if (v > effectiveOnsets[t - 1, f] && v > effectiveOnsets[t + 1, f])
                    {
                        peakThresh[t, f] = v;
                    }
                }
            }

            var onsetIdx = new List<(int t, int f)>();
            for (var t = 0; t < nFrames; t++)
            {
                for (var f = 0; f < bins; f++)
                {
                    if (peakThresh[t, f] >= onsetThresh)
                    {
                        onsetIdx.Add((t, f));
                    }
                }
            }
            onsetIdx.Reverse(); // "go backwards in time", matching basic_pitch's [::-1]

            var remainingEnergy = (float[,])frames.Clone();
            var noteEvents = new List<Note>();

            foreach (var (noteStartIdx, freqIdx) in onsetIdx)
            {
                if (noteStartIdx >= nFrames - 1)
                {
                    continue;
                }

                var i = noteStartIdx + 1;
                var k = 0;
                while (i < nFrames - 1 && k < EnergyTol)
                {
                    if (remainingEnergy[i, freqIdx] < frameThresh)
                    {
                        k++;
                    }
                    else
                    {
                        k = 0;
                    }
                    i++;
                }
                i -= k;

                if (i - noteStartIdx <= minNoteLen)
                {
                    continue;
                }

                for (var t = noteStartIdx; t < i; t++)
                {
                    remainingEnergy[t, freqIdx] = 0;
                    if (freqIdx < MaxFreqIdx)
                    {
                        remainingEnergy[t, freqIdx + 1] = 0;
                    }
                    if (freqIdx > 0)
                    {
                        remainingEnergy[t, freqIdx - 1] = 0;
                    }
                }

                var amplitude = Mean(frames, noteStartIdx, i, freqIdx);
                noteEvents.Add(new Note(noteStartIdx, i, freqIdx + MidiOffset, amplitude));
            }

            if (melodiaTrick)
            {
                while (true)
                {
                    var (maxVal, iMid, freqIdx) = ArgMax(remainingEnergy);
                    if (maxVal <= frameThresh)
                    {
                        break;
                    }

                    remainingEnergy[iMid, freqIdx] = 0;

                    int i = iMid + 1, k = 0;
                    while (i < nFrames - 1 && k < EnergyTol)
                    {
                        if (remainingEnergy[i, freqIdx] < frameThresh)
                        {
                            k++;
                        }
                        else
                        {
                            k = 0;
                        }
                        remainingEnergy[i, freqIdx] = 0;
                        if (freqIdx < MaxFreqIdx)
                        {
                            remainingEnergy[i, freqIdx + 1] = 0;
                        }
                        if (freqIdx > 0)
                        {
                            remainingEnergy[i, freqIdx - 1] = 0;
                        }
                        i++;
                    }
                    var iEnd = i - 1 - k;

                    i = iMid - 1; k = 0;
                    while (i > 0 && k < EnergyTol)
                    {
                        if (remainingEnergy[i, freqIdx] < frameThresh)
                        {
                            k++;
                        }
                        else
                        {
                            k = 0;
                        }
                        remainingEnergy[i, freqIdx] = 0;
                        if (freqIdx < MaxFreqIdx)
                        {
                            remainingEnergy[i, freqIdx + 1] = 0;
                        }
                        if (freqIdx > 0)
                        {
                            remainingEnergy[i, freqIdx - 1] = 0;
                        }
                        i--;
                    }
                    var iStart = i + 1 + k;

                    if (iEnd - iStart <= minNoteLen)
                    {
                        continue;
                    }

                    var amplitude = Mean(frames, iStart, iEnd, freqIdx);
                    noteEvents.Add(new Note(iStart, iEnd, freqIdx + MidiOffset, amplitude));
                }
            }

            return noteEvents;
        }

        public static List<TimedNote> ToTimedNotes(List<Note> notes, int nFrames)
        {
            var times = ModelFramesToTime(nFrames);
            return notes.Select(n => new TimedNote(times[n.StartFrame], times[n.EndFrame], n.Pitch, n.Amplitude)).ToList();
        }

        private static double[] ModelFramesToTime(int nFrames)
        {
            const int fftHop = BasicPitchModel.FftHop;
            const int sr = BasicPitchModel.AudioSampleRate;
            const int annotNFrames = BasicPitchModel.AnnotFrames;
            const int audioNSamples = BasicPitchModel.AudioNSamples;

            // Corrects for the FFT_HOP granularity mismatch across window boundaries after
            // concatenation -- ported as-is from basic_pitch.note_creation.model_frames_to_time.
            var windowOffset = (fftHop / (double)sr) * (annotNFrames - (audioNSamples / (double)fftHop)) + 0.0018;

            var times = new double[nFrames];
            for (var t = 0; t < nFrames; t++)
            {
                var originalTime = t * (double)fftHop / sr;
                var windowNumber = Math.Floor(t / (double)annotNFrames);
                times[t] = originalTime - windowOffset * windowNumber;
            }
            return times;
        }

        private static float[,] GetInferredOnsets(float[,] onsets, float[,] frames, int nDiff = 2)
        {
            var nFrames = frames.GetLength(0);
            var bins = frames.GetLength(1);

            var frameDiff = new float[nFrames, bins];
            for (var t = 0; t < nFrames; t++)
            {
                for (var f = 0; f < bins; f++)
                {
                    frameDiff[t, f] = float.MaxValue;
                }
            }

            for (var n = 1; n <= nDiff; n++)
            {
                for (var t = 0; t < nFrames; t++)
                {
                    for (var f = 0; f < bins; f++)
                    {
                        var prev = t - n >= 0 ? frames[t - n, f] : 0f;
                        var diff = frames[t, f] - prev;
                        if (diff < frameDiff[t, f])
                        {
                            frameDiff[t, f] = diff;
                        }
                    }
                }
            }

            var maxOnset = 0f;
            for (var t = 0; t < nFrames; t++)
            {
                for (var f = 0; f < bins; f++)
                {
                    if (onsets[t, f] > maxOnset)
                    {
                        maxOnset = onsets[t, f];
                    }
                }
            }

            for (var t = 0; t < nFrames; t++)
            {
                for (var f = 0; f < bins; f++)
                {
                    if (frameDiff[t, f] < 0)
                    {
                        frameDiff[t, f] = 0;
                    }
                }
            }

            for (var t = 0; t < Math.Min(nDiff, nFrames); t++)
            {
                for (var f = 0; f < bins; f++)
                {
                    frameDiff[t, f] = 0;
                }
            }

            var maxDiff = 0f;
            for (var t = 0; t < nFrames; t++)
            {
                for (var f = 0; f < bins; f++)
                {
                    if (frameDiff[t, f] > maxDiff)
                    {
                        maxDiff = frameDiff[t, f];
                    }
                }
            }

            var result = new float[nFrames, bins];
            for (var t = 0; t < nFrames; t++)
            {
                for (var f = 0; f < bins; f++)
                {
                    var rescaled = maxDiff > 0 ? maxOnset * frameDiff[t, f] / maxDiff : 0f;
                    result[t, f] = Math.Max(onsets[t, f], rescaled);
                }
            }
            return result;
        }

        private static float Mean(float[,] arr, int startInclusive, int endExclusive, int col)
        {
            var sum = 0f;
            var count = 0;
            for (var t = startInclusive; t < endExclusive; t++)
            {
                sum += arr[t, col];
                count++;
            }
            return count > 0 ? sum / count : 0f;
        }

        private static (float value, int t, int f) ArgMax(float[,] arr)
        {
            var rows = arr.GetLength(0);
            var cols = arr.GetLength(1);
            var best = float.NegativeInfinity;
            int bt = 0, bf = 0;
            for (var t = 0; t < rows; t++)
            {
                for (var f = 0; f < cols; f++)
                {
                    if (arr[t, f] > best)
                    {
                        best = arr[t, f];
                        bt = t;
                        bf = f;
                    }
                }
            }
            return (best, bt, bf);
        }
    }
}
