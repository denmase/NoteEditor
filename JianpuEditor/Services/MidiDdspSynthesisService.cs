using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Models;
using JianpuEditor.Rendering;
using MidiDdsp.Core;
using MidiDdsp.Core.Midi;
using MidiDdsp.Core.Models;

namespace JianpuEditor.Services
{
    public sealed class MidiDdspSynthesisService : IMidiDdspSynthesisService
    {
        private readonly object _loadLock = new object();
        private MidiDdspSynthesizer _synthesizer;
        private string _loadedFromPath;
        private bool _loadFailed;

        public bool IsConfigured
        {
            get
            {
                var path = AppTheme.MidiDdspWeightsPath;
                return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
            }
        }

        public bool IsInstrumentSupported(int midiProgram)
        {
            return Instruments.ForMidiProgram(midiProgram) != null;
        }

        public Task<MidiDdspRenderResult> RenderAsync(JianpuScore score, CancellationToken cancellationToken)
        {
            if (score == null)
            {
                throw new ArgumentNullException(nameof(score));
            }

            return Task.Run(() => Render(score, cancellationToken), cancellationToken);
        }

        private MidiDdspRenderResult Render(JianpuScore score, CancellationToken cancellationToken)
        {
            if (!IsConfigured)
            {
                return MidiDdspRenderResult.Empty;
            }

            var hasEligiblePart =
                IsInstrumentSupported(score.MelodyInstrument) ||
                IsInstrumentSupported(score.ChordInstrument) ||
                Enumerable.Range(0, ScoreMidiSchedule.GetExtraVoiceRoleLabels(score).Count)
                    .Any(i => IsInstrumentSupported(ScoreMidiSchedule.GetExtraVoiceInstrument(score, i)));
            if (!hasEligiblePart)
            {
                return MidiDdspRenderResult.Empty;
            }

            var synthesizer = GetOrLoadSynthesizer();
            if (synthesizer == null)
            {
                return MidiDdspRenderResult.Empty;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var tempMidiPath = Path.Combine(Path.GetTempPath(), "jianpu-ddsp-render-" + Guid.NewGuid().ToString("N") + ".mid");
            try
            {
                MidiExportService.Export(score, tempMidiPath);
                cancellationToken.ThrowIfCancellationRequested();

                var midiFile = MidiFile.Load(tempMidiPath);
                var result = synthesizer.Synthesize(midiFile, new SynthesisOptions());
                if (!result.Parts.Any())
                {
                    return MidiDdspRenderResult.Empty;
                }

                return new MidiDdspRenderResult
                {
                    Samples = result.Mix,
                    SampleRate = MidiDdsp.Core.Dsp.DdspMath.SampleRate,
                    RenderedChannels = result.Parts.Select(p => p.Part.Channel).Distinct().ToArray()
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppLog.Exception("MIDI-DDSP render failed", ex);
                return MidiDdspRenderResult.Empty;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempMidiPath))
                    {
                        File.Delete(tempMidiPath);
                    }
                }
                catch (IOException)
                {
                    // Best-effort cleanup.
                }
            }
        }

        private MidiDdspSynthesizer GetOrLoadSynthesizer()
        {
            var path = AppTheme.MidiDdspWeightsPath;
            lock (_loadLock)
            {
                if (_synthesizer != null && _loadedFromPath == path)
                {
                    return _synthesizer;
                }

                if (_loadFailed && _loadedFromPath == path)
                {
                    return null;
                }

                try
                {
                    _synthesizer = MidiDdspSynthesizer.Load(path);
                    _loadedFromPath = path;
                    _loadFailed = false;
                    AppLog.Info("MIDI-DDSP model loaded from " + path);
                    return _synthesizer;
                }
                catch (Exception ex)
                {
                    AppLog.Exception("Failed to load MIDI-DDSP model from " + path, ex);
                    _synthesizer = null;
                    _loadedFromPath = path;
                    _loadFailed = true;
                    return null;
                }
            }
        }
    }
}
