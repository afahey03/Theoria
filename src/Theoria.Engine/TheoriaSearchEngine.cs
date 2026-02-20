using System.Diagnostics;
using Theoria.Engine.Indexing;
using Theoria.Engine.Querying;
using Theoria.Engine.Scoring;
using Theoria.Engine.Snippets;
using Theoria.Engine.Storage;
using Theoria.Shared.Interfaces;
using Theoria.Shared.Models;

namespace Theoria.Engine;

/// <summary>
/// The main search engine implementation.
///
/// This is the single shared core used by both the desktop client (in-process)
/// and the web API (behind HTTP). Both pathways produce identical, deterministic
/// results because they share the same tokenizer, index, and scorer.
///
/// Responsibilities:
///   - Orchestrates indexing (tokenize → add to inverted index → persist)
///   - Orchestrates search  (parse query → score docs → generate snippets → rank)
///   - Manages index persistence (save/load on startup)
/// </summary>
public sealed class TheoriaSearchEngine : ISearchEngine, IIndexer, IDisposable
{
    private readonly InvertedIndex _invertedIndex;
    private readonly ITokenizer _tokenizer;
    private readonly IScorer _scorer;
    private readonly QueryParser _queryParser;
    private readonly IndexStorage? _storage;
    private readonly Timer? _debounceSaveTimer;
    private static readonly TimeSpan SaveDebounceInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Creates a new search engine instance.
    /// </summary>
    /// <param name="tokenizer">Tokenizer for text normalization.</param>
    /// <param name="invertedIndex">The inverted index to store/query.</param>
    /// <param name="scorer">The scoring function (BM25).</param>
    /// <param name="storagePath">
    /// Optional path on disk where the index is persisted.
    /// If null, the index is only held in memory.
    /// </param>
    public TheoriaSearchEngine(
        ITokenizer tokenizer,
        InvertedIndex invertedIndex,
        IScorer scorer,
        string? storagePath = null)
    {
        _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
        _invertedIndex = invertedIndex ?? throw new ArgumentNullException(nameof(invertedIndex));
        _scorer = scorer ?? throw new ArgumentNullException(nameof(scorer));
        _queryParser = new QueryParser(_tokenizer);

        if (storagePath is not null)
        {
            _storage = new IndexStorage(storagePath);
            _debounceSaveTimer = new Timer(DebouncedSaveCallback, null, Timeout.Infinite, Timeout.Infinite);
        }
    }

    // ----- ISearchEngine -----

    /// <inheritdoc />
    public Task<SearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sw = Stopwatch.StartNew();

        // 1. Parse the query
        var parsed = _queryParser.Parse(request.Query);
        if (parsed.IsEmpty)
        {
            return Task.FromResult(new SearchResult
            {
                Query = request.Query,
                TotalMatches = 0,
                ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
                Items = []
            });
        }

        // 2. Collect all query terms for scoring
        var allTerms = new List<string>();
        allTerms.AddRange(parsed.RequiredTerms);
        allTerms.AddRange(parsed.OptionalTerms);
        foreach (var phrase in parsed.Phrases)
            allTerms.AddRange(phrase);

        // 3. Find candidate documents (any document containing at least one query term)
        var candidateDocIds = new HashSet<string>();
        foreach (var term in allTerms)
        {
            foreach (var posting in _invertedIndex.GetPostings(term))
            {
                candidateDocIds.Add(posting.DocId);
            }
        }

        // 4. Filter by required terms (AND semantics) — O(1) per term via GetPosting
        if (parsed.RequiredTerms.Count > 0)
        {
            candidateDocIds.RemoveWhere(docId =>
            {
                foreach (var term in parsed.RequiredTerms)
                {
                    if (_invertedIndex.GetPosting(term, docId) is null)
                        return true;
                }
                return false;
            });
        }

        // 5. Filter by phrase matches (must have consecutive term positions)
        if (parsed.Phrases.Count > 0)
        {
            candidateDocIds.RemoveWhere(docId => !MatchesPhrases(docId, parsed.Phrases));
        }

        // 6. Apply content type filter if specified
        if (request.ContentTypeFilter.HasValue)
        {
            candidateDocIds.RemoveWhere(docId =>
            {
                var meta = _invertedIndex.GetDocument(docId);
                return meta is null || meta.ContentType != request.ContentTypeFilter.Value;
            });
        }

