using System.Net;
using System.Text.RegularExpressions;
using Zakira.Recall.Abstractions.Models;

namespace Zakira.Recall.Playwright.Providers;

internal static class DuckDuckGoHtmlParser
{
    private static readonly Regex ResultPattern = new("<div\\s+class=\"(?<class>[^\"]*)\"[^>]*>(?<content>.*?)<div\\s+class=\"clear\"></div>\\s*</div>\\s*</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex TitlePattern = new("<a[^>]*class=\"[^\"]*\\bresult__a\\b[^\"]*\"[^>]*href=\"(?<href>[^\"]+)\"[^>]*>(?<text>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex DisplayUrlPattern = new("<(?:a|span)[^>]*class=\"[^\"]*\\bresult__url\\b[^\"]*\"[^>]*>(?<text>.*?)</(?:a|span)>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex SnippetPattern = new("<(?:a|div|span)[^>]*class=\"[^\"]*\\bresult__snippet\\b[^\"]*\"[^>]*>(?<text>.*?)</(?:a|div|span)>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex TagPattern = new("<.*?>", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex OrganicClassPattern = new("\\b(results_links|web-result|result__body|links_main)\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<SearchResult> ParseResults(string html, int maxResults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);

        var results = new List<SearchResult>();
        var segments = SplitIntoResultSegments(html);

        foreach (var segment in segments)
        {
            if (results.Count >= maxResults)
            {
                break;
            }

            var titleMatch = TitlePattern.Match(segment);
            if (!titleMatch.Success)
            {
                continue;
            }

            var title = NormalizeText(titleMatch.Groups["text"].Value);
            var url = NormalizeUrl(WebUtility.HtmlDecode(titleMatch.Groups["href"].Value));
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            var canonicalUrl = CanonicalizeUrl(url);
            var host = TryGetHost(canonicalUrl ?? url);
            var displayUrl = NormalizeText(DisplayUrlPattern.Match(segment).Groups["text"].Value) ?? url;
            var snippet = NormalizeText(SnippetPattern.Match(segment).Groups["text"].Value);
            var rank = results.Count + 1;
            results.Add(new SearchResult
            {
                Title = title,
                Url = url,
                CanonicalUrl = canonicalUrl,
                Host = host,
                DisplayUrl = displayUrl,
                Snippet = snippet,
                Provider = "duckduckgo",
                Rank = rank,
                RawRank = rank,
                QualityScore = ScoreResult(title, snippet, host),
                QueryVariant = null,
                SourceProviders = ["duckduckgo"]
            });
        }

        return results;
    }

    private static IReadOnlyList<string> SplitIntoResultSegments(string html)
    {
        var matches = ResultPattern.Matches(html);
        var segments = new List<string>();

        foreach (Match match in matches)
        {
            var classValue = match.Groups["class"].Value;
            if (!ContainsCssClass(classValue, "result") || !OrganicClassPattern.IsMatch(classValue))
            {
                continue;
            }

            segments.Add(match.Groups["content"].Value);
        }

        return segments;
    }

    private static string? NormalizeUrl(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var absoluteHref = href.StartsWith("//", StringComparison.Ordinal) ? $"https:{href}" : href;
        if (!Uri.TryCreate(absoluteHref, UriKind.Absolute, out var uri))
        {
            return href;
        }

        if (uri.Host.EndsWith("duckduckgo.com", StringComparison.OrdinalIgnoreCase))
        {
            var redirectTarget = GetQueryValue(uri.Query, "uddg");
            if (!string.IsNullOrWhiteSpace(redirectTarget))
            {
                return redirectTarget;
            }
        }

        return uri.ToString();
    }

    private static string? NormalizeText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var decoded = WebUtility.HtmlDecode(TagPattern.Replace(html, " "));
        return HtmlText.Normalize(decoded);
    }

    private static bool ContainsCssClass(string classValue, string className)
        => classValue.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(className, StringComparer.Ordinal);

    private static string? CanonicalizeUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty,
            Host = uri.Host.ToLowerInvariant()
        };

        if ((builder.Scheme == Uri.UriSchemeHttps && builder.Port == 443)
            || (builder.Scheme == Uri.UriSchemeHttp && builder.Port == 80))
        {
            builder.Port = -1;
        }

        return builder.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }

    private static string? TryGetHost(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;

    private static int ScoreResult(string title, string? snippet, string? host)
    {
        var score = title.Length * 2;
        if (!string.IsNullOrWhiteSpace(snippet))
        {
            score += Math.Min(snippet.Length, 200);
        }

        if (!string.IsNullOrWhiteSpace(host))
        {
            score += 25;
        }

        return score;
    }

    private static string? GetQueryValue(string query, string key)
    {
        foreach (var segment in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = segment.IndexOf('=');
            var rawName = separatorIndex >= 0 ? segment[..separatorIndex] : segment;
            if (!string.Equals(Uri.UnescapeDataString(rawName), key, StringComparison.Ordinal))
            {
                continue;
            }

            var rawValue = separatorIndex >= 0 ? segment[(separatorIndex + 1)..] : string.Empty;
            return Uri.UnescapeDataString(rawValue);
        }

        return null;
    }
}
