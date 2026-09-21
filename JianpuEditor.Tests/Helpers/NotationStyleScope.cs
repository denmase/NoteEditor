using System;
using JianpuEditor.Models;
using JianpuEditor.Rendering;

namespace JianpuEditor.Tests.Helpers
{
    /// <summary>Temporarily overrides AppTheme.NotationStyle (global static state) for the
    /// duration of a test, restoring the previous value on dispose so tests don't leak state into
    /// each other.</summary>
    internal sealed class NotationStyleScope : IDisposable
    {
        private readonly NotationStyle _previous;

        public NotationStyleScope(NotationStyle style)
        {
            _previous = AppTheme.NotationStyle;
            AppTheme.SetNotationStyle(style, persist: false);
        }

        public void Dispose()
        {
            AppTheme.SetNotationStyle(_previous, persist: false);
        }
    }
}