        // 7. Score and rank
        var scored = new List<(string DocId, double Score)>(candidateDocIds.Count);
        foreach (var docId in candidateDocIds)
        {
            double score = _scorer.Score(allTerms, docId);
            scored.Add((docId, score));
        }

        scored.Sort((a, b) => b.Score.CompareTo(a.Score)); // descending

        int totalMatches = scored.Count;
        var topResults = scored.Take(request.TopN).ToList();

        // 8. Build result items with snippets
        var items = new List<SearchResultItem>(topResults.Count);
        foreach (var (docId, score) in topResults)
        {
            var meta = _invertedIndex.GetDocument(docId);
            var content = _invertedIndex.GetDocumentContent(docId);

            items.Add(new SearchResultItem
            {
                Title = meta?.Title ?? docId,
                Url = meta?.Url ?? meta?.Path,
                Snippet = SnippetGenerator.Generate(content ?? string.Empty, allTerms),
                Score = score,
                SourceType = meta?.ContentType ?? Shared.Enums.ContentType.Markdown
            });
        }

        sw.Stop();

        return Task.FromResult(new SearchResult
        {
            Query = request.Query,
            TotalMatches = totalMatches,
            ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
            Items = items
        });
    }

    // ----- IIndexer -----

    /// <inheritdoc />
    public Task IndexDocumentAsync(DocumentMetadata metadata, string content, CancellationToken cancellationToken = default)
    {
        _invertedIndex.AddDocument(metadata, content);
        ScheduleDebouncedSave();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        _invertedIndex.RemoveDocument(documentId);
        ScheduleDebouncedSave();
        return Task.CompletedTask;
    }

    // ----- Index persistence -----

    /// <summary>
    /// Loads a previously saved index from disk. Call once at startup.
    /// </summary>
    public async Task<bool> LoadIndexAsync(CancellationToken cancellationToken = default)
    {
        if (_storage is null) return false;
        return await _storage.LoadAsync(_invertedIndex, cancellationToken);
    }

    /// <summary>
    /// Immediately flushes the index to disk. Use after bulk indexing operations
    /// to ensure persistence without waiting for the debounce timer.
    /// </summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_storage is not null)
            await _storage.SaveAsync(_invertedIndex, cancellationToken);
    }

    /// <summary>Disposes the debounce timer and performs a final flush.</summary>
    public void Dispose()
    {
        _debounceSaveTimer?.Dispose();
        // Synchronous final save — best-effort to persist on shutdown
        if (_storage is not null)
        {
            try { _storage.SaveAsync(_invertedIndex).GetAwaiter().GetResult(); }
            catch { /* swallow on teardown */ }
        }
    }

    // ----- Private helpers -----

    private void ScheduleDebouncedSave()
    {
        _debounceSaveTimer?.Change(SaveDebounceInterval, Timeout.InfiniteTimeSpan);
    }

    private async void DebouncedSaveCallback(object? state)
    {
        try
        {
            if (_storage is not null)
                await _storage.SaveAsync(_invertedIndex);
        }
        catch
        {
            // Swallow exceptions in timer callbacks to prevent process crashes
        }
    }

    /// <summary>
    /// Checks whether a document contains all specified phrases
    /// (consecutive term positions). Uses O(1) GetPosting lookups.
    /// </summary>
    private bool MatchesPhrases(string docId, List<List<string>> phrases)
    {
        foreach (var phrase in phrases)
        {
            if (phrase.Count == 0) continue;

            // O(1) lookup for the first term's posting in this document
            var firstPosting = _invertedIndex.GetPosting(phrase[0], docId);
            if (firstPosting is null) return false;

            // Check each starting position of the first term
            bool phraseFound = false;
            foreach (var startPos in firstPosting.Positions)
            {
                bool allMatch = true;
                for (int i = 1; i < phrase.Count; i++)
                {
                    // O(1) lookup for subsequent terms
                    var tp = _invertedIndex.GetPosting(phrase[i], docId);

                    if (tp is null || !tp.Positions.Contains(startPos + i))
                    {
                        allMatch = false;
                        break;
                    }
                }

                if (allMatch) { phraseFound = true; break; }
            }

            if (!phraseFound) return false;
        }

        return true;
    }
}
