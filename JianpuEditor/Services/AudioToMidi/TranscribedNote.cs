namespace JianpuEditor.Services.AudioToMidi
{
    /// <summary>One transcribed note, in the shared shape both engines (basic-pitch and
    /// GAME) produce after their own model-specific decoding.</summary>
    internal readonly struct TranscribedNote
    {
        public TranscribedNote(double start, double end, int pitch, float amplitude)
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
}
