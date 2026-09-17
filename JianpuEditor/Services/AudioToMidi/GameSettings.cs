namespace JianpuEditor.Services.AudioToMidi
{
    /// <summary>Tunable GAME decoding thresholds, exposed to the user before each import
    /// so they can experiment without a code change.</summary>
    public sealed class GameSettings
    {
        public float SegThreshold { get; set; } = 0.2f;

        public long SegRadiusFrames { get; set; } = 2;

        public float EstThreshold { get; set; } = 0.2f;

        /// <summary>Any note no longer than this, immediately flanked on both sides by a
        /// note of one identical pitch, is merged into that surrounding note -- catches a
        /// vibrato dip/rise that the segmenter mistakes for a distinct note without
        /// touching genuine passing tones or chromatic runs (which move on to a different
        /// pitch rather than returning to where they started). 0 disables this.</summary>
        public double VibratoSmoothingSeconds { get; set; } = 0.15;

        public static GameSettings CreateDefault()
        {
            return new GameSettings();
        }

        public GameSettings Clone()
        {
            return new GameSettings
            {
                SegThreshold = SegThreshold,
                SegRadiusFrames = SegRadiusFrames,
                EstThreshold = EstThreshold,
                VibratoSmoothingSeconds = VibratoSmoothingSeconds
            };
        }
    }
}
