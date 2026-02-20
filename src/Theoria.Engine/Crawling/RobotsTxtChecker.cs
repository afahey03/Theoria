using System.Collections.Concurrent;

namespace Theoria.Engine.Crawling;

/// <summary>
/// Fetches, parses, and caches robots.txt files for polite crawling.
/// Respects Disallow/Allow directives using longest-match-wins precedence
/// (per Google's robots.txt specification).
///
/// Thread-safe: results are cached per host using ConcurrentDictionary.
/// If robots.txt cannot be fetched (timeout, 404, network error), crawling is allowed.
/// </summary>
public sealed class RobotsTxtChecker
{
    private readonly HttpClient _httpClient;
    private readonly string _userAgent;
    private readonly ConcurrentDictionary<string, RobotRules> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Short timeout for fetching robots.txt — don't block crawls on slow responses.</summary>
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(3);

    public RobotsTxtChecker(HttpClient httpClient, string userAgent = "theoria")
    {
        _httpClient = httpClient;
        _userAgent = userAgent.ToLowerInvariant();
    }

    /// <summary>
    /// Checks whether the given URL is allowed by that domain's robots.txt.
    /// Returns true if allowed (or if robots.txt is unavailable/malformed).
    /// </summary>
    public async Task<bool> IsAllowedAsync(string url, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return true;

        var host = uri.Host;
        var rules = await GetRulesAsync(host, uri.Scheme, ct);
        return rules.IsAllowed(uri.AbsolutePath);
    }

    private async Task<RobotRules> GetRulesAsync(string host, string scheme, CancellationToken ct)
    {
        if (_cache.TryGetValue(host, out var cached))
            return cached;

        var rules = await FetchAndParseAsync(host, scheme, ct);
        _cache.TryAdd(host, rules);
        return rules;
    }

    private async Task<RobotRules> FetchAndParseAsync(string host, string scheme, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(FetchTimeout);

            var robotsUrl = $"{scheme}://{host}/robots.txt";
            var response = await _httpClient.GetAsync(robotsUrl, cts.Token);

            if (!response.IsSuccessStatusCode)
                return RobotRules.AllowAll;

            var content = await response.Content.ReadAsStringAsync(cts.Token);
            return RobotRules.Parse(content, _userAgent);
        }
        catch
        {
            // If we can't fetch robots.txt, assume everything is allowed
            return RobotRules.AllowAll;
        }
    }
}

/// <summary>
/// Parsed robots.txt rules for a specific user-agent.
/// Implements longest-match-wins precedence between Allow and Disallow directives.
/// Supports wildcards (*) and end-of-URL anchors ($) per the de facto standard.
/// </summary>
internal sealed class RobotRules
{
    public static readonly RobotRules AllowAll = new([], []);

    private readonly List<string> _disallowed;
    private readonly List<string> _allowed;

    private RobotRules(List<string> disallowed, List<string> allowed)
    {
        _disallowed = disallowed;
        _allowed = allowed;
    }

    /// <summary>
    /// Determines whether the given URL path is allowed.
    /// Uses longest-match-wins: if both Allow and Disallow match, the longer pattern wins.
    /// If lengths are equal, Allow takes precedence.
    /// </summary>
    public bool IsAllowed(string path)
    {
        string? bestAllow = null;
        string? bestDisallow = null;

        foreach (var pattern in _allowed)
        {
            if (PathMatches(path, pattern))
            {
                if (bestAllow is null || pattern.Length > bestAllow.Length)
                    bestAllow = pattern;
            }
        }

        foreach (var pattern in _disallowed)
        {
            if (PathMatches(path, pattern))
            {
                if (bestDisallow is null || pattern.Length > bestDisallow.Length)
                    bestDisallow = pattern;
            }
        }

        if (bestAllow is null && bestDisallow is null) return true;
        if (bestAllow is not null && bestDisallow is null) return true;
        if (bestAllow is null && bestDisallow is not null) return false;

        // Both matched — longer pattern wins; equal length = Allow wins
        return bestAllow!.Length >= bestDisallow!.Length;
    }

    private static bool PathMatches(string path, string pattern)
    {
        // Empty disallow means allow all
        if (string.IsNullOrEmpty(pattern)) return false;

        // "$" anchor: exact path match (minus the $)
        if (pattern.EndsWith('$'))
        {
            return path.Equals(pattern[..^1], StringComparison.OrdinalIgnoreCase);
        }

        // "*" wildcard: simple glob matching
        if (pattern.Contains('*'))
        {
            var parts = pattern.Split('*');
            int pos = 0;
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                var idx = path.IndexOf(part, pos, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) return false;
                pos = idx + part.Length;
            }
            return true;
        }

        // Default: prefix match
        return path.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses robots.txt content for the given target user-agent.
    /// Falls back to wildcard (*) rules if no specific section matches.
    /// </summary>
    public static RobotRules Parse(string content, string targetUserAgent)
    {
        var lines = content.Split('\n', StringSplitOptions.TrimEntries);

        var disallowed = new List<string>();
        var allowed = new List<string>();
        var globalDisallowed = new List<string>();
        var globalAllowed = new List<string>();

        bool inMatchingSection = false;
        bool inGlobalSection = false;
        bool foundSpecificSection = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine;
            var commentIdx = line.IndexOf('#');
            if (commentIdx >= 0) line = line[..commentIdx];
            line = line.Trim();

            if (string.IsNullOrEmpty(line)) continue;

            if (line.StartsWith("User-agent:", StringComparison.OrdinalIgnoreCase))
            {
                var agent = line["User-agent:".Length..].Trim().ToLowerInvariant();

                if (agent == "*")
                {
                    inGlobalSection = true;
                    inMatchingSection = false;
                }
                else if (agent == targetUserAgent || targetUserAgent.Contains(agent))
                {
                    inMatchingSection = true;
                    inGlobalSection = false;
                    foundSpecificSection = true;
                }
                else
                {
                    inMatchingSection = false;
                    inGlobalSection = false;
                }
            }
            else if (line.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase))
            {
                var path = line["Disallow:".Length..].Trim();
                if (!string.IsNullOrEmpty(path))
                {
                    if (inMatchingSection) disallowed.Add(path);
                    else if (inGlobalSection) globalDisallowed.Add(path);
                }
            }
            else if (line.StartsWith("Allow:", StringComparison.OrdinalIgnoreCase))
            {
                var path = line["Allow:".Length..].Trim();
                if (!string.IsNullOrEmpty(path))
                {
                    if (inMatchingSection) allowed.Add(path);
                    else if (inGlobalSection) globalAllowed.Add(path);
                }
            }
        }

        if (!foundSpecificSection)
        {
            return new RobotRules(globalDisallowed, globalAllowed);
        }

        return new RobotRules(disallowed, allowed);
    }
}
