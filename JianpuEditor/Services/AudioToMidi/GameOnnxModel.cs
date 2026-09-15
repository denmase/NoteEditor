using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Newtonsoft.Json.Linq;

namespace JianpuEditor.Services.AudioToMidi
{
    /// <summary>
    /// Runs openvpi/GAME's ONNX model suite (encoder -> D3PM segmenter loop -> bd2dur ->
    /// estimator), ported from the reference implementation in Xiantaidu/Vocal2Midi's
    /// inference/game/onnx_runtime.py and validated against a Python port of the same
    /// reference (numerically matching, including on real vocal audio) before this file
    /// was written. Unlike basic-pitch, GAME's encoder has no internal windowing -- it
    /// runs self-attention over the whole input, whose cost scales roughly quadratically
    /// with duration. Feeding it more than ~30-45s at once risks multi-GB memory spikes
    /// (confirmed empirically: a 3-minute clip fed whole was OOM-killed). Callers must
    /// chunk long audio themselves -- see <see cref="GameTranscriber"/>.
    /// </summary>
    internal sealed class GameOnnxModel : IDisposable
    {
        public int SampleRate { get; }
        public float Timestep { get; }
        public bool Loop { get; }

        private readonly InferenceSession _encoder;
        private readonly InferenceSession _segmenter;
        private readonly InferenceSession _estimator;
        private readonly InferenceSession _bd2dur;

        public GameOnnxModel(string modelDir)
        {
            var configPath = Path.Combine(modelDir, "config.json");
            var config = JObject.Parse(File.ReadAllText(configPath));
            SampleRate = config.Value<int>("samplerate");
            Timestep = config.Value<float>("timestep");
            Loop = config.Value<bool>("loop");

            _encoder = new InferenceSession(Path.Combine(modelDir, "encoder.onnx"));
            _segmenter = new InferenceSession(Path.Combine(modelDir, "segmenter.onnx"));
            _estimator = new InferenceSession(Path.Combine(modelDir, "estimator.onnx"));
            _bd2dur = new InferenceSession(Path.Combine(modelDir, "bd2dur.onnx"));
        }

        public void Dispose()
        {
            _encoder.Dispose();
            _segmenter.Dispose();
            _estimator.Dispose();
            _bd2dur.Dispose();
        }

        public struct Note
        {
            public double Start;
            public double End;
            public float Pitch;
        }

        /// <summary>GAME's own infer.py CLI default: t0=0.0, nsteps=8.</summary>
        public static double[] DefaultTs(double t0 = 0.0, int nsteps = 8)
        {
            var step = (1 - t0) / nsteps;
            var ts = new double[nsteps];
            for (var i = 0; i < nsteps; i++)
            {
                ts[i] = t0 + i * step;
            }
            return ts;
        }

        /// <summary>
        /// Single-clip (batch size 1) inference, no lyric alignment (known_boundaries
        /// all-zero, matching Vocal2Midi's no-lyrics mode). Caller is responsible for
        /// keeping <paramref name="waveform"/> short enough (~30-45s or less) -- see
        /// the class remarks.
        /// </summary>
        public List<Note> Infer(
            float[] waveform, float durationSeconds,
            float segThreshold = 0.2f, long segRadiusFrames = 2, float estThreshold = 0.2f,
            double[] ts = null)
        {
            var waveformTensor = new DenseTensor<float>(new[] { 1, waveform.Length });
            for (var i = 0; i < waveform.Length; i++)
            {
                waveformTensor[0, i] = waveform[i];
            }
            var durationTensor = new DenseTensor<float>(new[] { 1 });
            durationTensor[0] = durationSeconds;

            Tensor<float> xSeg, xEst;
            Tensor<bool> maskT;
            using (var encOut = _encoder.Run(new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("waveform", waveformTensor),
                NamedOnnxValue.CreateFromTensor("duration", durationTensor),
            }))
            {
                var encList = new List<DisposableNamedOnnxValue>(encOut);
                xSeg = CloneTensor(encList[0].AsTensor<float>());
                xEst = CloneTensor(encList[1].AsTensor<float>());
                maskT = CloneTensor(encList[2].AsTensor<bool>());
            }
            var tFrames = maskT.Dimensions[1];

            var knownBoundaries = new DenseTensor<bool>(new[] { 1, tFrames }); // all-false: no lyric alignment
            var boundaries = new DenseTensor<bool>(new[] { 1, tFrames });
            var langArr = new DenseTensor<long>(new[] { 1 }); // 0 = language-neutral (GAME's own recommendation)

