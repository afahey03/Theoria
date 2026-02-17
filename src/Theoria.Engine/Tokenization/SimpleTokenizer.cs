using System.Text.RegularExpressions;
using Theoria.Shared.Interfaces;

namespace Theoria.Engine.Tokenization;

/// <summary>
/// Default tokenizer that performs:
///   1. Lowercase normalization
///   2. Punctuation stripping (keeps alphanumeric and hyphens)
///   3. Stop-word removal (common English words)
///   4. Porter stemming (theology → theolog, philosophical → philosoph)
///
/// Stemming dramatically improves recall: a search for "theology" now
/// matches documents containing "theological", "theologians", "theologies", etc.
///
/// This ensures deterministic results across desktop and web clients,
/// because both share the exact same tokenizer implementation.
/// </summary>
public sealed partial class SimpleTokenizer : ITokenizer
{
    // Compile once — matches one or more non-letter/non-digit characters.
    [GeneratedRegex(@"[^a-z0-9\-]+", RegexOptions.Compiled)]
    private static partial Regex SplitPattern();

    /// <summary>
    /// A standard set of English stop words that carry little search value.
    /// Removing them shrinks the index and improves relevance.
    /// </summary>
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "and", "or", "not", "but", "in", "on", "at",
        "to", "for", "of", "with", "by", "from", "is", "it", "as",
        "be", "was", "were", "been", "are", "have", "has", "had",
        "do", "does", "did", "will", "would", "could", "should",
        "may", "might", "shall", "can", "this", "that", "these",
        "those", "i", "you", "he", "she", "we", "they", "me",
        "him", "her", "us", "them", "my", "your", "his", "its",
        "our", "their", "what", "which", "who", "whom", "how",
        "when", "where", "why", "if", "then", "so", "no", "yes",
        "about", "up", "out", "just", "into", "over", "after"
    };

    /// <inheritdoc />
    public IReadOnlyList<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var lower = text.ToLowerInvariant();
        var parts = SplitPattern().Split(lower);

        var tokens = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            if (part.Length > 0 && !StopWords.Contains(part))
            {
                tokens.Add(PorterStemmer.Stem(part));
            }
        }

        return tokens;
    }
}
