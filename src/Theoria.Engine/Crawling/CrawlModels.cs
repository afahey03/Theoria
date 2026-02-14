namespace Theoria.Engine.Crawling;

/// <summary>
/// Configuration for a crawl operation.
/// </summary>
public sealed class CrawlOptions
{
    /// <summary>The starting URL to crawl from.</summary>
    public required string SeedUrl { get; init; }

    /// <summary>Maximum number of pages to fetch. Prevents runaway crawls.</summary>
    public int MaxPages { get; init; } = 20;

    /// <summary>
    /// Whether to stay on the same domain as the seed URL.
    /// When true, only links on the same host are followed.
    /// </summary>
    public bool SameDomainOnly { get; init; } = true;

    /// <summary>Maximum crawl depth from the seed page. 0 = seed only.</summary>
    public int MaxDepth { get; init; } = 2;

    /// <summary>Delay between requests to be polite to servers (ms).</summary>
    public int DelayBetweenRequestsMs { get; init; } = 500;
}

/// <summary>
/// Result of crawling a single page.
/// </summary>
public sealed class CrawledPage
{
    public required string Url { get; init; }
    public required string Title { get; init; }
    public required string TextContent { get; init; }
    public required IReadOnlyList<string> OutLinks { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Progress report emitted during a crawl.
/// </summary>
public sealed class CrawlProgress
{
    public int PagesCrawled { get; init; }
    public int PagesRemaining { get; init; }
    public int PagesIndexed { get; init; }
    public string CurrentUrl { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}
