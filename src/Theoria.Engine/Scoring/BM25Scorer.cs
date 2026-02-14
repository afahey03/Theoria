using Theoria.Shared.Interfaces;

namespace Theoria.Engine.Scoring;

/// <summary>
/// Implements the Okapi BM25 ranking function.
///
/// BM25 is a probabilistic retrieval model that scores documents based on
/// term frequency (TF), inverse document frequency (IDF), and document
/// length normalization. It is the industry standard for full-text search.
///
/// Formula per query term t in document d:
///   score(t, d) = IDF(t) * (tf * (k1 + 1)) / (tf + k1 * (1 - b + b * dl / avgdl))
///
/// Where:
///   tf    = term frequency in document d
///   dl    = document length (token count)
///   avgdl = average document length across the corpus
///   k1    = term-frequency saturation parameter (default 1.2)
///   b     = length normalization parameter (default 0.75)
///   IDF   = log((N - n + 0.5) / (n + 0.5) + 1)
///   N     = total documents, n = documents containing t
/// </summary>
public sealed class BM25Scorer : IScorer
{
    private readonly Indexing.InvertedIndex _index;

    /// <summary>
    /// Controls how quickly term frequency saturates.
    /// Higher values give more weight to repeated terms.
    /// </summary>
    public double K1 { get; init; } = 1.2;

    /// <summary>
    /// Controls document-length normalization.
    /// 0 = no normalization, 1 = full normalization.
    /// </summary>
    public double B { get; init; } = 0.75;

    public BM25Scorer(Indexing.InvertedIndex index)
    {
        _index = index ?? throw new ArgumentNullException(nameof(index));
    }

    /// <inheritdoc />
    public double Score(IReadOnlyList<string> queryTerms, string docId)
    {
        double score = 0.0;
        int totalDocs = _index.DocumentCount;
        double avgDl = _index.AverageDocumentLength;
        int docLength = _index.GetDocumentLength(docId);

        if (totalDocs == 0 || avgDl == 0) return 0;

        foreach (var term in queryTerms)
        {
            int docFreq = _index.GetDocumentFrequency(term);
            if (docFreq == 0) continue;

            // O(1) lookup instead of O(n) scan through postings list
            var posting = _index.GetPosting(term, docId);
            if (posting is null) continue;

            int tf = posting.TermFrequency;

            // IDF component: log((N - n + 0.5) / (n + 0.5) + 1)
            double idf = Math.Log((totalDocs - docFreq + 0.5) / (docFreq + 0.5) + 1.0);

            // TF component with length normalization
            double tfNorm = (tf * (K1 + 1.0)) /
                            (tf + K1 * (1.0 - B + B * (docLength / avgDl)));

            score += idf * tfNorm;
        }

        return score;
    }
}
