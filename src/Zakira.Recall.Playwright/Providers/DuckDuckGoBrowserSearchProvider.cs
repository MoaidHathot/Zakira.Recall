using Microsoft.Playwright;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Playwright.Browser;

namespace Zakira.Recall.Playwright.Providers;

public sealed class DuckDuckGoBrowserSearchProvider(IBrowserSessionFactory browserSessionFactory) : ISearchProvider
{
    public string Name => "duckduckgo-browser";

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

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = profile.TimeoutSeconds * 1000 });
        var selectors = new[]
        {
            "article[data-testid='result']",
            "#links .result",
            ".react-results--main article"
        };

        ILocator? items = null;
        foreach (var selector in selectors)
        {
            var locator = page.Locator(selector);
            if (await locator.CountAsync() > 0)
            {
                items = locator;
                break;
            }
        }

        if (items is null)
        {
            return [];
        }

        var count = Math.Min(await items.CountAsync(), request.MaxResults);
        var results = new List<SearchResult>(count);
        for (var index = 0; index < count; index++)
        {
            var item = items.Nth(index);
            var title = HtmlText.Normalize(await item.Locator("h2, [data-testid='result-title-a']").First.TextContentAsync());
            var link = await item.Locator("a[href]").First.GetAttributeAsync("href");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
            {
                continue;
            }

            var snippet = HtmlText.Normalize(await GetOptionalTextAsync(item, "[data-result='snippet'], .result__snippet, [data-testid='result-snippet']"));
            results.Add(new SearchResult
            {
                Title = title,
                Url = NormalizeUrl(link),
                Snippet = snippet,
                Provider = Name,
                Rank = results.Count + 1,
                DisplayUrl = NormalizeDisplayUrl(link)
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

    private static string NormalizeUrl(string href)
        => href.StartsWith("//", StringComparison.Ordinal) ? $"https:{href}" : href;

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

    private static async Task<string?> GetOptionalTextAsync(ILocator item, string selector)
    {
        var locator = item.Locator(selector);
        return await locator.CountAsync() == 0 ? null : await locator.First.TextContentAsync();
    }
}
