using System.Collections.Generic;

namespace JianpuEditor.Models
{
    /// <summary>The persisted (session.json) and restored shape of the app's open tabs -- see
    /// JianpuEditor.Core.Abstractions.ISessionService.</summary>
    public sealed class SessionState
    {
        public List<SessionTabState> Tabs { get; set; } = new List<SessionTabState>();

        public int ActiveTabIndex { get; set; }
    }
}
