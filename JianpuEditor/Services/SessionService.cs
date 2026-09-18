using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JianpuEditor.Core.Abstractions;
using JianpuEditor.Models;
using Newtonsoft.Json;

namespace JianpuEditor.Services
{
    public sealed class SessionService : ISessionService
    {
        private readonly IScoreFileService _fileService;
        private readonly string _sessionPath;
        private readonly string _autosaveDirectory;

        /// <param name="baseDirectory">Where session.json and the autosave/ folder live. Omit to
        /// use the real portable-vs-installed location (see <see cref="ResolveBaseDirectory"/>);
        /// tests pass an isolated temp directory instead.</param>
        public SessionService(IScoreFileService fileService, string baseDirectory = null)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));

            baseDirectory = baseDirectory ?? ResolveBaseDirectory();
            _sessionPath = Path.Combine(baseDirectory, "session.json");
            _autosaveDirectory = Path.Combine(baseDirectory, "autosave");
        }

        // Mirrors AppTheme.ResolveSettingsPath's portable-vs-installed convention: a portable
        // build drops a marker file next to the exe so state stays inside the portable folder
        // instead of the per-user profile.
        private static string ResolveBaseDirectory()
        {
            var exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var portableMarker = Path.Combine(exeDirectory, "portable.txt");
            if (File.Exists(portableMarker))
            {
                return exeDirectory;
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "JianpuEditor");
        }

        public SessionState Load()
        {
            try
            {
                if (!File.Exists(_sessionPath))
                {
                    return new SessionState();
                }

                var json = File.ReadAllText(_sessionPath);
                return JsonConvert.DeserializeObject<SessionState>(json) ?? new SessionState();
            }
            catch (Exception ex)
            {
                AppLog.Exception("Failed to load session.json", ex);
                return new SessionState();
            }
        }

        public void Save(IReadOnlyList<SessionTabSnapshot> tabs, int activeTabIndex)
        {
            if (tabs == null)
            {
                throw new ArgumentNullException(nameof(tabs));
            }

            try
            {
                Directory.CreateDirectory(_autosaveDirectory);

                var state = new SessionState { ActiveTabIndex = activeTabIndex };
                foreach (var tab in tabs)
                {
                    var entry = new SessionTabState { FilePath = tab.FilePath, IsDirty = tab.IsDirty };
                    if (tab.IsDirty)
                    {
                        entry.AutosavePath = Path.Combine(_autosaveDirectory, Guid.NewGuid().ToString("N") + ".jianpu");
                        _fileService.Save(tab.Score, entry.AutosavePath);
                    }

                    state.Tabs.Add(entry);
                }

                var json = JsonConvert.SerializeObject(state, Formatting.Indented);
                File.WriteAllText(_sessionPath, json);

                DeleteUnreferencedAutosaveFiles(state);
            }
            catch (Exception ex)
            {
                AppLog.Exception("Failed to save session.json", ex);
            }
        }

        private void DeleteUnreferencedAutosaveFiles(SessionState state)
        {
            if (!Directory.Exists(_autosaveDirectory))
            {
                return;
            }

            var referenced = new HashSet<string>(
                state.Tabs
                    .Where(tab => !string.IsNullOrEmpty(tab.AutosavePath))
                    .Select(tab => tab.AutosavePath),
                StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.GetFiles(_autosaveDirectory))
            {
                if (referenced.Contains(file))
                {
                    continue;
                }

                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    AppLog.Exception("Failed to delete stale autosave file: " + file, ex);
                }
            }
        }
    }
}
