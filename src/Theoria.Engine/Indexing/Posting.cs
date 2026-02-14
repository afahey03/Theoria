namespace Theoria.Engine.Indexing;

/// <summary>
/// A single entry in a term's postings list.
/// Records the document a term appears in, how many times, and at which positions.
/// Positions enable phrase search and proximity queries.
/// </summary>
public sealed class Posting
{
    /// <summary>The document this posting refers to.</summary>
    public required string DocId { get; init; }

    /// <summary>Number of times the term appears in the document.</summary>
    public int TermFrequency { get; set; }

    /// <summary>
    /// Zero-based token positions where the term occurs.
    /// Used for phrase matching and snippet generation.
    /// </summary>
    public List<int> Positions { get; init; } = [];
}
