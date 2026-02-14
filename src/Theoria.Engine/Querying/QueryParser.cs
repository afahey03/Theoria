using System.Text.RegularExpressions;
using Theoria.Shared.Interfaces;

namespace Theoria.Engine.Querying;

/// <summary>
/// Parses a raw query string into a <see cref="ParsedQuery"/>.
///
/// Supported syntax:
///   - Simple words:   "aquinas virtue"  → all terms required (AND)
///   - Quoted phrases: '"natural law"'   → exact phrase match
///   - OR operator:    "faith OR reason" → either term acceptable
///   - AND operator:   "faith AND hope"  → both required (default behavior)
///
/// The parser tokenizes the query using the same ITokenizer as the indexer
/// to ensure identical normalization.
/// </summary>
public sealed partial class QueryParser
{
    private readonly ITokenizer _tokenizer;

    [GeneratedRegex("\"([^\"]+)\"", RegexOptions.Compiled)]
    private static partial Regex PhraseRegex();

    public QueryParser(ITokenizer tokenizer)
    {
        _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
    }

    /// <summary>
    /// Parses a raw query string into structured form.
    /// </summary>
    public ParsedQuery Parse(string rawQuery)
    {
        if (string.IsNullOrWhiteSpace(rawQuery))
            return new ParsedQuery();

        var result = new ParsedQuery();

        // 1. Extract quoted phrases first
        var remaining = rawQuery;

        foreach (Match match in PhraseRegex().Matches(rawQuery))
        {
            var phraseTokens = _tokenizer.Tokenize(match.Groups[1].Value);
            if (phraseTokens.Count > 0)
            {
                result.Phrases.Add(phraseTokens.ToList());
            }
            remaining = remaining.Replace(match.Value, " ");
        }

        // 2. Split remaining text on whitespace and process operators
        var parts = remaining.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        bool nextIsOr = false;

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];

            // Skip boolean operators (case-insensitive)
            if (part.Equals("AND", StringComparison.OrdinalIgnoreCase))
                continue;

            if (part.Equals("OR", StringComparison.OrdinalIgnoreCase))
            {
                nextIsOr = true;
                continue;
            }

            var tokens = _tokenizer.Tokenize(part);
            foreach (var token in tokens)
            {
                if (nextIsOr)
                {
                    result.OptionalTerms.Add(token);
                    nextIsOr = false;
                }
                else
                {
                    result.RequiredTerms.Add(token);
                }
            }
        }

        return result;
    }
}
