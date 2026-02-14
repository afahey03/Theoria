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
}
