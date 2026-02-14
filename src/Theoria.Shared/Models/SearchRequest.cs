namespace Theoria.Shared.Models;

/// <summary>
/// Represents an incoming search request from the client.
/// Encapsulates query text and pagination/ranking options.
/// </summary>
public sealed class SearchRequest
{
    /// <summary>The raw query string entered by the user.</summary>
    public required string Query { get; init; }

    /// <summary>Maximum number of results to return. Defaults to 10.</summary>
    public int TopN { get; init; } = 10;

    /// <summary>Optional filter to restrict results to a specific content type.</summary>
    public Enums.ContentType? ContentTypeFilter { get; init; }
}
