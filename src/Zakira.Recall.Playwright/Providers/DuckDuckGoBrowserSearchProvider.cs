using Microsoft.Playwright;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Playwright.Browser;

namespace Zakira.Recall.Playwright.Providers;

public sealed class DuckDuckGoBrowserSearchProvider(IBrowserSessionFactory browserSessionFactory) : ISearchProvider
{
    private static readonly string[] ResultSelectors =
    [
        "article[data-testid='result']",
        "#links .result.results_links",
        ".react-results--main article[data-testid='result']",
        "#links .result",
        "[data-testid='result']"
    ];

    private static readonly string[] OrganicLinkSelectors =
    [
        "h2 a[href]",
        "a[data-testid='result-title-a'][href]",
        ".result__title a.result__a[href]",
        "a.result__a[href]"
    ];

    private static readonly string[] SnippetSelectors =
    [
        "[data-result='snippet']",
        ".result__snippet",
        "[data-testid='result-snippet']"
    ];

    private static readonly string[] DisplayUrlSelectors =
    [
        ".result__url",
        "[data-testid='result-extras-url-link']",
        "[data-testid='result-url']"
    ];

    public string Name => "duckduckgo-browser";

    public string? SetupUrl => "https://duckduckgo.com";

    public SearchProviderCapabilities Capabilities => new()
    {
        SupportsPagination = true,
        SupportsTimeRange = true,
        SupportsSafeSearch = true,
        RequiresBrowser = true,
        SupportsInteractiveSetup = true
    };

    public async ValueTask<IReadOnlyList<SearchResult>> SearchAsync(SearchRequest request, ProfileDescriptor profile, CancellationToken cancellationToken = default)
    {
        await using var context = await browserSessionFactory.CreateContextAsync(profile, cancellationToken);
        var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
        await page.GotoAsync(BuildSearchUrl(request), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = profile.TimeoutSeconds * 1000
        });

        if (await IsBotChallengePageAsync(page))
        {
            throw new InvalidOperationException("DuckDuckGo browser search was blocked by a bot challenge.");
        }

        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = GetPostLoadWaitTimeoutMs(profile.TimeoutSeconds * 1000) });
        }
        catch (TimeoutException)
        {
        }

        await page.WaitForTimeoutAsync(250);
        var items = await FindResultsLocatorAsync(page);

        if (items is null)
        {
            return [];
        }

        var count = Math.Min(await items.CountAsync(), request.MaxResults);
        var results = new List<SearchResult>(count);
        for (var index = 0; index < count; index++)
        {
            var item = items.Nth(index);
            var linkLocator = await FindFirstLocatorAsync(item, OrganicLinkSelectors);
            if (linkLocator is null)
            {
                continue;
            }

            var title = HtmlText.Normalize(await linkLocator.TextContentAsync());
            var link = await linkLocator.GetAttributeAsync("href");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
            {
                continue;
            }

            var normalizedUrl = NormalizeUrl(link);
            var canonicalUrl = CanonicalizeUrl(normalizedUrl);
            var host = TryGetHost(canonicalUrl ?? normalizedUrl);
            var displayUrl = await GetOptionalTextAsync(item, DisplayUrlSelectors) ?? NormalizeDisplayUrl(canonicalUrl ?? normalizedUrl);
            var snippet = await GetOptionalTextAsync(item, SnippetSelectors);
            var rank = results.Count + 1;
            results.Add(new SearchResult
            {
                Title = title,
                Url = normalizedUrl,
                CanonicalUrl = canonicalUrl,
                Host = host,
                DisplayUrl = displayUrl,
                Snippet = snippet,
                Provider = Name,
                Rank = rank,
                RawRank = index + 1,
                QualityScore = ScoreResult(title, snippet, host),
                QueryVariant = null,
                SourceProviders = [Name]
            });
        }

        return results;
    }

    private static string BuildSearchUrl(SearchRequest request)
    {
        var parameters = new List<string>
        {
            $"q={Uri.EscapeDataString(request.Query)}",
            "ia=web"
        };

        if (request.Page > 1)
        {
            parameters.Add($"s={(request.Page - 1) * Math.Max(1, request.MaxResults)}");
        }

        if (!string.IsNullOrWhiteSpace(request.TimeRange))
        {
            parameters.Add($"df={MapDuckDuckGoTimeRange(request.TimeRange)}");
        }

        if (request.SafeSearch.HasValue)
        {
            parameters.Add($"kp={(request.SafeSearch.Value ? "1" : "-1")}");
        }

        return $"https://duckduckgo.com/?{string.Join("&", parameters)}";
    }

    private static async Task<ILocator?> FindResultsLocatorAsync(IPage page)
    {
        foreach (var selector in ResultSelectors)
        {
            var locator = page.Locator(selector);
            if (await locator.CountAsync() > 0)
            {
                return locator;
            }
        }

        return null;
    }

    private static async Task<ILocator?> FindFirstLocatorAsync(ILocator item, IReadOnlyList<string> selectors)
    {
        foreach (var selector in selectors)
        {
            var locator = item.Locator(selector);
            if (await locator.CountAsync() > 0)
            {
                return locator.First;
            }
        }

        return null;
    }

    private static string NormalizeUrl(string href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return href;
        }

        var absoluteHref = href.StartsWith("//", StringComparison.Ordinal)
            ? $"https:{href}"
            : href.StartsWith("/", StringComparison.Ordinal)
                ? $"https://duckduckgo.com{href}"
                : href;

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

    private static string NormalizeDisplayUrl(string href)
        => Uri.TryCreate(NormalizeUrl(href), UriKind.Absolute, out var uri) ? uri.Host + uri.PathAndQuery : href;

    private static string MapDuckDuckGoTimeRange(string timeRange)
        => timeRange.Trim().ToLowerInvariant() switch
        {
            "day" => "d",
            "week" => "w",
            "month" => "m",
            "year" => "y",
            _ => timeRange.Trim().ToLowerInvariant()
        };

    private static async Task<string?> GetOptionalTextAsync(ILocator item, IReadOnlyList<string> selectors)
    {
        foreach (var selector in selectors)
        {
            var locator = item.Locator(selector);
            if (await locator.CountAsync() == 0)
            {
                continue;
            }

            var text = HtmlText.Normalize(await locator.First.TextContentAsync());
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    private static async Task<bool> IsBotChallengePageAsync(IPage page)
    {
        var bodyText = (await page.Locator("body").TextContentAsync() ?? string.Empty).ToLowerInvariant();
        return bodyText.Contains("bots use duckduckgo too", StringComparison.Ordinal)
            || bodyText.Contains("confirm this search was made by a human", StringComparison.Ordinal)
            || bodyText.Contains("select all squares containing a duck", StringComparison.Ordinal);
    }

    private static int GetPostLoadWaitTimeoutMs(int timeoutMs)
        => Math.Clamp(timeoutMs, 250, 5000);

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

        var canonicalUrl = builder.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return string.IsNullOrWhiteSpace(canonicalUrl) ? builder.Uri.GetLeftPart(UriPartial.Path) : canonicalUrl;
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
