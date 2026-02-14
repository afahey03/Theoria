namespace Theoria.Shared.Interfaces;

/// <summary>
/// Computes relevance scores for documents against a set of query terms.
/// The default implementation uses the BM25 ranking function.
/// </summary>
public interface IScorer
{
    /// <summary>
    /// Computes the BM25 (or equivalent) score for a single document
    /// given the query terms.
    /// </summary>
    /// <param name="queryTerms">Normalized query tokens.</param>
    /// <param name="docId">The document being scored.</param>
    /// <returns>A relevance score (higher = more relevant).</returns>
    double Score(IReadOnlyList<string> queryTerms, string docId);
}
