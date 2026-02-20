using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace Theoria.Engine.Crawling;

/// <summary>
/// Extracts clean, readable text from raw HTML.
/// Uses AngleSharp's fully HTML5-compliant parser for correct DOM construction,
/// strips scripts/styles/nav, and returns the visible text content + discovered links.
/// AngleSharp is thread-safe and handles malformed HTML better than legacy parsers.
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

    /// <summary>Reusable HTML parser — AngleSharp's HtmlParser is thread-safe.</summary>
    private static readonly HtmlParser Parser = new();

    /// <summary>
    /// Parses HTML once and extracts title, visible text, and links in a single pass.
    /// This avoids the cost of parsing the same HTML three times.
    /// </summary>
    public static ExtractionResult Extract(string html, string baseUrl)
    {
        var doc = Parser.ParseDocument(html);

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
        var doc = Parser.ParseDocument(html);
        return ExtractTitleFromDoc(doc);
    }

    /// <summary>
    /// Extracts visible text content from a pre-parsed document.
    /// </summary>
    public static string ExtractText(string html)
    {
        var doc = Parser.ParseDocument(html);
        return ExtractTextFromDoc(doc);
    }

    /// <summary>
    /// Extracts all hyperlinks from a pre-parsed document.
    /// </summary>
    public static IReadOnlyList<string> ExtractLinks(string html, string baseUrl)
    {
        var doc = Parser.ParseDocument(html);
        return ExtractLinksFromDoc(doc, baseUrl);
    }

    // --- Internal single-doc helpers ---

    private static string ExtractTitleFromDoc(IHtmlDocument doc)
    {
        var titleEl = doc.QuerySelector("title");
        if (titleEl is not null)
            return titleEl.TextContent.Trim();

        var h1El = doc.QuerySelector("h1");
        if (h1El is not null)
            return h1El.TextContent.Trim();

        return string.Empty;
    }

    private static string ExtractTextFromDoc(IHtmlDocument doc)
    {
        var sb = new StringBuilder(4096);
        var root = (INode?)doc.Body ?? doc.DocumentElement;
        if (root is not null)
            ExtractTextRecursive(root, sb);

        var text = MultipleWhitespace().Replace(sb.ToString(), " ").Trim();
        return text;
    }

    private static IReadOnlyList<string> ExtractLinksFromDoc(IHtmlDocument doc, string baseUrl)
    {
        var anchors = doc.QuerySelectorAll("a[href]");
        if (anchors.Length == 0) return [];

        Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri);
        var links = new List<string>(anchors.Length);

        foreach (var anchor in anchors)
        {
            var href = anchor.GetAttribute("href") ?? string.Empty;
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

    private static void ExtractTextRecursive(INode node, StringBuilder sb)
    {
        if (node.NodeType == NodeType.Text)
        {
            var text = node.TextContent;
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.Append(text);
                sb.Append(' ');
            }
            return;
        }

        if (node is not IElement element) return;
        if (ExcludedTags.Contains(element.LocalName)) return;

        foreach (var child in node.ChildNodes)
        {
            ExtractTextRecursive(child, sb);
        }

        if (element.LocalName is "p" or "div" or "br" or "li" or "h1" or "h2" or "h3"
            or "h4" or "h5" or "h6" or "tr" or "blockquote" or "section" or "article")
        {
            sb.Append(' ');
        }
    }
}
