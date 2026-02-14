using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Theoria.Engine.Crawling;

/// <summary>
/// Discovers relevant URLs for a search query by scraping DuckDuckGo's HTML
/// search results. No API key required.
///
/// This is the "discovery" step: given a user query like "Aquinas natural law",
/// it returns a list of URLs that DuckDuckGo considers relevant. The caller
/// can then fetch each URL and score/rank the content locally.
/// </summary>
public sealed class WebSearchProvider
{
    private readonly HttpClient _httpClient;

    public WebSearchProvider(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateDefaultClient();
    }

    /// <summary>
    /// Searches DuckDuckGo for the given query and returns discovered URLs.
    /// </summary>
    /// <param name="query">The user's search query.</param>
    /// <param name="maxResults">Maximum number of result URLs to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ordered list of (url, title, snippet) tuples from DuckDuckGo.</returns>
    public async Task<IReadOnlyList<WebSearchResult>> SearchAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var results = new List<WebSearchResult>();

        try
        {
            // DuckDuckGo HTML search (no API key needed)
            var encoded = Uri.EscapeDataString(query);
            var url = $"https://html.duckduckgo.com/html/?q={encoded}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            // DuckDuckGo requires form-style requests to work reliably
            request.Headers.Add("Accept", "text/html");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // DuckDuckGo HTML results are in <div class="result"> blocks
            var resultNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'result__body')]")
                           ?? doc.DocumentNode.SelectNodes("//div[contains(@class,'result')]");

            if (resultNodes is null)
                return results;

            foreach (var node in resultNodes)
            {
                if (results.Count >= maxResults) break;

                // Extract the link
                var linkNode = node.SelectSingleNode(".//a[contains(@class,'result__a')]")
                             ?? node.SelectSingleNode(".//a[@href]");

                if (linkNode is null) continue;

                var href = linkNode.GetAttributeValue("href", string.Empty);
                var resultUrl = ExtractRealUrl(href);

                if (string.IsNullOrWhiteSpace(resultUrl)) continue;
                if (!resultUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;

                // Extract title
                var title = HtmlEntity.DeEntitize(linkNode.InnerText).Trim();

                // Extract snippet
                var snippetNode = node.SelectSingleNode(".//a[contains(@class,'result__snippet')]")
                               ?? node.SelectSingleNode(".//div[contains(@class,'result__snippet')]");
                var snippet = snippetNode is not null
                    ? HtmlEntity.DeEntitize(snippetNode.InnerText).Trim()
                    : string.Empty;

                results.Add(new WebSearchResult
                {
                    Url = resultUrl,
                    Title = title,
                    Snippet = snippet
                });
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // If DuckDuckGo is unreachable, return empty list rather than crash
            System.Diagnostics.Debug.WriteLine($"Web search failed: {ex.Message}");
        }

        return results;
    }

    /// <summary>
    /// DuckDuckGo wraps URLs in a redirect like:
    ///   //duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com&amp;...
    /// This extracts the actual destination URL.
    /// </summary>
    private static string ExtractRealUrl(string href)
    {
        if (string.IsNullOrWhiteSpace(href))
            return string.Empty;

        // If the href contains uddg= parameter, extract the real URL
        if (href.Contains("uddg=", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(href, @"uddg=([^&]+)");
            if (match.Success)
            {
                return Uri.UnescapeDataString(match.Groups[1].Value);
            }
        }

        // If already a direct URL
        if (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return href;
        }

        return string.Empty;
    }

    private static HttpClient CreateDefaultClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        return client;
    }
}

/// <summary>
/// A single result returned from a web search engine (e.g., DuckDuckGo).
/// Contains the URL, title, and snippet as shown in the search results page.
/// </summary>
public sealed class WebSearchResult
{
    public required string Url { get; init; }
    public required string Title { get; init; }
    public string Snippet { get; init; } = string.Empty;
}
