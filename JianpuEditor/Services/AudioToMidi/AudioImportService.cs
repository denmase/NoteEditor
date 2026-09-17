using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Models;

namespace JianpuEditor.Services.AudioToMidi
{
    /// <summary>
    /// Audio-to-MIDI transcription for a single-instrument (STEM) recording. Picks
    /// between two ONNX engines (<see cref="BasicPitchTranscriber"/> for instruments,
    /// <see cref="GameTranscriber"/> for vocals), writes the resulting notes as a
    /// temporary MIDI file, and hands that to the existing <see cref="IMidiImportService"/>
    /// pipeline -- reusing its MIDI-to-score conversion rather than building a
    /// <see cref="JianpuScore"/> from scratch.
    /// </summary>
    /// <remarks>
    /// This is a prototype. basic-pitch was validated against synthesized audio with
    /// known ground truth; GAME was additionally validated against a real vocal
    /// recording. Known limitations: a note re-struck at the same pitch with no
    /// silence gap can be merged into one note by either engine (a general hard case
    /// for this class of model), and GAME chunks long audio into ~24s windows, so a
    /// note landing exactly on a chunk boundary can be split into two.
    /// </remarks>
    internal sealed class AudioImportService : IAudioImportService
    {
        private readonly IMidiImportService _midiImportService;
        private readonly BasicPitchTranscriber _basicPitchTranscriber = new BasicPitchTranscriber();
        private readonly GameTranscriber _gameTranscriber = new GameTranscriber();

        public AudioImportService(IMidiImportService midiImportService)
        {
            _midiImportService = midiImportService ?? throw new ArgumentNullException(nameof(midiImportService));
        }

        public JianpuScore Import(
            string audioPath,
            AudioTranscriptionEngine engine,
            BasicPitchSettings basicPitchSettings = null,
            GameSettings gameSettings = null,
            IProgress<string> progress = null)
        {
            if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath))
            {
                throw new FileNotFoundException("Audio file not found.", audioPath);
            }

            List<TranscribedNote> notes = engine == AudioTranscriptionEngine.Vocal
                ? _gameTranscriber.Transcribe(audioPath, gameSettings, progress)
                : _basicPitchTranscriber.Transcribe(audioPath, basicPitchSettings, progress);

            if (notes.Count == 0)
            {
                throw new InvalidOperationException("No notes were detected in this audio file.");
            }

            progress?.Report("Estimating tempo...");
            var bpm = TempoEstimator.EstimateBpm(notes);

            var onsetQuantizeGrid = engine == AudioTranscriptionEngine.Vocal
                ? (gameSettings ?? GameSettings.CreateDefault()).OnsetQuantizeGrid
                : (basicPitchSettings ?? BasicPitchSettings.CreateDefault()).OnsetQuantizeGrid;
            notes = OnsetQuantizer.Quantize(notes, bpm, onsetQuantizeGrid);

            progress?.Report("Estimating time signature...");
            var (timeSignatureNumerator, timeSignatureDenominator) = TimeSignatureEstimator.EstimateTimeSignature(notes, bpm);

            var tempMidiPath = Path.Combine(Path.GetTempPath(), "audio-import-" + Guid.NewGuid().ToString("N") + ".mid");
            try
            {
                SimpleMidiWriter.Write(
                    tempMidiPath,
                    notes.Select(n => (n.Start, n.End, n.Pitch, n.Amplitude)).ToList(),
                    bpm,
                    timeSignatureNumerator,
                    timeSignatureDenominator);
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
    }
}
