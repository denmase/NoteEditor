namespace JianpuEditor.Models
{
    /// <summary>One currently-open tab's state, as JianpuEditor.Core.Abstractions.ISessionService
    /// needs it to decide whether to write a fresh autosave file for it when saving the
    /// session.</summary>
    public sealed class SessionTabSnapshot
    {
        public string FilePath { get; set; }

        public bool IsDirty { get; set; }

        public JianpuScore Score { get; set; }
    }
}
