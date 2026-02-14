namespace Theoria.Engine.Querying;

/// <summary>
/// Represents a parsed search query with structured components.
/// Supports AND, OR operators and exact phrase matching.
/// </summary>
public sealed class ParsedQuery
{
    /// <summary>Terms that MUST appear in matching documents (AND logic).</summary>
    public List<string> RequiredTerms { get; init; } = [];

    /// <summary>Terms where at least one should appear (OR logic).</summary>
    public List<string> OptionalTerms { get; init; } = [];

    /// <summary>
    /// Exact phrase sequences. Each inner list is the ordered tokens
    /// that must appear consecutively in the document.
    /// </summary>
    public List<List<string>> Phrases { get; init; } = [];

    /// <summary>Whether this query has any actionable terms.</summary>
    public bool IsEmpty => RequiredTerms.Count == 0
                        && OptionalTerms.Count == 0
                        && Phrases.Count == 0;
}
