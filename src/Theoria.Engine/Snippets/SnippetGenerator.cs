using System.Text;
using System.Text.RegularExpressions;

namespace Theoria.Engine.Snippets;

/// <summary>
/// Generates short text snippets from document content with matched terms highlighted.
///
/// Strategy (best-window algorithm):
///   1. Find ALL occurrences of each query term in the raw text.
///   2. Slide a window over the text, scoring each position by how many
///      distinct query terms it covers and how many total hits it contains.
///   3. Pick the window with the highest score (= densest coverage of terms).
///   4. Snap window edges to word boundaries for readability.
///   5. Highlight matched terms with &lt;mark&gt; tags.
/// </summary>
public static class SnippetGenerator
{
    /// <summary>Default snippet window size in characters.</summary>
    private const int WindowSize = 280;

    /// <summary>How far apart to step when sliding the window.</summary>
    private const int StepSize = 40;

    /// <summary>Highlight open tag.</summary>
    private const string HighlightOpen = "<mark>";

    /// <summary>Highlight close tag.</summary>
    private const string HighlightClose = "</mark>";

    /// <summary>
    /// Generates a highlighted snippet from the given content for the specified query terms.
    /// Uses a sliding-window approach with sorted hits and binary search for the best term coverage.
    /// </summary>
    public static string Generate(string content, IReadOnlyList<string> queryTerms)
    {
        if (string.IsNullOrWhiteSpace(content) || queryTerms.Count == 0)
            return content?.Length > WindowSize ? content[..WindowSize] + "..." : content ?? string.Empty;

        // Build a list of (position, termIndex) for every hit in the content
        var hits = new List<(int Position, int TermIndex)>();
        for (int t = 0; t < queryTerms.Count; t++)
        {
            var term = queryTerms[t];
            int searchFrom = 0;
            while (searchFrom < content.Length)
            {
                int idx = content.IndexOf(term, searchFrom, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) break;
                hits.Add((idx, t));
                searchFrom = idx + 1;
            }
        }

        // If no hits at all, return the beginning of the document
        if (hits.Count == 0)
            return content.Length > WindowSize ? content[..WindowSize] + "..." : content;

        // Sort hits by position for binary search and early exit in window scanning
        hits.Sort((a, b) => a.Position.CompareTo(b.Position));

        // Slide window across the text and score each position
        // Use binary search to find the first hit in each window — O(log n) instead of O(n)
        int bestStart = 0;
        double bestScore = -1;
        int hitCount = hits.Count;

        for (int pos = 0; pos < content.Length; pos += StepSize)
        {
            int winEnd = pos + WindowSize;

            // Binary search for the first hit >= pos
            int lo = 0, hi = hitCount;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (hits[mid].Position < pos) lo = mid + 1;
                else hi = mid;
            }

            // Count hits within the window, early-exit when past window end
            var distinctTerms = new HashSet<int>();
            int totalHits = 0;
            for (int j = lo; j < hitCount && hits[j].Position < winEnd; j++)
            {
                distinctTerms.Add(hits[j].TermIndex);
                totalHits++;
            }

            // Score: prioritize covering more distinct terms, then total density
            double score = distinctTerms.Count * 1000.0 + totalHits;
            if (score > bestScore)
            {
                bestScore = score;
                bestStart = pos;
            }
        }

        // Snap to word boundaries
        int start = Math.Max(0, bestStart);
        int end = Math.Min(content.Length, start + WindowSize);

        if (start > 0)
        {
            int space = content.IndexOf(' ', start);
            if (space >= 0 && space < start + 30)
                start = space + 1;
        }
        if (end < content.Length)
        {
            int space = content.LastIndexOf(' ', end - 1, Math.Min(end, 30));
            if (space > start)
                end = space;
        }

        var snippet = content[start..end];

        // Build output with ellipsis and highlights
        var sb = new StringBuilder();
        if (start > 0) sb.Append("...");

        var highlighted = snippet;
        // Single combined regex for all terms — one pass instead of N passes
        var termPatterns = queryTerms
            .Select(t => Regex.Escape(t) + @"\w*")
            .Distinct();
        var combinedPattern = $"({string.Join("|", termPatterns)})";
        highlighted = Regex.Replace(
            highlighted,
            combinedPattern,
            $"{HighlightOpen}$1{HighlightClose}",
            RegexOptions.IgnoreCase,
            TimeSpan.FromMilliseconds(100));

        sb.Append(highlighted);
        if (end < content.Length) sb.Append("...");

        return sb.ToString();
    }
}
