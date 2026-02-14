namespace Theoria.Shared.Models;

/// <summary>
/// The complete result set returned by a search operation.
/// Wraps the list of items with metadata about the search itself.
/// </summary>
public sealed class SearchResult
{
    /// <summary>The query that produced these results.</summary>
    public required string Query { get; init; }

    /// <summary>Total number of matching documents (before TopN truncation).</summary>
    public int TotalMatches { get; init; }

    /// <summary>Time taken to execute the search, in milliseconds.</summary>
    public double ElapsedMilliseconds { get; init; }

    /// <summary>Ranked list of result items.</summary>
    public required IReadOnlyList<SearchResultItem> Items { get; init; }
}
