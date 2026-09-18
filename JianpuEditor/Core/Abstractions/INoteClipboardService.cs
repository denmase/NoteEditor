using System.Collections.Generic;
using JianpuEditor.Models;

namespace JianpuEditor.Core.Abstractions
{
    /// <summary>
    /// In-app clipboard for melody notes, shared across every open tab (a genuine app-wide
    /// resource, like <see cref="IMidiOutput"/>) so cut/copy in one tab can be pasted into
    /// another. Not backed by the OS clipboard.
    /// </summary>
    public interface INoteClipboardService
    {
        bool HasNotes { get; }

        /// <summary>Stores a deep copy of <paramref name="notes"/>; later mutations to the
        /// source notes (or to whatever <see cref="GetNotes"/> previously returned) do not
        /// affect what's stored.</summary>
        void SetNotes(IReadOnlyList<JianpuNote> notes);

        /// <summary>Returns a fresh deep copy each call, safe to insert directly into a score.</summary>
        IReadOnlyList<JianpuNote> GetNotes();
    }
}
