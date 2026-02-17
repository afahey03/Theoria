using Theoria.Shared.Enums;

namespace Theoria.Shared.Models;

/// <summary>
/// A single item within a search result set.
/// Contains everything the UI needs to render one result row.
/// </summary>
public sealed class SearchResultItem
{
    /// <summary>Document title.</summary>
    public required string Title { get; init; }

    /// <summary>URL or file path of the original document.</summary>
    public string? Url { get; init; }

    /// <summary>
    /// Short text snippet surrounding the matched terms,
    /// with highlight markers for the UI to render.
    /// </summary>
    public required string Snippet { get; init; }

    /// <summary>BM25 relevance score.</summary>
    public double Score { get; init; }

    /// <summary>The type of source document.</summary>
    public ContentType SourceType { get; init; }

    /// <summary>
    /// Whether this result comes from a known scholarly / academic domain
    /// (journals, university repositories, digital libraries, etc.).
    /// UIs can use this to show an "Academic" badge.
    /// </summary>
    public bool IsScholarly { get; init; }

    /// <summary>
    /// The display domain of the source (e.g., "jstor.org", "plato.stanford.edu").
    /// Useful for showing a source label in the UI.
    /// </summary>
    public string? Domain { get; init; }
}
