using JianpuEditor.Models;

namespace JianpuEditor.Core.Abstractions
{
    /// <summary>Transcribes a single-instrument (STEM) audio recording into a score.</summary>
    public interface IAudioImportService
    {
        JianpuScore Import(string audioPath, AudioTranscriptionEngine engine);
    }
}
