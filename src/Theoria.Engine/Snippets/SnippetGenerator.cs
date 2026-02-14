using System.Text;
using System.Text.RegularExpressions;

namespace Theoria.Engine.Snippets;

/// <summary>
/// Generates short text snippets from document content with matched terms highlighted.
///
/// Strategy:
///   1. Find the first occurrence of any query term in the raw text.
///   2. Extract a window of text around that position.
///   3. Wrap matched terms with highlight markers (e.g., <mark>...</mark>)
///      so the UI can render them visually.
/// </summary>
public static partial class SnippetGenerator
{
    /// <summary>Default snippet window size in characters.</summary>
    private const int WindowSize = 200;

    /// <summary>Highlight open tag.</summary>
    private const string HighlightOpen = "<mark>";

    /// <summary>Highlight close tag.</summary>
    private const string HighlightClose = "</mark>";

    [GeneratedRegex(@"\b", RegexOptions.Compiled)]
    private static partial Regex WordBoundary();

    /// <summary>
    /// Generates a highlighted snippet from the given content for the specified query terms.
    /// </summary>
    /// <param name="content">The full text of the document.</param>
    /// <param name="queryTerms">Normalized query terms to highlight.</param>
    /// <returns>A string snippet with highlight markers around matched terms.</returns>
    public static string Generate(string content, IReadOnlyList<string> queryTerms)
    {
        if (string.IsNullOrWhiteSpace(content) || queryTerms.Count == 0)
            return content?.Length > WindowSize ? content[..WindowSize] + "..." : content ?? string.Empty;

        // Find the position of the first matching term in the raw text
        int bestPos = content.Length; // worst case: end
        foreach (var term in queryTerms)
        {
            int idx = content.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && idx < bestPos)
                bestPos = idx;
        }

        // Build window around the best match position
        int start = Math.Max(0, bestPos - WindowSize / 4);
        int end = Math.Min(content.Length, start + WindowSize);

        // Adjust start to the nearest word boundary
        if (start > 0)
        {
            int space = content.IndexOf(' ', start);
            if (space >= 0 && space < start + 30)
                start = space + 1;
        }

        var snippet = content[start..end];

        // Add ellipsis indicators
        var sb = new StringBuilder();
        if (start > 0) sb.Append("...");

        // Highlight query terms in the snippet (case-insensitive replacement)
        var highlighted = snippet;
        foreach (var term in queryTerms)
        {
            highlighted = Regex.Replace(
                highlighted,
                @$"\b({Regex.Escape(term)})\b",
                $"{HighlightOpen}$1{HighlightClose}",
                RegexOptions.IgnoreCase);
        }

        sb.Append(highlighted);
        if (end < content.Length) sb.Append("...");

        return sb.ToString();
    }
}
