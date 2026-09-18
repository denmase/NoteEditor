using JianpuEditor.Services;
using JianpuEditor.ViewModels;
using Xunit;

namespace JianpuEditor.Tests.ViewModels
{
    /// <summary>
    /// Exercises the real wiring between <see cref="PlaybackViewModel"/> and
    /// <see cref="PlaybackCoordinator"/> (not the coordinator in isolation), proving two tabs
    /// sharing one coordinator -- as every tab does via the app's singleton registration -- can
    /// never both be audibly playing at once.
    /// </summary>
    public sealed class PlaybackViewModelTests
    {
        [Fact]
        public void SecondTabPlaying_StopsTheFirstTabThroughTheSharedCoordinator()
        {
            var coordinator = new PlaybackCoordinator();
            var tabA = CreatePlaybackViewModel(coordinator);
            var tabB = CreatePlaybackViewModel(coordinator);

            tabA.Play();
            Assert.True(tabA.IsPlaying);

            tabB.Play();

            Assert.True(tabB.IsPlaying);
            Assert.False(tabA.IsPlaying);
        }

        [Fact]
        public void StoppingOneTab_DoesNotAffectAnotherTabThatStartedPlayingAfterwards()
        {
            var coordinator = new PlaybackCoordinator();
            var tabA = CreatePlaybackViewModel(coordinator);
            var tabB = CreatePlaybackViewModel(coordinator);

            tabA.Play();
            tabB.Play(); // auto-stops tabA via the coordinator
            tabA.Stop(); // already stopped; must be a no-op, not disturb tabB

            Assert.True(tabB.IsPlaying);
        }

        [Fact]
        public void DisposingAPlayingTab_LetsAnotherTabStartWithoutError()
        {
            var coordinator = new PlaybackCoordinator();
            var tabA = CreatePlaybackViewModel(coordinator);
            var tabB = CreatePlaybackViewModel(coordinator);

            tabA.Play();
            tabA.Dispose(); // simulates closing a still-playing tab

            tabB.Play();

            Assert.True(tabB.IsPlaying);
        }

        private static PlaybackViewModel CreatePlaybackViewModel(PlaybackCoordinator coordinator)
        {
            var messenger = ViewModelTestHelper.CreateMessenger();
            var document = ViewModelTestHelper.CreateDocument(messenger);
            return new PlaybackViewModel(document, new FakePlaybackService(), messenger, coordinator);
        }
    }
}
