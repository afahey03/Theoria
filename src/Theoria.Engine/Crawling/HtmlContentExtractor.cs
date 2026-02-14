using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Theoria.Engine.Crawling;

/// <summary>
/// Extracts clean, readable text from raw HTML.
/// Uses HtmlAgilityPack to parse the DOM once, strips scripts/styles/nav,
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
    /// Result of a single-pass HTML extraction.
    /// </summary>
    public readonly record struct ExtractionResult(
        string Title,
        string Text,
        IReadOnlyList<string> Links);

    /// <summary>
    /// Parses HTML once and extracts title, visible text, and links in a single pass.
    /// This avoids the cost of parsing the same HTML three times.
    /// </summary>
    public static ExtractionResult Extract(string html, string baseUrl)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var title = ExtractTitleFromDoc(doc);
        var text = ExtractTextFromDoc(doc);
        var links = ExtractLinksFromDoc(doc, baseUrl);

        return new ExtractionResult(title, text, links);
    }

    /// <summary>
    /// Extracts the page title from a pre-parsed document.
    /// </summary>
    public static string ExtractTitle(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return ExtractTitleFromDoc(doc);
    }

    /// <summary>
    /// Extracts visible text content from a pre-parsed document.
    /// </summary>
    public static string ExtractText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return ExtractTextFromDoc(doc);
    }

    /// <summary>
    /// Extracts all hyperlinks from a pre-parsed document.
    /// </summary>
    public static IReadOnlyList<string> ExtractLinks(string html, string baseUrl)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return ExtractLinksFromDoc(doc, baseUrl);
    }

    // --- Internal single-doc helpers ---

    private static string ExtractTitleFromDoc(HtmlDocument doc)
    {
        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        if (titleNode is not null)
            return HtmlEntity.DeEntitize(titleNode.InnerText).Trim();

        var h1Node = doc.DocumentNode.SelectSingleNode("//h1");
        if (h1Node is not null)
            return HtmlEntity.DeEntitize(h1Node.InnerText).Trim();

        return string.Empty;
    }

    private static string ExtractTextFromDoc(HtmlDocument doc)
    {
        var sb = new StringBuilder(4096);
        ExtractTextRecursive(doc.DocumentNode, sb);

        var text = MultipleWhitespace().Replace(sb.ToString(), " ").Trim();
        return text;
    }

    private static IReadOnlyList<string> ExtractLinksFromDoc(HtmlDocument doc, string baseUrl)
    {
        var anchorNodes = doc.DocumentNode.SelectNodes("//a[@href]");
        if (anchorNodes is null) return [];

        Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri);
        var links = new List<string>(anchorNodes.Count);

        foreach (var anchor in anchorNodes)
        {
            var href = anchor.GetAttributeValue("href", string.Empty);
            if (string.IsNullOrWhiteSpace(href)) continue;

            if (href.StartsWith('#') || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
                                     || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                continue;

            if (baseUri is not null && Uri.TryCreate(baseUri, href, out var absolute))
            {
                if (absolute.Scheme is "http" or "https")
                    links.Add(absolute.GetLeftPart(UriPartial.Query));
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
        if (ExcludedTags.Contains(node.Name)) return;

        foreach (var child in node.ChildNodes)
        {
            ExtractTextRecursive(child, sb);
        }

        if (node.Name is "p" or "div" or "br" or "li" or "h1" or "h2" or "h3"
            or "h4" or "h5" or "h6" or "tr" or "blockquote" or "section" or "article")
        {
            sb.Append(' ');
        }
    }
}
