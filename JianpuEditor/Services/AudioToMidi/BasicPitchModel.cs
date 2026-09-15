using System;
using System.Collections.Generic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace JianpuEditor.Services.AudioToMidi
{
    /// <summary>
    /// Runs Spotify's basic-pitch ONNX model directly (no Python at runtime), porting
    /// basic_pitch.inference's windowing/unwrapping so the output matches the reference
    /// Python implementation frame-for-frame. Validated against a standalone C# spike
    /// that reproduced the Python (TensorFlow and ONNX Runtime) outputs bit-for-bit on
    /// synthetic audio with known ground truth.
    /// </summary>
    internal sealed class BasicPitchModel : IDisposable
    {
        public const int AudioSampleRate = 22050;
        public const int AudioNSamples = 43844;
        public const int FftHop = 256;
        public const int AnnotFrames = 172; // frames per window in model output
        private const int NOverlappingFrames = 30;
        private const double AnnotationsFps = 86.0;

        private readonly InferenceSession _session;

        public BasicPitchModel(string onnxPath)
        {
            _session = new InferenceSession(onnxPath);
        }

        public void Dispose() => _session.Dispose();

        public sealed class ModelOutput
        {
            public float[,] Note = null!;   // (n_times, 88)
            public float[,] Onset = null!;  // (n_times, 88)
        }

        public ModelOutput RunInference(float[] audioOriginal, Action<int, int> onWindow = null)
        {
            var overlapLen = NOverlappingFrames * FftHop; // 7680
            var hopSize = AudioNSamples - overlapLen;      // 36164
            var originalLength = audioOriginal.Length;

            // Prepend overlap_len/2 zeros, matching basic_pitch.inference.get_audio_input.
            var prePad = overlapLen / 2;
            var padded = new float[prePad + originalLength];
            Array.Copy(audioOriginal, 0, padded, prePad, originalLength);

            var totalWindows = Math.Max(1, (int)Math.Ceiling((double)padded.Length / hopSize));
            var noteWindows = new List<float[,]>();
            var onsetWindows = new List<float[,]>();

            var windowIndex = 0;
            for (var i = 0; i < padded.Length; i += hopSize)
            {
                windowIndex++;
                onWindow?.Invoke(windowIndex, totalWindows);

                var window = new float[AudioNSamples];
                var available = Math.Min(AudioNSamples, padded.Length - i);
                Array.Copy(padded, i, window, 0, available);
                // remaining entries stay zero (matches np.pad with zeros)

                var (note, onset) = RunWindow(window);
                noteWindows.Add(note);
                onsetWindows.Add(onset);

                if (available < AudioNSamples)
                {
                    break; // this was the last (padded) window
                }
            }

            return new ModelOutput
            {
                Note = Unwrap(noteWindows, originalLength),
                Onset = Unwrap(onsetWindows, originalLength),
            };
        }

        // Exact names basic_pitch.inference.Model.predict requests for the ONNX backend
        // -- confirmed against the installed basic-pitch 0.4.0 source during prototyping.
        private const string InputName = "serving_default_input_2:0";
        private const string NoteOutputName = "StatefulPartitionedCall:1";
        private const string OnsetOutputName = "StatefulPartitionedCall:2";

        private (float[,] note, float[,] onset) RunWindow(float[] window)
        {
            var inputTensor = new DenseTensor<float>(new[] { 1, AudioNSamples, 1 });
            for (var i = 0; i < AudioNSamples; i++)
            {
                inputTensor[0, i, 0] = window[i];
            }

            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(InputName, inputTensor) };
            var requestedOutputs = new List<string> { NoteOutputName, OnsetOutputName };

            using var results = _session.Run(inputs, requestedOutputs);
            var resultList = new List<DisposableNamedOnnxValue>(results);

            var note = ToArray(resultList[0].AsTensor<float>());
            var onset = ToArray(resultList[1].AsTensor<float>());
            return (note, onset);
        }

        private static float[,] ToArray(Tensor<float> t)
        {
            var frames = t.Dimensions[1];
            var bins = t.Dimensions[2];
            var arr = new float[frames, bins];
            for (var f = 0; f < frames; f++)
            {
                for (var b = 0; b < bins; b++)
                {
                    arr[f, b] = t[0, f, b];
                }
            }
            return arr;
        }

        private static float[,] Unwrap(List<float[,]> windows, int originalLength)
        {
            var nOlap = NOverlappingFrames / 2; // 15
            var bins = windows[0].GetLength(1);

            // Trim n_olap frames off both ends of every window, then concatenate.
            var trimmedRows = new List<float[]>();
            foreach (var w in windows)
            {
                var frames = w.GetLength(0);
                for (var f = nOlap; f < frames - nOlap; f++)
                {
                    var row = new float[bins];
                    for (var b = 0; b < bins; b++)
                    {
                        row[b] = w[f, b];
                    }
                    trimmedRows.Add(row);
                }
            }

            var nOutputFramesOriginal = (int)Math.Floor(originalLength * (AnnotationsFps / AudioSampleRate));
            var finalCount = Math.Min(nOutputFramesOriginal, trimmedRows.Count);

            var result = new float[finalCount, bins];
            for (var f = 0; f < finalCount; f++)
            {
                for (var b = 0; b < bins; b++)
                {
                    result[f, b] = trimmedRows[f][b];
                }
            }
            return result;
        }
    }
}
