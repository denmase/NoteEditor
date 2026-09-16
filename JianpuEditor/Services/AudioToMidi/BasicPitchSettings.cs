namespace JianpuEditor.Services.AudioToMidi
{
    /// <summary>Tunable basic-pitch decoding thresholds, exposed to the user before each
    /// import so they can experiment without a code change.</summary>
    public sealed class BasicPitchSettings
    {
        public float OnsetThreshold = 0.5f;
        public float FrameThreshold = 0.3f;
        public int MinNoteLenFrames = 11; // basic-pitch's own default (~127.7ms)
        public double MergeGapSeconds = 0.06; // bridges a spurious re-onset mid-sustain
        public float MinAmplitude = 0.35f; // drops low-confidence/harmonic-bleed detections

        public static BasicPitchSettings CreateDefault()
        {
            return new BasicPitchSettings();
        }

        public BasicPitchSettings Clone()
        {
            return new BasicPitchSettings
            {
                OnsetThreshold = OnsetThreshold,
                FrameThreshold = FrameThreshold,
                MinNoteLenFrames = MinNoteLenFrames,
                MergeGapSeconds = MergeGapSeconds,
                MinAmplitude = MinAmplitude
            };
        }
    }
}
