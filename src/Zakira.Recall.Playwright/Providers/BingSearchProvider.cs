using Microsoft.Playwright;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Playwright.Browser;

namespace Zakira.Recall.Playwright.Providers;

public sealed class BingSearchProvider(IBrowserSessionFactory browserSessionFactory) : ISearchProvider
{
    public string Name => "bing";

    public async ValueTask<IReadOnlyList<SearchResult>> SearchAsync(SearchRequest request, ProfileDescriptor profile, CancellationToken cancellationToken = default)
    {
        await using var context = await browserSessionFactory.CreateContextAsync(profile, cancellationToken);
        var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
        var encodedQuery = Uri.EscapeDataString(request.Query);
        await page.GotoAsync($"https://www.bing.com/search?q={encodedQuery}", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = profile.TimeoutSeconds * 1000
        });

        await page.WaitForSelectorAsync("li.b_algo", new() { Timeout = profile.TimeoutSeconds * 1000 });
        var items = await page.Locator("li.b_algo").AllAsync();
        var results = new List<SearchResult>();

        foreach (var item in items)
        {
            if (results.Count >= request.MaxResults)
            {
                break;
            }

            var title = HtmlText.Normalize(await item.Locator("h2").TextContentAsync());
            var link = await item.Locator("h2 a").GetAttributeAsync("href");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
            {
                continue;
            }

            var snippet = HtmlText.Normalize(await item.Locator(".b_caption p").TextContentAsync());
            results.Add(new SearchResult
            {
                Title = title,
                Url = link,
                Snippet = snippet,
                Provider = Name,
                Rank = results.Count + 1,
                DisplayUrl = link
            });
        }

        return results;
    }
}
