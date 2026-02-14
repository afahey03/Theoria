using System.Net;
using Theoria.Shared.Enums;
using Theoria.Shared.Interfaces;
using Theoria.Shared.Models;

namespace Theoria.Engine.Crawling;

/// <summary>
/// Crawls web pages starting from a seed URL, extracts text content,
/// follows links, and indexes discovered pages into the search engine.
///
/// Design:
///   - Breadth-first crawl from the seed URL
///   - Respects MaxPages and MaxDepth limits
///   - Optional same-domain restriction
///   - Polite delay between requests
///   - Reports progress via an IProgress callback
/// </summary>
public sealed class WebCrawler
{
    private readonly HttpClient _httpClient;
    private readonly IIndexer _indexer;

    public WebCrawler(IIndexer indexer, HttpClient? httpClient = null)
    {
        _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
        _httpClient = httpClient ?? CreateDefaultHttpClient();
    }

    /// <summary>
    /// Crawls the web starting from the seed URL, indexing each page into the search engine.
    /// </summary>
    /// <param name="options">Crawl configuration.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Summary of all crawled pages.</returns>
    public async Task<IReadOnlyList<CrawledPage>> CrawlAsync(
        CrawlOptions options,
        IProgress<CrawlProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<CrawledPage>();
        int indexed = 0;

        // BFS queue: (url, depth)
        var queue = new Queue<(string Url, int Depth)>();
        queue.Enqueue((NormalizeUrl(options.SeedUrl), 0));

        Uri.TryCreate(options.SeedUrl, UriKind.Absolute, out var seedUri);
        var seedHost = seedUri?.Host;

        while (queue.Count > 0 && results.Count < options.MaxPages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (url, depth) = queue.Dequeue();

            if (visited.Contains(url)) continue;
            visited.Add(url);

            // Report progress
            progress?.Report(new CrawlProgress
            {
                PagesCrawled = results.Count,
                PagesRemaining = queue.Count,
                PagesIndexed = indexed,
                CurrentUrl = url,
                Status = $"Fetching: {url}"
            });

            // Fetch the page
            var page = await FetchPageAsync(url, cancellationToken);
            results.Add(page);

            if (!page.Success) continue;

            // Index the page content
            if (!string.IsNullOrWhiteSpace(page.TextContent))
            {
                var metadata = new DocumentMetadata
                {
                    Id = url,
                    Title = string.IsNullOrWhiteSpace(page.Title) ? url : page.Title,
                    Url = url,
                    ContentType = ContentType.Html,
                    LastIndexedAt = DateTime.UtcNow
                };

                await _indexer.IndexDocumentAsync(metadata, page.TextContent, cancellationToken);
                indexed++;
            }

            // Follow links if within depth limit
            if (depth < options.MaxDepth)
            {
                foreach (var link in page.OutLinks)
                {
                    if (visited.Contains(link)) continue;
                    if (results.Count + queue.Count >= options.MaxPages * 2) break; // don't queue too many

                    // Same-domain check
                    if (options.SameDomainOnly && seedHost is not null)
                    {
                        if (!Uri.TryCreate(link, UriKind.Absolute, out var linkUri) ||
                            !string.Equals(linkUri.Host, seedHost, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    queue.Enqueue((link, depth + 1));
                }
            }

            // Be polite
            if (options.DelayBetweenRequestsMs > 0 && queue.Count > 0)
                await Task.Delay(options.DelayBetweenRequestsMs, cancellationToken);
        }

        progress?.Report(new CrawlProgress
        {
            PagesCrawled = results.Count,
            PagesRemaining = 0,
            PagesIndexed = indexed,
            CurrentUrl = string.Empty,
            Status = $"Done. Crawled {results.Count} pages, indexed {indexed}."
        });

        return results;
    }

    /// <summary>
    /// Fetches a single URL and returns its text content and outbound links.
    /// </summary>
    public async Task<CrawledPage> FetchPageAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new CrawledPage
                {
                    Url = url,
                    Title = string.Empty,
                    TextContent = string.Empty,
                    OutLinks = [],
                    Success = false,
                    Error = $"HTTP {(int)response.StatusCode} {response.StatusCode}"
                };
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!contentType.Contains("html", StringComparison.OrdinalIgnoreCase) &&
                !contentType.Contains("text", StringComparison.OrdinalIgnoreCase))
            {
                return new CrawledPage
                {
                    Url = url,
                    Title = string.Empty,
                    TextContent = string.Empty,
                    OutLinks = [],
                    Success = false,
                    Error = $"Non-HTML content type: {contentType}"
                };
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var title = HtmlContentExtractor.ExtractTitle(html);
            var text = HtmlContentExtractor.ExtractText(html);
            var links = HtmlContentExtractor.ExtractLinks(html, url);

            return new CrawledPage
            {
                Url = url,
                Title = title,
                TextContent = text,
                OutLinks = links,
                Success = true
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            return new CrawledPage
            {
                Url = url,
                Title = string.Empty,
                TextContent = string.Empty,
                OutLinks = [],
                Success = false,
                Error = ex.Message
            };
        }
    }

    private static string NormalizeUrl(string url)
    {
        // Ensure scheme
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }
        return url.TrimEnd('/');
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        // Identify ourselves as a reasonable user agent
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Theoria/1.0 (Search Engine Crawler; +https://github.com/theoria-search)");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");

        return client;
    }
}
