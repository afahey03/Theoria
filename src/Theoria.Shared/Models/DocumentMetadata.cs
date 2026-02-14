using Theoria.Shared.Enums;

namespace Theoria.Shared.Models;

/// <summary>
/// Metadata describing an indexed document.
/// Stored alongside index data so documents can be identified and updated.
/// </summary>
public sealed class DocumentMetadata
{
    /// <summary>Unique identifier for the document within the index.</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable title of the document.</summary>
    public required string Title { get; init; }

    /// <summary>URL or file path where the original content can be found.</summary>
    public string? Url { get; init; }

    /// <summary>The type of content (PDF, Markdown, HTML).</summary>
    public ContentType ContentType { get; init; }

    /// <summary>Local file system path, if the document was indexed from disk.</summary>
    public string? Path { get; init; }

    /// <summary>Timestamp of the most recent indexing operation for this document.</summary>
    public DateTime LastIndexedAt { get; init; } = DateTime.UtcNow;
}
