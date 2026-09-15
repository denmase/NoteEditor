namespace JianpuEditor.Core.Abstractions
{
    /// <summary>Which model transcribes an audio-to-MIDI import.</summary>
    public enum AudioTranscriptionEngine
    {
        /// <summary>basic-pitch: monophonic and polyphonic instruments (piano, guitar, lead lines).</summary>
        Instrument,

        /// <summary>GAME (openvpi): monophonic singing voice only, but more accurate on vocals.</summary>
        Vocal,
    }
}
