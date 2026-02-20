using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
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
/// Pipeline:
///   1. Query → DuckDuckGo → list of candidate URLs (deduplicated)
///   2. Fetch each URL in parallel with per-page timeout
///   3. Build a temporary in-memory BM25 index over the fetched content
///   4. Score &amp; rank with: BM25 base + title match boost + scholarly domain boost
///   5. Generate best-window highlighted snippets
///   6. Return ranked SearchResult with source-type metadata
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

    /// <summary>Per-page fetch timeout in seconds. Slow sites won't block the whole search.</summary>
    public int PerPageTimeoutSeconds { get; init; } = 10;

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

        // --- Step 1b: Deduplicate URLs (normalize www, trailing slashes, etc.) ---
        var deduped = DeduplicateResults(webResults);

        // --- Step 1c: DNS prefetching — resolve all unique hosts in parallel ---
        PrefetchDns(deduped);

        // --- Step 2: Fetch pages in parallel with per-page timeout ---
        var fetchTasks = new List<Task<(WebSearchResult Discovery, CrawledPage Page)>>();
        using var semaphore = new SemaphoreSlim(MaxParallelFetches);

        foreach (var webResult in deduped)
        {
            var task = FetchWithThrottleAsync(semaphore, webResult, cancellationToken);
            fetchTasks.Add(task);
        }

        var fetchResults = await Task.WhenAll(fetchTasks);

        // --- Step 3: Build temporary in-memory index ---
        ITokenizer tokenizer = new SimpleTokenizer();
        var invertedIndex = new InvertedIndex(tokenizer);
        IScorer scorer = new BM25Scorer(invertedIndex);

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
            var fallbackItems = deduped.Select(r => new SearchResultItem
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

        // --- Step 4: Score documents with BM25 + title boost + domain boost ---
        var queryTokens = tokenizer.Tokenize(query);
        var allDocIds = invertedIndex.GetAllDocumentIds();
        var scored = new List<(string DocId, double Score)>();

        foreach (var docId in allDocIds)
        {
            double score = scorer.Score(queryTokens, docId);

            // Title match boost: if query terms appear in the page title, boost up to 30%
            // Uses HashSet for O(n+m) instead of O(n*m) nested loops
            var meta = invertedIndex.GetDocument(docId);
            if (meta is not null)
            {
                var titleTokens = tokenizer.Tokenize(meta.Title);
                var titleTokenSet = new HashSet<string>(titleTokens, StringComparer.Ordinal);
                int titleMatches = 0;
                foreach (var qt in queryTokens)
                {
                    if (titleTokenSet.Contains(qt))
                        titleMatches++;
                }
                if (titleMatches > 0)
                {
                    double titleBoost = 1.0 + 0.3 * ((double)titleMatches / queryTokens.Count);
                    score *= titleBoost;
                }
            }

            // Boost scholarly / academic domains by 50%
            if (WebSearchProvider.IsScholarlyDomain(docId))
                score *= 1.5;

            scored.Add((docId, score));
        }

        scored.Sort((a, b) => b.Score.CompareTo(a.Score));

        // --- Step 5: Build result items with best-match snippets ---
        var items = new List<SearchResultItem>();
        foreach (var (docId, score) in scored.Take(topN))
        {
            var meta = invertedIndex.GetDocument(docId);
            var content = invertedIndex.GetDocumentContent(docId);

            var title = meta?.Title ?? docId;
            if (pageMap.TryGetValue(docId, out var info) && !string.IsNullOrWhiteSpace(info.Page.Title))
                title = info.Page.Title;

            var url = meta?.Url ?? docId;
            string? domain = null;
            try { domain = new Uri(url).Host.Replace("www.", ""); } catch { }

            items.Add(new SearchResultItem
            {
                Title = title,
                Url = url,
                Snippet = SnippetGenerator.Generate(content ?? string.Empty, queryTokens),
                Score = score,
                SourceType = ContentType.Html,
                IsScholarly = WebSearchProvider.IsScholarlyDomain(url),
                Domain = domain
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

    /// <summary>
    /// Deduplicates web search results by normalizing URLs:
    /// strips www., trailing slashes, fragments, and normalizes http→https.
    /// </summary>
    private static List<WebSearchResult> DeduplicateResults(IReadOnlyList<WebSearchResult> results)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deduped = new List<WebSearchResult>();

        foreach (var r in results)
        {
            var normalized = NormalizeUrl(r.Url);
            if (seen.Add(normalized))
                deduped.Add(r);
        }
        return deduped;
    }

    /// <summary>
    /// Normalizes a URL for deduplication: https, no www, no trailing slash, no fragment.
    /// </summary>
    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        try
        {
            var uri = new Uri(url);
            var host = uri.Host;
            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                host = host[4..];
            var path = uri.AbsolutePath.TrimEnd('/');
            var queryString = uri.Query;
            return $"https://{host}{path}{queryString}".ToLowerInvariant();
        }
        catch
        {
            return url.ToLowerInvariant().TrimEnd('/');
        }
    }

    private async Task<(WebSearchResult Discovery, CrawledPage Page)> FetchWithThrottleAsync(
        SemaphoreSlim semaphore,
        WebSearchResult discovery,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            // Per-page timeout so one slow site doesn't block the entire search
            using var perPageCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            perPageCts.CancelAfter(TimeSpan.FromSeconds(PerPageTimeoutSeconds));

            var page = await _crawler.FetchPageAsync(discovery.Url, perPageCts.Token);
            return (discovery, page);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Per-page timeout hit, not a user cancellation — return a failure
            return (discovery, new CrawledPage
            {
                Url = discovery.Url,
                Title = string.Empty,
                TextContent = string.Empty,
                OutLinks = [],
                Success = false,
                Error = "Page fetch timed out"
            });
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Pre-resolves DNS for all unique hosts in the result set.
    /// Fire-and-forget: warms the OS DNS cache so subsequent HTTP connections
    /// skip DNS resolution latency.
    /// </summary>
    private static void PrefetchDns(IReadOnlyList<WebSearchResult> results)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in results)
        {
            if (Uri.TryCreate(r.Url, UriKind.Absolute, out var u)
                && u.Host is { Length: > 0 } host
                && seen.Add(host))
            {
                _ = Dns.GetHostEntryAsync(host);
            }
        }
    }

    /// <summary>
    /// Performs a live internet search and yields results in phases via IAsyncEnumerable.
    /// Phase 1 ("discovery"): preliminary results from search provider snippets (instant).
    /// Phase 2 ("scored"): final BM25-scored results after page fetching and scoring.
    /// Designed for Server-Sent Events streaming.
    /// </summary>
    public async IAsyncEnumerable<StreamedSearchEvent> SearchStreamingAsync(
        string query,
        int topN = 10,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(query))
        {
            yield return new StreamedSearchEvent
            {
                Phase = "scored",
                Result = new SearchResult
                {
                    Query = query ?? string.Empty,
                    TotalMatches = 0,
                    ElapsedMilliseconds = 0,
                    Items = []
                }
            };
            yield break;
        }

        // --- Discover candidate URLs ---
        var webResults = await _searchProvider.SearchAsync(query, MaxDiscoveryResults, cancellationToken);

        if (webResults.Count == 0)
        {
            yield return new StreamedSearchEvent
            {
                Phase = "scored",
                Result = new SearchResult
                {
                    Query = query,
                    TotalMatches = 0,
                    ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
                    Items = []
                }
            };
            yield break;
        }

        var deduped = DeduplicateResults(webResults);

        // --- Phase 1: Yield discovery results immediately (DuckDuckGo snippets) ---
        var discoveryItems = deduped.Take(topN).Select(r => new SearchResultItem
        {
            Title = r.Title,
            Url = r.Url,
            Snippet = r.Snippet,
            Score = 0,
            SourceType = ContentType.Html
        }).ToList();

        yield return new StreamedSearchEvent
        {
            Phase = "discovery",
            Result = new SearchResult
            {
                Query = query,
                TotalMatches = discoveryItems.Count,
                ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
                Items = discoveryItems
            }
        };

        // --- DNS prefetching + page fetching ---
        PrefetchDns(deduped);

        var fetchTasks = new List<Task<(WebSearchResult Discovery, CrawledPage Page)>>();
        using var semaphore = new SemaphoreSlim(MaxParallelFetches);
        foreach (var webResult in deduped)
            fetchTasks.Add(FetchWithThrottleAsync(semaphore, webResult, cancellationToken));

        var fetchResults = await Task.WhenAll(fetchTasks);

        // --- Build index + score ---
        ITokenizer tokenizer = new SimpleTokenizer();
        var invertedIndex = new InvertedIndex(tokenizer);
        IScorer scorer = new BM25Scorer(invertedIndex);
        var pageMap = new Dictionary<string, (WebSearchResult Discovery, CrawledPage Page)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var (discovery, page) in fetchResults)
        {
            if (!page.Success || string.IsNullOrWhiteSpace(page.TextContent)) continue;

            invertedIndex.AddDocument(new DocumentMetadata
            {
                Id = page.Url,
                Title = !string.IsNullOrWhiteSpace(page.Title) ? page.Title : discovery.Title,
                Url = page.Url,
                ContentType = ContentType.Html,
                LastIndexedAt = DateTime.UtcNow
            }, page.TextContent);
            pageMap[page.Url] = (discovery, page);
        }

        // If no pages fetched successfully, re-yield discovery results as final
        if (pageMap.Count == 0)
        {
            yield return new StreamedSearchEvent
            {
                Phase = "scored",
                Result = new SearchResult
                {
                    Query = query,
                    TotalMatches = discoveryItems.Count,
                    ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
                    Items = discoveryItems
                }
            };
            yield break;
        }

        var queryTokens = tokenizer.Tokenize(query);
        var scored = new List<(string DocId, double Score)>();

        foreach (var docId in invertedIndex.GetAllDocumentIds())
        {
            double score = scorer.Score(queryTokens, docId);
            var meta = invertedIndex.GetDocument(docId);
            if (meta is not null)
            {
                var titleTokenSet = new HashSet<string>(tokenizer.Tokenize(meta.Title), StringComparer.Ordinal);
                int titleMatches = queryTokens.Count(qt => titleTokenSet.Contains(qt));
                if (titleMatches > 0)
                    score *= 1.0 + 0.3 * ((double)titleMatches / queryTokens.Count);
            }
            if (WebSearchProvider.IsScholarlyDomain(docId))
                score *= 1.5;
            scored.Add((docId, score));
        }

        scored.Sort((a, b) => b.Score.CompareTo(a.Score));

        // --- Phase 2: Yield final BM25-scored results ---
        var items = new List<SearchResultItem>();
        foreach (var (docId, score) in scored.Take(topN))
        {
            var meta = invertedIndex.GetDocument(docId);
            var content = invertedIndex.GetDocumentContent(docId);
            var title = meta?.Title ?? docId;
            if (pageMap.TryGetValue(docId, out var info) && !string.IsNullOrWhiteSpace(info.Page.Title))
                title = info.Page.Title;
            var url = meta?.Url ?? docId;
            string? domain = null;
            try { domain = new Uri(url).Host.Replace("www.", ""); } catch { }

            items.Add(new SearchResultItem
            {
                Title = title,
                Url = url,
                Snippet = SnippetGenerator.Generate(content ?? string.Empty, queryTokens),
                Score = score,
                SourceType = ContentType.Html,
                IsScholarly = WebSearchProvider.IsScholarlyDomain(url),
                Domain = domain
            });
        }

        sw.Stop();
        yield return new StreamedSearchEvent
        {
            Phase = "scored",
            Result = new SearchResult
            {
                Query = query,
                TotalMatches = scored.Count,
                ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
                Items = items
            }
        };
    }
}

/// <summary>
/// An event emitted during streamed search.
/// Phase "discovery" = initial quick results from search provider snippets (instant).
/// Phase "scored" = final BM25-scored results after page fetching and scoring.
/// </summary>
public sealed class StreamedSearchEvent
{
    /// <summary>The search phase: "discovery" or "scored".</summary>
    public required string Phase { get; init; }

    /// <summary>The search result data for this phase.</summary>
    public required SearchResult Result { get; init; }
}
