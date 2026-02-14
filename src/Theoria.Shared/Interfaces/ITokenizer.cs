namespace Theoria.Shared.Interfaces;

/// <summary>
/// Breaks raw text into a sequence of normalized tokens.
/// Implementations handle case folding, punctuation stripping,
/// stop-word removal, and optional stemming.
/// </summary>
public interface ITokenizer
{
    /// <summary>
    /// Tokenizes the given text into a list of normalized terms.
    /// </summary>
    /// <param name="text">Raw input text.</param>
    /// <returns>Ordered list of tokens ready for indexing or querying.</returns>
    IReadOnlyList<string> Tokenize(string text);
}
