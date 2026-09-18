using System.Collections.Generic;
using JianpuEditor.Models;

namespace JianpuEditor.Core.Abstractions
{
    /// <summary>
    /// Persists which tabs were open -- including unsaved content, via autosave copies -- so the
    /// app can restore them on next launch. Follows the same portable-vs-installed path convention
    /// as JianpuEditor.Rendering.AppTheme.ResolveSettingsPath.
    /// </summary>
    public interface ISessionService
    {
        /// <summary>Reads the persisted session, or an empty <see cref="SessionState"/> (no tabs)
        /// if none exists or it can't be read.</summary>
        SessionState Load();

        /// <summary>Persists the current set of open tabs. Writes a fresh autosave file for each
        /// dirty tab's current in-memory score and deletes any autosave file from a previous
        /// session that's no longer referenced.</summary>
        void Save(IReadOnlyList<SessionTabSnapshot> tabs, int activeTabIndex);
    }
}
