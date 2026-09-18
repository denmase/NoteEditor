namespace JianpuEditor.Core.Abstractions
{
    /// <summary>
    /// A per-tab playback session that <see cref="IPlaybackCoordinator"/> can stop on another
    /// tab's behalf, without depending on <c>PlaybackViewModel</c> or any other ViewModel type.
    /// </summary>
    public interface IPlaybackController
    {
        void StopPlayback();
    }
}
