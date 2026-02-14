using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Theoria.Engine.Crawling;

/// <summary>
/// Extracts clean, readable text from raw HTML.
/// Uses HtmlAgilityPack to parse the DOM, strips scripts/styles/nav,
/// and returns the visible text content + discovered links.
/// </summary>
public static partial class HtmlContentExtractor
{
    /// <summary>Tags whose content is never visible text.</summary>
    private static readonly HashSet<string> ExcludedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "noscript", "svg", "path", "iframe",
        "nav", "footer", "header"
    };

    [GeneratedRegex(@"\s{2,}", RegexOptions.Compiled)]
    private static partial Regex MultipleWhitespace();

    /// <summary>
    /// Extracts the page title from HTML.
    /// </summary>
    public static string ExtractTitle(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Try <title> tag first
        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        if (titleNode is not null)
            return HtmlEntity.DeEntitize(titleNode.InnerText).Trim();

        // Fall back to first <h1>
        var h1Node = doc.DocumentNode.SelectSingleNode("//h1");
        if (h1Node is not null)
            return HtmlEntity.DeEntitize(h1Node.InnerText).Trim();

        return string.Empty;
    }

    /// <summary>
    /// Extracts visible text content from HTML, stripping all tags,
    /// scripts, styles, and navigation elements.
    /// </summary>
    public static string ExtractText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var sb = new StringBuilder();
        ExtractTextRecursive(doc.DocumentNode, sb);

        // Collapse multiple whitespace into single spaces
        var text = MultipleWhitespace().Replace(sb.ToString(), " ").Trim();
        return text;
    }

    /// <summary>
    /// Extracts all hyperlinks (href values) from the HTML.
    /// Returns absolute URLs when possible using the base URL for resolution.
    /// </summary>
    public static IReadOnlyList<string> ExtractLinks(string html, string baseUrl)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var links = new List<string>();
        var anchorNodes = doc.DocumentNode.SelectNodes("//a[@href]");

        if (anchorNodes is null) return links;

        Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri);

        foreach (var anchor in anchorNodes)
        {
            var href = anchor.GetAttributeValue("href", string.Empty);
            if (string.IsNullOrWhiteSpace(href)) continue;

            // Skip fragment-only links, javascript:, mailto:, etc.
            if (href.StartsWith('#') || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
                                     || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                continue;

            // Resolve relative URLs
            if (baseUri is not null && Uri.TryCreate(baseUri, href, out var absolute))
            {
                // Only keep http/https links
                if (absolute.Scheme is "http" or "https")
                    links.Add(absolute.GetLeftPart(UriPartial.Query)); // strip fragments
            }
            else if (Uri.TryCreate(href, UriKind.Absolute, out var abs) && abs.Scheme is "http" or "https")
            {
                links.Add(abs.GetLeftPart(UriPartial.Query));
            }
        }

        return links;
    }

    private static void ExtractTextRecursive(HtmlNode node, StringBuilder sb)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            var text = HtmlEntity.DeEntitize(node.InnerText);
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.Append(text);
                sb.Append(' ');
            }
            return;
        }

        if (node.NodeType != HtmlNodeType.Element) return;

        // Skip excluded elements entirely
        if (ExcludedTags.Contains(node.Name)) return;

        foreach (var child in node.ChildNodes)
        {
            ExtractTextRecursive(child, sb);
        }

        // Add line breaks after block-level elements
        if (node.Name is "p" or "div" or "br" or "li" or "h1" or "h2" or "h3"
            or "h4" or "h5" or "h6" or "tr" or "blockquote" or "section" or "article")
        {
            sb.Append(' ');
        }
    }
}
