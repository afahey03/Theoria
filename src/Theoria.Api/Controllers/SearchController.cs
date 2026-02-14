using Microsoft.AspNetCore.Mvc;
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
/// </summary>
[ApiController]
[Route("[controller]")]
public class SearchController : ControllerBase
{
    private readonly LiveSearchOrchestrator _liveSearch;
    private readonly ISearchEngine _localEngine;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        LiveSearchOrchestrator liveSearch,
        ISearchEngine localEngine,
        ILogger<SearchController> logger)
    {
        _liveSearch = liveSearch;
        _localEngine = localEngine;
        _logger = logger;
    }

    /// <summary>
    /// GET /search?q=aquinas&amp;topN=10&amp;mode=live
    /// Searches the internet in real time by default.
    /// Use mode=local to search only the locally indexed documents.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SearchResult>> Search(
        [FromQuery(Name = "q")] string query,
        [FromQuery] int topN = 10,
        [FromQuery] string mode = "live",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { error = "Query parameter 'q' is required." });

        _logger.LogInformation("Search request: q={Query}, topN={TopN}, mode={Mode}", query, topN, mode);

        SearchResult result;

        if (string.Equals(mode, "local", StringComparison.OrdinalIgnoreCase))
        {
            // Search only the locally persisted index
            var request = new SearchRequest
            {
                Query = query,
                TopN = topN
            };
            result = await _localEngine.SearchAsync(request, cancellationToken);
        }
        else
        {
            // Live internet search (default)
            result = await _liveSearch.SearchAsync(query, topN, cancellationToken);
        }

        return Ok(result);
    }
}
