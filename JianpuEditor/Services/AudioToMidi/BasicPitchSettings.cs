namespace JianpuEditor.Services.AudioToMidi
{
    /// <summary>Tunable basic-pitch decoding thresholds, exposed to the user before each
    /// import so they can experiment without a code change.</summary>
    public sealed class BasicPitchSettings
    {
        public float OnsetThreshold { get; set; } = 0.5f;

        public float FrameThreshold { get; set; } = 0.3f;

        public int MinNoteLenFrames { get; set; } = 11; // basic-pitch's own default (~127.7ms)

        public double MergeGapSeconds { get; set; } = 0.06; // bridges a spurious re-onset mid-sustain

        public float MinAmplitude { get; set; } = 0.35f; // drops low-confidence/harmonic-bleed detections

        /// <summary>Snaps each transcribed note's onset to the nearest multiple of this many
        /// quarter-note beats (e.g. 0.25 = nearest sixteenth) before the temporary MIDI file is
        /// written, using the already-estimated tempo. Raw transcription onsets have no reason
        /// to land on any notatable grid, and left uncorrected that jitter can exceed the small
        /// gap tolerance <c>MidiImportService.BuildMeasures</c> allows and show up as a spurious
        /// rest wedged between two notes that should be adjacent. 0 disables quantization
        /// entirely. Values finer than a sixteenth (0.25) have no effect beyond what
        /// <c>MidiImportService</c> already re-snaps onsets to on import, since jianpu notation
        /// has no shorter notatable duration.</summary>
        public double OnsetQuantizeGrid { get; set; } = 0.25;

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
                MinAmplitude = MinAmplitude,
                OnsetQuantizeGrid = OnsetQuantizeGrid
            };
        }
    }
}
