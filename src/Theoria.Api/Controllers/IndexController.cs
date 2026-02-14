using Microsoft.AspNetCore.Mvc;
using Theoria.Engine.Crawling;
using Theoria.Shared.Enums;
using Theoria.Shared.Interfaces;
using Theoria.Shared.Models;

namespace Theoria.Api.Controllers;

/// <summary>
/// Handles document indexing operations via HTTP.
/// Accepts file paths, URLs (fetched from the internet), and crawl requests.
/// </summary>
[ApiController]
[Route("[controller]")]
public class IndexController : ControllerBase
{
    private readonly IIndexer _indexer;
    private readonly WebCrawler _crawler;
    private readonly ILogger<IndexController> _logger;

    public IndexController(IIndexer indexer, WebCrawler crawler, ILogger<IndexController> logger)
    {
        _indexer = indexer;
        _crawler = crawler;
        _logger = logger;
    }

    /// <summary>
    /// POST /index/file
    /// Indexes a file from the local file system.
    /// Body: { "path": "C:\\docs\\paper.md", "title": "Paper Title" }
    /// </summary>
    [HttpPost("file")]
    public async Task<IActionResult> IndexFile(
        [FromBody] IndexFileRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
            return BadRequest(new { error = "File path is required." });

        if (!System.IO.File.Exists(request.Path))
            return NotFound(new { error = $"File not found: {request.Path}" });

        _logger.LogInformation("Indexing file: {Path}", request.Path);

        var content = await System.IO.File.ReadAllTextAsync(request.Path, cancellationToken);
        var extension = System.IO.Path.GetExtension(request.Path).ToLowerInvariant();

        var contentType = extension switch
        {
            ".md" or ".markdown" => ContentType.Markdown,
            ".html" or ".htm" => ContentType.Html,
            ".pdf" => ContentType.Pdf,
            _ => ContentType.Markdown // default fallback
        };

        var metadata = new DocumentMetadata
        {
            Id = request.Path, // use path as unique ID
            Title = request.Title ?? System.IO.Path.GetFileNameWithoutExtension(request.Path),
            Path = request.Path,
            ContentType = contentType,
            LastIndexedAt = DateTime.UtcNow
        };

        await _indexer.IndexDocumentAsync(metadata, content, cancellationToken);

        return Ok(new { message = "Document indexed successfully.", documentId = metadata.Id });
    }

    /// <summary>
    /// POST /index/url
    /// Fetches a web page from the internet, extracts its text, and indexes it.
    /// Body: { "url": "https://example.com/article", "title": "Article Title" }
    /// </summary>
    [HttpPost("url")]
    public async Task<IActionResult> IndexUrl(
        [FromBody] IndexUrlRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return BadRequest(new { error = "URL is required." });

        _logger.LogInformation("Fetching and indexing URL: {Url}", request.Url);

        // Actually fetch the page from the internet
        var page = await _crawler.FetchPageAsync(request.Url, cancellationToken);

        if (!page.Success)
            return BadRequest(new { error = $"Failed to fetch URL: {page.Error}" });

        if (string.IsNullOrWhiteSpace(page.TextContent))
            return BadRequest(new { error = "No text content could be extracted from the URL." });

        var metadata = new DocumentMetadata
        {
            Id = request.Url,
            Title = request.Title ?? page.Title ?? request.Url,
            Url = request.Url,
            ContentType = ContentType.Html,
            LastIndexedAt = DateTime.UtcNow
        };

        await _indexer.IndexDocumentAsync(metadata, page.TextContent, cancellationToken);

        return Ok(new
        {
            message = "URL fetched and indexed successfully.",
            documentId = metadata.Id,
            title = metadata.Title,
            contentLength = page.TextContent.Length,
            linksFound = page.OutLinks.Count
        });
    }

    /// <summary>
    /// POST /index/crawl
    /// Crawls the web starting from a seed URL, following links and indexing
    /// every page discovered. This brings real internet content into the index.
    ///
    /// Body: { "seedUrl": "https://plato.stanford.edu/entries/aquinas/",
    ///          "maxPages": 10, "maxDepth": 2, "sameDomainOnly": true }
    /// </summary>
    [HttpPost("crawl")]
    public async Task<IActionResult> Crawl(
        [FromBody] CrawlRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SeedUrl))
            return BadRequest(new { error = "Seed URL is required." });

        _logger.LogInformation(
            "Starting crawl from {SeedUrl}, maxPages={MaxPages}, maxDepth={MaxDepth}",
            request.SeedUrl, request.MaxPages, request.MaxDepth);

        var options = new CrawlOptions
        {
            SeedUrl = request.SeedUrl,
            MaxPages = Math.Clamp(request.MaxPages, 1, 50), // cap at 50 to prevent abuse
            MaxDepth = Math.Clamp(request.MaxDepth, 0, 3),
            SameDomainOnly = request.SameDomainOnly,
            DelayBetweenRequestsMs = 500
        };

        var results = await _crawler.CrawlAsync(options, cancellationToken: cancellationToken);

        var succeeded = results.Count(r => r.Success);
        var failed = results.Count(r => !r.Success);

        return Ok(new
        {
            message = $"Crawl complete. {succeeded} pages indexed, {failed} failed.",
            totalCrawled = results.Count,
            pagesIndexed = succeeded,
            pagesFailed = failed,
            pages = results.Select(r => new
            {
                r.Url,
                r.Title,
                r.Success,
                r.Error,
                contentLength = r.TextContent.Length
            })
        });
    }
}

// --- Request DTOs ---

public sealed record IndexFileRequest
{
    public string? Path { get; init; }
    public string? Title { get; init; }
}

public sealed record IndexUrlRequest
{
    public string? Url { get; init; }
    public string? Title { get; init; }
    /// <summary>Optional pre-fetched content. If empty, the server should fetch the URL.</summary>
    public string? Content { get; init; }
}

public sealed record CrawlRequest
{
    public string? SeedUrl { get; init; }
    public int MaxPages { get; init; } = 10;
    public int MaxDepth { get; init; } = 2;
    public bool SameDomainOnly { get; init; } = true;
}
