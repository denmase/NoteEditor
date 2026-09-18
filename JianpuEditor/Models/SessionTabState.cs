namespace JianpuEditor.Models
{
    /// <summary>
    /// One tab's entry in session.json (see <see cref="SessionState"/>). A dirty tab keeps its
    /// original <see cref="FilePath"/> (possibly null, for a document never saved) so a later Save
    /// still writes back to the right place, alongside an <see cref="AutosavePath"/> holding its
    /// actual unsaved content; a clean tab has only <see cref="FilePath"/>.
    /// </summary>
    public sealed class SessionTabState
    {
        public string FilePath { get; set; }

        public string AutosavePath { get; set; }

        public bool IsDirty { get; set; }
    }
}
