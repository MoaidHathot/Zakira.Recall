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
        var timeoutMs = Math.Max(5, request.TimeoutSeconds > 0 ? request.TimeoutSeconds : profile.TimeoutSeconds) * 1000;
        await page.GotoAsync(request.Url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = timeoutMs
        });

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = timeoutMs });
        var snapshot = await page.EvaluateAsync<PageSnapshot>(
            """
            () => {
                const source = document.querySelector('article') || document.querySelector('main') || document.body;
                const clone = source ? source.cloneNode(true) : document.body.cloneNode(true);
                const noisySelectors = [
                    'script', 'style', 'noscript', 'svg', 'nav', 'header', 'footer', 'aside', 'iframe',
                    'form', 'button', 'dialog', '[role="navigation"]', '[aria-hidden="true"]',
                    '.sidebar', '.table-of-contents', '.toc', '.breadcrumbs', '.related', '.share', '.social', '.ads', '.advertisement'
                ];
                for (const selector of noisySelectors) {
                    for (const node of clone.querySelectorAll(selector)) {
                        node.remove();
                    }
                }

                const title = document.title || '';
                const siteName = document.querySelector('meta[property="og:site_name"]')?.getAttribute('content')
                    || document.querySelector('meta[name="application-name"]')?.getAttribute('content')
                    || location.hostname;
                const publishedAt = document.querySelector('meta[property="article:published_time"]')?.getAttribute('content')
                    || document.querySelector('meta[name="article:published_time"]')?.getAttribute('content')
                    || document.querySelector('time[datetime]')?.getAttribute('datetime')
                    || null;
                const text = (clone.innerText || '').replace(/\s+/g, ' ').trim();
                return { title, siteName, publishedAt, text };
            }
            """);

        var normalizedText = HtmlText.Normalize(snapshot.Text) ?? string.Empty;
        var excerpt = normalizedText.Length <= 400 ? normalizedText : normalizedText[..400];
        var finalUrl = page.Url;
        return new FetchResponse
        {
            Url = request.Url,
            FinalUrl = finalUrl,
            Success = true,
            Title = HtmlText.Normalize(snapshot.Title),
            Text = normalizedText,
            Excerpt = excerpt,
            Domain = Uri.TryCreate(finalUrl, UriKind.Absolute, out var uri) ? uri.Host : null,
            SiteName = HtmlText.Normalize(snapshot.SiteName),
            PublishedAt = DateTimeOffset.TryParse(snapshot.PublishedAt, out var publishedAt) ? publishedAt : null,
            WordCount = normalizedText.Length == 0 ? 0 : normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length
        };
    }

    private sealed class PageSnapshot
    {
        public string? Title { get; init; }

        public string? SiteName { get; init; }

        public string? PublishedAt { get; init; }

        public string? Text { get; init; }
    }
}
