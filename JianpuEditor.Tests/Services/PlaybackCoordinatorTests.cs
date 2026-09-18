using JianpuEditor.Core.Abstractions;
using JianpuEditor.Services;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public sealed class PlaybackCoordinatorTests
    {
        [Fact]
        public void NotifyPlaying_StopsWhicheverControllerWasPreviouslyPlaying()
        {
            var coordinator = new PlaybackCoordinator();
            var first = new RecordingController();
            var second = new RecordingController();

            coordinator.NotifyPlaying(first);
            coordinator.NotifyPlaying(second);

            Assert.Equal(1, first.StopCount);
            Assert.Equal(0, second.StopCount);
        }

        [Fact]
        public void NotifyPlaying_SameControllerAgain_DoesNotStopItself()
        {
            var coordinator = new PlaybackCoordinator();
            var controller = new RecordingController();

            coordinator.NotifyPlaying(controller);
            coordinator.NotifyPlaying(controller);

            Assert.Equal(0, controller.StopCount);
        }

        [Fact]
        public void NotifyStopped_ThenNotifyPlayingElsewhere_DoesNotReStopTheOriginal()
        {
            // Once a controller has told the coordinator it stopped (Stop(), a natural finish, or
            // its tab closing/disposing), it must no longer be treated as "the one to stop" --
            // otherwise a later tab starting playback would call Stop() on an already-stopped or
            // even disposed controller.
            var coordinator = new PlaybackCoordinator();
            var first = new RecordingController();
            var second = new RecordingController();

            coordinator.NotifyPlaying(first);
            coordinator.NotifyStopped(first);
            coordinator.NotifyPlaying(second);

            Assert.Equal(0, first.StopCount);
        }

        [Fact]
        public void NotifyStopped_IgnoresAControllerThatIsNotTheCurrentOne()
        {
            var coordinator = new PlaybackCoordinator();
            var first = new RecordingController();
            var second = new RecordingController();

            coordinator.NotifyPlaying(first);
            coordinator.NotifyStopped(second); // Stale/unrelated notification -- first is still current.

            var third = new RecordingController();
            coordinator.NotifyPlaying(third);

            Assert.Equal(1, first.StopCount);
        }

        private sealed class RecordingController : IPlaybackController
        {
            public int StopCount { get; private set; }

            public void StopPlayback()
            {
                StopCount++;
            }
        }
    }
}
