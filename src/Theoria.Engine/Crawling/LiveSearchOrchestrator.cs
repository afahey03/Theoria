using System.Diagnostics;
using Theoria.Engine.Indexing;
using Theoria.Engine.Scoring;
using Theoria.Engine.Snippets;
using Theoria.Engine.Tokenization;
using Theoria.Shared.Enums;
using Theoria.Shared.Interfaces;
using Theoria.Shared.Models;

namespace Theoria.Engine.Crawling;

/// <summary>
/// Orchestrates a live internet search: the user types a query, the engine
/// discovers relevant URLs via DuckDuckGo, fetches each page, scores the
/// content with BM25, and returns ranked results — all in a single call.
///
/// This is the core pipeline that makes Theoria work like a real search engine:
///   1. Query → DuckDuckGo → list of candidate URLs
///   2. Fetch each URL in parallel (using WebCrawler.FetchPageAsync)
///   3. Build a temporary in-memory BM25 index over the fetched content
///   4. Score &amp; rank documents against the original query
///   5. Generate highlighted snippets
///   6. Return ranked SearchResult
///
/// Both the web API and the desktop client call into this same orchestrator
/// so they share identical behaviour.
/// </summary>
public sealed class LiveSearchOrchestrator
{
    private readonly WebSearchProvider _searchProvider;
    private readonly WebCrawler _crawler;

    /// <summary>Max concurrent page fetches to run in parallel.</summary>
    public int MaxParallelFetches { get; init; } = 8;

    /// <summary>How many DuckDuckGo result URLs to fetch.</summary>
    public int MaxDiscoveryResults { get; init; } = 50;

    public LiveSearchOrchestrator(
        WebSearchProvider searchProvider,
        WebCrawler crawler)
    {
        _searchProvider = searchProvider ?? throw new ArgumentNullException(nameof(searchProvider));
        _crawler = crawler ?? throw new ArgumentNullException(nameof(crawler));
    }

    /// <summary>
    /// Performs a live internet search for the given query.
    /// </summary>
    /// <param name="query">The user's search query.</param>
    /// <param name="topN">Maximum results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A SearchResult with ranked items from live internet content.</returns>
    public async Task<SearchResult> SearchAsync(
        string query,
        int topN = 10,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchResult
            {
                Query = query ?? string.Empty,
                TotalMatches = 0,
                ElapsedMilliseconds = 0,
                Items = []
            };
        }

        // --- Step 1: Discover candidate URLs via DuckDuckGo ---
        var webResults = await _searchProvider.SearchAsync(query, MaxDiscoveryResults, cancellationToken);

        if (webResults.Count == 0)
        {
            sw.Stop();
            return new SearchResult
            {
                Query = query,
                TotalMatches = 0,
                ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
                Items = []
            };
        }

        // --- Step 2: Fetch pages in parallel ---
        var fetchTasks = new List<Task<(WebSearchResult Discovery, CrawledPage Page)>>();
        using var semaphore = new SemaphoreSlim(MaxParallelFetches);

        foreach (var webResult in webResults)
        {
            var task = FetchWithThrottleAsync(semaphore, webResult, cancellationToken);
            fetchTasks.Add(task);
        }

        var fetchResults = await Task.WhenAll(fetchTasks);

        // --- Step 3: Build temporary in-memory index and score ---
        ITokenizer tokenizer = new SimpleTokenizer();
        var invertedIndex = new InvertedIndex(tokenizer);
        IScorer scorer = new BM25Scorer(invertedIndex);

        // A map from URL → fetched data for result building
        var pageMap = new Dictionary<string, (WebSearchResult Discovery, CrawledPage Page)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var (discovery, page) in fetchResults)
        {
            if (!page.Success || string.IsNullOrWhiteSpace(page.TextContent))
                continue;

            var metadata = new DocumentMetadata
            {
                Id = page.Url,
                Title = !string.IsNullOrWhiteSpace(page.Title) ? page.Title : discovery.Title,
                Url = page.Url,
                ContentType = ContentType.Html,
                LastIndexedAt = DateTime.UtcNow
            };

            invertedIndex.AddDocument(metadata, page.TextContent);
            pageMap[page.Url] = (discovery, page);
        }

        // If nothing was successfully fetched, fall back to DuckDuckGo snippets
        if (pageMap.Count == 0)
        {
            sw.Stop();
            var fallbackItems = webResults.Select(r => new SearchResultItem
            {
                Title = r.Title,
                Url = r.Url,
                Snippet = r.Snippet,
                Score = 0,
                SourceType = ContentType.Html
            }).Take(topN).ToList();

            return new SearchResult
            {
                Query = query,
                TotalMatches = fallbackItems.Count,
                ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
                Items = fallbackItems
            };
        }

        // --- Step 4: Score documents against the query ---
        var queryTokens = tokenizer.Tokenize(query);
        var allDocIds = invertedIndex.GetAllDocumentIds();
        var scored = new List<(string DocId, double Score)>();

        foreach (var docId in allDocIds)
        {
            double score = scorer.Score(queryTokens, docId);

            // Boost scholarly / academic domains by 50%
            if (WebSearchProvider.IsScholarlyDomain(docId))
                score *= 1.5;

            scored.Add((docId, score));
        }

        scored.Sort((a, b) => b.Score.CompareTo(a.Score));

        // --- Step 5: Build result items with snippets ---
        var items = new List<SearchResultItem>();
        foreach (var (docId, score) in scored.Take(topN))
        {
            var meta = invertedIndex.GetDocument(docId);
            var content = invertedIndex.GetDocumentContent(docId);

            // Use the page title; fall back to the DuckDuckGo title
            var title = meta?.Title ?? docId;
            if (pageMap.TryGetValue(docId, out var info) && !string.IsNullOrWhiteSpace(info.Page.Title))
                title = info.Page.Title;

            items.Add(new SearchResultItem
            {
                Title = title,
                Url = meta?.Url ?? docId,
                Snippet = SnippetGenerator.Generate(content ?? string.Empty, queryTokens),
                Score = score,
                SourceType = ContentType.Html
            });
        }

        sw.Stop();

        return new SearchResult
        {
            Query = query,
            TotalMatches = scored.Count,
            ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
            Items = items
        };
    }

    private async Task<(WebSearchResult Discovery, CrawledPage Page)> FetchWithThrottleAsync(
        SemaphoreSlim semaphore,
        WebSearchResult discovery,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var page = await _crawler.FetchPageAsync(discovery.Url, cancellationToken);
            return (discovery, page);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
