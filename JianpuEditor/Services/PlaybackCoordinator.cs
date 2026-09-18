using System;
using JianpuEditor.Core.Abstractions;

namespace JianpuEditor.Services
{
    public sealed class PlaybackCoordinator : IPlaybackCoordinator
    {
        private IPlaybackController _current;

        public void NotifyPlaying(IPlaybackController controller)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            var previous = _current;
            _current = controller;
            if (previous != null && !ReferenceEquals(previous, controller))
            {
                previous.StopPlayback();
            }
        }

        public void NotifyStopped(IPlaybackController controller)
        {
            if (ReferenceEquals(_current, controller))
            {
                _current = null;
            }
        }
    }
}
