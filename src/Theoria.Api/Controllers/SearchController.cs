using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Theoria.Engine.Crawling;
using Theoria.Shared.Enums;
using Theoria.Shared.Interfaces;
using Theoria.Shared.Models;

namespace Theoria.Api.Controllers;

/// <summary>
/// Handles search queries.
/// By default, performs a live internet search: discovers URLs via DuckDuckGo,
/// fetches the pages, scores them with BM25, and returns ranked results.
/// Optionally falls back to the local index with ?mode=local.
/// Caches recent results in-memory to avoid redundant crawls.
/// </summary>
[ApiController]
[Route("[controller]")]
public class SearchController : ControllerBase
{
    private readonly LiveSearchOrchestrator _liveSearch;
    private readonly ISearchEngine _localEngine;
    private readonly ILogger<SearchController> _logger;
    private readonly IMemoryCache _cache;

    /// <summary>How long to cache a search result before re-fetching.</summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public SearchController(
        LiveSearchOrchestrator liveSearch,
        ISearchEngine localEngine,
        ILogger<SearchController> logger,
        IMemoryCache cache)
    {
        _liveSearch = liveSearch;
        _localEngine = localEngine;
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// GET /search?q=aquinas&amp;topN=10&amp;mode=live
    /// Searches the internet in real time by default.
    /// Use mode=local to search only the locally indexed documents.
    /// Results are cached for 5 minutes per unique query+mode+topN.
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 120, VaryByQueryKeys = ["q", "topN", "mode"])]
    public async Task<ActionResult<SearchResult>> Search(
        [FromQuery(Name = "q")] string query,
        [FromQuery] int topN = 10,
        [FromQuery] string mode = "live",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { error = "Query parameter 'q' is required." });

        // Build a cache key from query + mode + topN
        var cacheKey = $"search:{mode}:{topN}:{query.Trim().ToLowerInvariant()}";

        if (_cache.TryGetValue(cacheKey, out SearchResult? cached) && cached is not null)
        {
            _logger.LogInformation("Cache hit for q={Query}", query);
            return Ok(cached);
        }

        _logger.LogInformation("Search request: q={Query}, topN={TopN}, mode={Mode}", query, topN, mode);

        SearchResult result;

        if (string.Equals(mode, "local", StringComparison.OrdinalIgnoreCase))
        {
            var request = new SearchRequest
            {
                Query = query,
                TopN = topN
            };
            result = await _localEngine.SearchAsync(request, cancellationToken);
        }
        else
        {
            result = await _liveSearch.SearchAsync(query, topN, cancellationToken);
        }

        // Cache result
        _cache.Set(cacheKey, result, CacheDuration);

        return Ok(result);
    }

    /// <summary>
    /// GET /search/stream?q=aquinas&amp;topN=10
    /// Streams search results via Server-Sent Events (SSE).
    /// First event ("discovery"): instant results from DuckDuckGo snippets.
    /// Second event ("scored"): final BM25-scored results after page fetching.
    /// Connect with EventSource in the browser for real-time updates.
    /// </summary>
    [HttpGet("stream")]
    public async Task StreamSearch(
        [FromQuery(Name = "q")] string query,
        [FromQuery] int topN = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Response.StatusCode = 400;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no"; // Disable buffering for nginx proxies

        _logger.LogInformation("Streaming search: q={Query}, topN={TopN}", query, topN);

        await foreach (var evt in _liveSearch.SearchStreamingAsync(query, topN, cancellationToken))
        {
            var json = JsonSerializer.Serialize(evt.Result);
            await Response.WriteAsync($"event: {evt.Phase}\ndata: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}
