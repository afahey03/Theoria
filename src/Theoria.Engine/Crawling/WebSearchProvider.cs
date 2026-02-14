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
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var scholarlyQuery = BuildScholarlyQuery(query);
        var encoded = Uri.EscapeDataString(scholarlyQuery);

        // Fetch up to 2 pages from DuckDuckGo to get enough results
        // Page 1: normal GET request
        // Page 2: use the s= offset extracted from page 1's "next" form
        string? nextFormData = null;

        for (int page = 0; page < 2 && results.Count < maxResults; page++)
        {
            try
            {
                string html;

                if (page == 0)
                {
                    var url = $"https://html.duckduckgo.com/html/?q={encoded}";
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("Accept", "text/html");
                    request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
                    var response = await _httpClient.SendAsync(request, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    html = await response.Content.ReadAsStringAsync(cancellationToken);
                }
                else if (nextFormData is not null)
                {
                    // POST to fetch the next page using form data from page 1
                    var request = new HttpRequestMessage(HttpMethod.Post, "https://html.duckduckgo.com/html/")
                    {
                        Content = new StringContent(nextFormData, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded")
                    };
                    request.Headers.Add("Accept", "text/html");
                    request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
                    var response = await _httpClient.SendAsync(request, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    html = await response.Content.ReadAsStringAsync(cancellationToken);
                }
                else
                {
                    break; // no next page data available
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Parse results from this page
                var resultNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'result__body')]")
                               ?? doc.DocumentNode.SelectNodes("//div[contains(@class,'result')]");

                if (resultNodes is not null)
                {
                    foreach (var node in resultNodes)
                    {
                        if (results.Count >= maxResults) break;

                        var linkNode = node.SelectSingleNode(".//a[contains(@class,'result__a')]")
                                     ?? node.SelectSingleNode(".//a[@href]");

                        if (linkNode is null) continue;

                        var href = linkNode.GetAttributeValue("href", string.Empty);
                        var resultUrl = ExtractRealUrl(href);

                        if (string.IsNullOrWhiteSpace(resultUrl)) continue;
                        if (!resultUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!seenUrls.Add(resultUrl)) continue; // skip duplicates

                        var title = HtmlEntity.DeEntitize(linkNode.InnerText).Trim();

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

                // Extract the "next page" form data for pagination
                nextFormData = ExtractNextPageFormData(doc);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"Web search page {page} failed: {ex.Message}");
                break;
            }
        }

        return results;
    }

    /// <summary>
    /// Extracts the hidden form fields from DuckDuckGo's "Next Page" form
    /// so we can POST for page 2.
    /// </summary>
    private static string? ExtractNextPageFormData(HtmlDocument doc)
    {
        // DuckDuckGo has a form with class "nav-link" for the next button
        var nextForms = doc.DocumentNode.SelectNodes("//form[contains(@class,'nav-link')]");
        if (nextForms is null || nextForms.Count == 0)
        {
            // Fallback: look for any form with a "next" input
            nextForms = doc.DocumentNode.SelectNodes("//form[.//input[@value='Next']]");
        }

        if (nextForms is null || nextForms.Count == 0)
            return null;

        // Take the last form (usually the "Next" button at bottom)
        var form = nextForms[^1];
        var inputs = form.SelectNodes(".//input[@name]");
        if (inputs is null) return null;

        var pairs = new List<string>();
        foreach (var input in inputs)
        {
            var name = input.GetAttributeValue("name", "");
            var value = input.GetAttributeValue("value", "");
            if (!string.IsNullOrEmpty(name))
                pairs.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}");
        }

        return pairs.Count > 0 ? string.Join("&", pairs) : null;
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

    /// <summary>
    /// Augments the user's raw query with scholarly/academic qualifiers
    /// to bias DuckDuckGo results towards theology and philosophy scholarship.
    /// Does NOT use site: restrictions (which would limit results to only those
    /// domains). Instead, adds contextual academic terms so DuckDuckGo naturally
    /// favours scholarly pages. The domain-based score boost in
    /// <see cref="LiveSearchOrchestrator"/> handles rank promotion separately.
    /// </summary>
    private static string BuildScholarlyQuery(string query)
    {
        var lower = query.ToLowerInvariant();

        bool hasAcademicTerm = lower.Contains("scholar") ||
                               lower.Contains("academic") ||
                               lower.Contains("journal") ||
                               lower.Contains("paper") ||
                               lower.Contains("site:");

        if (hasAcademicTerm)
            return query;

        // Light contextual bias without site: restrictions
        return $"{query} scholarly theology philosophy";
    }

    /// <summary>
    /// Well-known scholarly and theological domains that receive a BM25 score
    /// boost via <see cref="LiveSearchOrchestrator"/>.
    /// </summary>
    public static readonly HashSet<string> ScholarlyDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        // Academic repositories & encyclopedias
        "plato.stanford.edu",       // Stanford Encyclopedia of Philosophy
        "iep.utm.edu",              // Internet Encyclopedia of Philosophy
        "jstor.org",                // JSTOR
        "academia.edu",             // Academia.edu
        "philpapers.org",           // PhilPapers
        "scholar.google.com",       // Google Scholar
        "arxiv.org",
        "doi.org",

        // Catholic / Thomistic sources
        "newadvent.org",            // Catholic Encyclopedia & Church Fathers
        "corpusthomisticum.org",    // Corpus Thomisticum
        "dhspriory.org",            // Dominican House of Studies
        "aquinas.cc",               // Aquinas online
        "ccel.org",                 // Christian Classics Ethereal Library
        "fordham.edu",              // Fordham Medieval Sourcebook

        // Orthodox & Patristic
        "orthodoxwiki.org",

        // Protestant / Reformed
        "carm.org",
        "monergism.com",
        "theopedia.com",

        // General theology & encyclopedias
        "britannica.com",
        "en.wikipedia.org",
    };

    /// <summary>
    /// Returns true if the given URL belongs to a known scholarly domain.
    /// </summary>
    public static bool IsScholarlyDomain(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        try
        {
            var host = new Uri(url).Host;
            // Check exact match or suffix match (e.g. "www.jstor.org" matches "jstor.org")
            foreach (var domain in ScholarlyDomains)
            {
                if (host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { /* malformed URL, ignore */ }
        return false;
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
