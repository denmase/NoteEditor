using System;
using JianpuEditor.Models;
using JianpuEditor.Services.AudioToMidi;

namespace JianpuEditor.Core.Abstractions
{
    /// <summary>Transcribes a single-instrument (STEM) audio recording into a score.</summary>
    public interface IAudioImportService
    {
        JianpuScore Import(
            string audioPath,
            AudioTranscriptionEngine engine,
            BasicPitchSettings basicPitchSettings = null,
            GameSettings gameSettings = null,
            IProgress<string> progress = null);
    }
}
