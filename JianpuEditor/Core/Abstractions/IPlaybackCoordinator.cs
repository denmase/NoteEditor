namespace JianpuEditor.Core.Abstractions
{
    /// <summary>
    /// Enforces that only one tab is audibly playing at a time through the single shared
    /// <see cref="IMidiOutput"/>, even though each tab owns its own scoped
    /// <see cref="IScorePlaybackService"/>/transport. A process-wide singleton, unlike the
    /// playback services it coordinates.
    /// </summary>
    public interface IPlaybackCoordinator
    {
        /// <summary>Call when a tab starts playing. Stops whichever other tab was previously
        /// playing, if any, then remembers <paramref name="controller"/> as the current one.</summary>
        void NotifyPlaying(IPlaybackController controller);

        /// <summary>Call when a tab stops playing (by user action, natural finish, or tab
        /// close/dispose). A no-op unless <paramref name="controller"/> is the tab currently
        /// remembered as playing.</summary>
        void NotifyStopped(IPlaybackController controller);
    }
}