            var xSegTensor = (DenseTensor<float>)xSeg;
            var maskTTensor = (DenseTensor<bool>)maskT;

            if (Loop && ts != null && ts.Length > 0)
            {
                foreach (var t in ts)
                {
                    var tTensor = new DenseTensor<float>(new[] { 1 });
                    tTensor[0] = (float)t;
                    boundaries = RunSegmenter(xSegTensor, langArr, knownBoundaries, boundaries, tTensor, maskTTensor, segThreshold, segRadiusFrames);
                }
            }
            else
            {
                var tTensor = new DenseTensor<float>(new[] { 1 });
                boundaries = RunSegmenter(xSegTensor, langArr, knownBoundaries, boundaries, tTensor, maskTTensor, segThreshold, segRadiusFrames);
            }

            DenseTensor<float> durations;
            DenseTensor<bool> maskN;
            using (var bdOut = _bd2dur.Run(new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("boundaries", boundaries),
                NamedOnnxValue.CreateFromTensor("maskT", maskTTensor),
            }))
            {
                var bdList = new List<DisposableNamedOnnxValue>(bdOut);
                durations = CloneTensor(bdList[0].AsTensor<float>());
                maskN = CloneTensor(bdList[1].AsTensor<bool>());
            }

            var estThresholdTensor = new DenseTensor<float>(Array.Empty<int>());
            estThresholdTensor.Buffer.Span[0] = estThreshold;

            Tensor<bool> presence;
            Tensor<float> scores;
            using (var estOut = _estimator.Run(new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("x_est", (DenseTensor<float>)xEst),
                NamedOnnxValue.CreateFromTensor("boundaries", boundaries),
                NamedOnnxValue.CreateFromTensor("maskT", maskTTensor),
                NamedOnnxValue.CreateFromTensor("maskN", maskN),
                NamedOnnxValue.CreateFromTensor("threshold", estThresholdTensor),
            }))
            {
                var estList = new List<DisposableNamedOnnxValue>(estOut);
                presence = CloneTensor(estList[0].AsTensor<bool>());
                scores = CloneTensor(estList[1].AsTensor<float>());
            }

            var notes = new List<Note>();
            double t0Time = 0;
            var n = durations.Dimensions[1];
            for (var i = 0; i < n; i++)
            {
                if (!maskN[0, i])
                {
                    break;
                }
                var dur = durations[0, i];
                if (presence[0, i])
                {
                    notes.Add(new Note { Start = t0Time, End = t0Time + dur, Pitch = scores[0, i] });
                }
                t0Time += dur;
            }
            return notes;
        }

        private DenseTensor<bool> RunSegmenter(
            DenseTensor<float> xSeg, DenseTensor<long> language, DenseTensor<bool> knownBoundaries,
            DenseTensor<bool> prevBoundaries, DenseTensor<float> t, DenseTensor<bool> maskT,
            float threshold, long radius)
        {
            var thresholdTensor = new DenseTensor<float>(Array.Empty<int>());
            thresholdTensor.Buffer.Span[0] = threshold;
            var radiusTensor = new DenseTensor<long>(Array.Empty<int>());
            radiusTensor.Buffer.Span[0] = radius;

            using (var results = _segmenter.Run(new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("x_seg", xSeg),
                NamedOnnxValue.CreateFromTensor("language", language),
                NamedOnnxValue.CreateFromTensor("known_boundaries", knownBoundaries),
                NamedOnnxValue.CreateFromTensor("prev_boundaries", prevBoundaries),
                NamedOnnxValue.CreateFromTensor("t", t),
                NamedOnnxValue.CreateFromTensor("maskT", maskT),
                NamedOnnxValue.CreateFromTensor("threshold", thresholdTensor),
                NamedOnnxValue.CreateFromTensor("radius", radiusTensor),
            }))
            {
                var list = new List<DisposableNamedOnnxValue>(results);
                return CloneTensor(list[0].AsTensor<bool>());
            }
        }

        private static DenseTensor<T> CloneTensor<T>(Tensor<T> t)
        {
            var dims = new int[t.Dimensions.Length];
            for (var i = 0; i < dims.Length; i++)
            {
                dims[i] = t.Dimensions[i];
            }
            var result = new DenseTensor<T>(dims);
            var i2 = 0;
            foreach (var value in t)
            {
                result.Buffer.Span[i2] = value;
                i2++;
            }
            return result;
        }
    }
}
