using JianpuEditor.Models;

namespace JianpuEditor.Core.Abstractions
{
    /// <summary>
    /// Optional capability of an <see cref="IHarmonySuggestionService"/> that offers more than one
    /// suggestion backend. Cast to this interface to expose an engine picker in the UI; a service
    /// backed by only one backend simply doesn't implement it, and callers should treat that as
    /// "no picker available" rather than an error.
    /// </summary>
    public interface IHarmonySuggestionEngineOptions
    {
        HarmonySuggestionEngineKind ActiveKind { get; set; }
    }
}
