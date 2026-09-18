using System.Collections.Generic;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Models;

namespace JianpuEditor.Tests.Services
{
    internal sealed class FakeSessionService : ISessionService
    {
        public SessionState SessionToLoad { get; set; } = new SessionState();

        public IReadOnlyList<SessionTabSnapshot> SavedTabs { get; private set; }

        public int SavedActiveTabIndex { get; private set; }

        public int SaveCallCount { get; private set; }

        public SessionState Load()
        {
            return SessionToLoad;
        }

        public void Save(IReadOnlyList<SessionTabSnapshot> tabs, int activeTabIndex)
        {
            SavedTabs = tabs;
            SavedActiveTabIndex = activeTabIndex;
            SaveCallCount++;
        }
    }
}
