using System;
using System.IO;
using JianpuEditor.Models;
using JianpuEditor.Services;
using Xunit;

namespace JianpuEditor.Tests.Services
{
    public sealed class SessionServiceTests
    {
        [Fact]
        public void Load_WithNoPersistedSession_ReturnsEmptyState()
        {
            using var sandbox = new TempDirectory();
            var service = new SessionService(new ScoreFileServiceAdapter(), sandbox.Path);

            var state = service.Load();

            Assert.Empty(state.Tabs);
        }

        [Fact]
        public void SaveAndLoad_RoundTripsACleanTabWithNoAutosaveFile()
        {
            using var sandbox = new TempDirectory();
            var service = new SessionService(new ScoreFileServiceAdapter(), sandbox.Path);
            var tabs = new[] { new SessionTabSnapshot { FilePath = @"C:\Songs\Clean.jianpu", IsDirty = false } };

            service.Save(tabs, activeTabIndex: 0);
            var loaded = service.Load();

            var tab = Assert.Single(loaded.Tabs);
            Assert.Equal(@"C:\Songs\Clean.jianpu", tab.FilePath);
            Assert.False(tab.IsDirty);
            Assert.Null(tab.AutosavePath);
            Assert.Equal(0, loaded.ActiveTabIndex);
        }

        [Fact]
        public void Save_WritesAnAutosaveFileForADirtyTab_RestorableViaTheReturnedPath()
        {
            using var sandbox = new TempDirectory();
            var service = new SessionService(new ScoreFileServiceAdapter(), sandbox.Path);
            var score = new JianpuScore { Title = "Unsaved Work" };
            var tabs = new[] { new SessionTabSnapshot { FilePath = null, IsDirty = true, Score = score } };

            service.Save(tabs, activeTabIndex: 0);
            var loaded = service.Load();

            var tab = Assert.Single(loaded.Tabs);
            Assert.True(tab.IsDirty);
            Assert.False(string.IsNullOrEmpty(tab.AutosavePath));
            Assert.True(File.Exists(tab.AutosavePath));
            var recovered = ScoreFileService.Load(tab.AutosavePath);
            Assert.Equal("Unsaved Work", recovered.Title);
        }

        [Fact]
        public void Save_DeletesAutosaveFilesNoLongerReferencedByTheNewSession()
        {
            using var sandbox = new TempDirectory();
            var service = new SessionService(new ScoreFileServiceAdapter(), sandbox.Path);

            // First save: one dirty tab gets an autosave file.
            service.Save(
                new[] { new SessionTabSnapshot { FilePath = null, IsDirty = true, Score = new JianpuScore() } },
                activeTabIndex: 0);
            var firstAutosavePath = Assert.Single(service.Load().Tabs).AutosavePath;
            Assert.True(File.Exists(firstAutosavePath));

            // Second save: that tab is gone (e.g. closed or saved-and-cleaned), replaced by a
            // clean one with no autosave file of its own.
            service.Save(
                new[] { new SessionTabSnapshot { FilePath = @"C:\Songs\NowClean.jianpu", IsDirty = false } },
                activeTabIndex: 0);

            Assert.False(File.Exists(firstAutosavePath));
        }

        [Fact]
        public void Save_RepeatedlyWithTheSameDirtyTabCount_NeverLeavesExtraOrMissingAutosaveFiles()
        {
            using var sandbox = new TempDirectory();
            var service = new SessionService(new ScoreFileServiceAdapter(), sandbox.Path);
            var autosaveDirectory = Path.Combine(sandbox.Path, "autosave");
            Func<SessionTabSnapshot[]> makeTwoDirtyTabs = () => new[]
            {
                new SessionTabSnapshot { FilePath = null, IsDirty = true, Score = new JianpuScore() },
                new SessionTabSnapshot { FilePath = @"C:\Songs\StillDirty.jianpu", IsDirty = true, Score = new JianpuScore() }
            };

            service.Save(makeTwoDirtyTabs(), activeTabIndex: 0);
            service.Save(makeTwoDirtyTabs(), activeTabIndex: 1);

            // Each Save writes fresh autosave files for the still-dirty tabs and cleans up the
            // previous call's now-unreferenced ones -- exactly 2 should remain, not 4 (leaked) or
            // 0 (deleted too early).
            Assert.Equal(2, Directory.GetFiles(autosaveDirectory).Length);
        }

        private sealed class TempDirectory : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jianpu-session-tests-" + Guid.NewGuid());

            public void Dispose()
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
        }
    }
}
