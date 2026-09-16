namespace JianpuEditor.Services.AudioToMidi
{
    /// <summary>Tunable GAME decoding thresholds, exposed to the user before each import
    /// so they can experiment without a code change.</summary>
    public sealed class GameSettings
    {
        public float SegThreshold = 0.2f;
        public long SegRadiusFrames = 2;
        public float EstThreshold = 0.2f;

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
                EstThreshold = EstThreshold
            };
        }
    }
}
