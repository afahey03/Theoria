using Theoria.Shared.Models;

namespace Theoria.Shared.Interfaces;

/// <summary>
/// Adds documents to the search index.
/// Supports incremental indexing and document updates.
/// </summary>
public interface IIndexer
{
    /// <summary>
    /// Indexes a single document's content, associating it with the given metadata.
    /// If a document with the same Id already exists, it is re-indexed (updated).
    /// </summary>
    /// <param name="metadata">Metadata describing the document.</param>
    /// <param name="content">The full text content of the document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IndexDocumentAsync(DocumentMetadata metadata, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a document from the index.
    /// </summary>
    /// <param name="documentId">The unique Id of the document to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveDocumentAsync(string documentId, CancellationToken cancellationToken = default);
}
