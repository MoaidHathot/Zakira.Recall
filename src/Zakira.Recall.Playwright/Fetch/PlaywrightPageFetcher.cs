using Microsoft.Playwright;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Playwright.Browser;
using Zakira.Recall.Playwright.Providers;

namespace Zakira.Recall.Playwright.Fetch;

public sealed class PlaywrightPageFetcher(IBrowserSessionFactory browserSessionFactory) : IPageFetcher
{
    public async ValueTask<FetchResponse> FetchAsync(FetchRequest request, ProfileDescriptor profile, CancellationToken cancellationToken = default)
    {
        await using var context = await browserSessionFactory.CreateContextAsync(profile, cancellationToken);
        var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
        await page.GotoAsync(request.Url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = request.TimeoutSeconds * 1000
        });

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = request.TimeoutSeconds * 1000 });
        var title = HtmlText.Normalize(await page.TitleAsync());
        var text = await page.EvaluateAsync<string>(@"() => {
            const article = document.querySelector('main') || document.querySelector('article') || document.body;
            return (article?.innerText || '').replace(/\s+/g, ' ').trim();
        }");

        var normalizedText = HtmlText.Normalize(text) ?? string.Empty;
        return new FetchResponse
        {
            Url = request.Url,
            FinalUrl = page.Url,
            Title = title,
            Text = normalizedText,
            Excerpt = normalizedText.Length <= 400 ? normalizedText : normalizedText[..400]
        };
    }
}
