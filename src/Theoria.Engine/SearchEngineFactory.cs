using Theoria.Engine.Indexing;
using Theoria.Engine.Scoring;
using Theoria.Engine.Tokenization;
using Theoria.Shared.Interfaces;

namespace Theoria.Engine;

/// <summary>
/// Factory that wires up the default search engine components.
/// Provides a single entry point for both the desktop and API hosts.
/// </summary>
public static class SearchEngineFactory
{
    /// <summary>
    /// Creates a fully configured <see cref="TheoriaSearchEngine"/> with the
    /// default tokenizer, inverted index, and BM25 scorer.
    /// </summary>
    /// <param name="storagePath">
    /// Directory on disk for index persistence. Pass null for in-memory only.
    /// </param>
    /// <param name="k1">BM25 k1 parameter (term-frequency saturation). Default: 1.2.</param>
    /// <param name="b">BM25 b parameter (document-length normalization). Default: 0.75.</param>
    public static TheoriaSearchEngine Create(
        string? storagePath = null,
        double k1 = 1.2,
        double b = 0.75)
    {
        ITokenizer tokenizer = new SimpleTokenizer();
        var invertedIndex = new InvertedIndex(tokenizer);
        IScorer scorer = new BM25Scorer(invertedIndex) { K1 = k1, B = b };

        return new TheoriaSearchEngine(tokenizer, invertedIndex, scorer, storagePath);
    }
}
