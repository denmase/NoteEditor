namespace JianpuEditor.Services.AudioToMidi
{
    /// <summary>Tunable GAME decoding thresholds, exposed to the user before each import
    /// so they can experiment without a code change.</summary>
    public sealed class GameSettings
    {
        public float SegThreshold { get; set; } = 0.2f;

        public long SegRadiusFrames { get; set; } = 2;

        public float EstThreshold { get; set; } = 0.2f;

        /// <summary>A note flanked on both sides by one identical pitch is merged into
        /// that surrounding note when its own duration falls between
        /// <see cref="MinVibratoSmoothingSeconds"/> and this -- catches a vibrato dip/rise
        /// that the segmenter mistakes for a distinct note without touching genuine
        /// passing tones or chromatic runs (which move on to a different pitch rather
        /// than returning to where they started). Grounded in normal human vibrato rate
        /// (commonly cited as ~3-9 Hz, e.g. in vocal-science vibrato analysis tools such
        /// as VibratoScope): a half-cycle "dip" at the slow (3 Hz) end lasts about 167ms.
        /// 0 disables smoothing entirely.</summary>
        public double MaxVibratoSmoothingSeconds { get; set; } = 0.15;

        /// <summary>The other end of the vibrato-smoothing window: a half-cycle dip at the
        /// fast (9 Hz) end of normal human vibrato lasts about 56ms, so anything shorter
        /// is more likely decoder noise than a real, perceptible vibrato wobble and is
        /// left alone rather than merged. 0 disables the floor (any short blip qualifies,
        /// down to zero duration).</summary>
        public double MinVibratoSmoothingSeconds { get; set; } = 0.05;

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
                MaxVibratoSmoothingSeconds = MaxVibratoSmoothingSeconds,
                MinVibratoSmoothingSeconds = MinVibratoSmoothingSeconds
            };
        }
    }
}
