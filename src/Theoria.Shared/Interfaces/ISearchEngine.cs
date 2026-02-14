using Theoria.Shared.Models;

namespace Theoria.Shared.Interfaces;

/// <summary>
/// The top-level search engine contract.
/// Consumed by both the web API and the desktop client so the same engine
/// can run in-process (desktop) or behind HTTP (web).
/// </summary>
public interface ISearchEngine
{
    /// <summary>
    /// Executes a search query and returns ranked results.
    /// </summary>
    /// <param name="request">The search request containing query and options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="SearchResult"/> containing matched documents.</returns>
    Task<SearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);
}
