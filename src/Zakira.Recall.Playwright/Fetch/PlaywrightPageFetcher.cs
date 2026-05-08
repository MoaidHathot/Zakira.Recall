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

        try
        {
            // Some sites keep polling in the background forever. Treat network idle as a best-effort settle step.
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = GetPostLoadWaitTimeoutMs(timeoutMs) });
        }
        catch (TimeoutException)
        {
        }

        await page.WaitForTimeoutAsync(250);
        var snapshot = await page.EvaluateAsync<PageSnapshot>(
            """
            () => {
                const createCleanClone = (source) => {
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

                    return clone;
                };

                const extractText = (source) => {
                    const clone = createCleanClone(source);
                    return (clone.innerText || '').replace(/\s+/g, ' ').trim();
                };

                const mainText = extractText(document.querySelector('article'))
                    || extractText(document.querySelector('main'))
                    || extractText(document.querySelector('[role="main"]'))
                    || extractText(document.querySelector('#__next'))
                    || extractText(document.querySelector('#root'))
                    || extractText(document.body);

                const metaDescription = document.querySelector('meta[name="description"]')?.getAttribute('content')
                    || document.querySelector('meta[property="og:description"]')?.getAttribute('content')
                    || document.querySelector('meta[name="twitter:description"]')?.getAttribute('content')
                    || null;

                const headline = document.querySelector('h1')?.innerText?.replace(/\s+/g, ' ').trim()
                    || document.querySelector('h2')?.innerText?.replace(/\s+/g, ' ').trim()
                    || null;

                const combinedText = [headline, metaDescription, mainText]
                    .filter(value => value && value.length > 0)
                    .join(' ')
                    .replace(/\s+/g, ' ')
                    .trim();

                const noisySelectors = [
                    'script', 'style', 'noscript', 'svg', 'nav', 'header', 'footer', 'aside', 'iframe',
                    'form', 'button', 'dialog', '[role="navigation"]', '[aria-hidden="true"]',
                    '.sidebar', '.table-of-contents', '.toc', '.breadcrumbs', '.related', '.share', '.social', '.ads', '.advertisement'
                ];

                const title = document.title || '';
                const siteName = document.querySelector('meta[property="og:site_name"]')?.getAttribute('content')
                    || document.querySelector('meta[name="application-name"]')?.getAttribute('content')
                    || location.hostname;
                const publishedAt = document.querySelector('meta[property="article:published_time"]')?.getAttribute('content')
                    || document.querySelector('meta[name="article:published_time"]')?.getAttribute('content')
                    || document.querySelector('time[datetime]')?.getAttribute('datetime')
                    || null;
                return { title, siteName, publishedAt, text: combinedText, metaDescription, headline };
            }
            """);

        var normalizedText = HtmlText.Normalize(snapshot.Text) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            normalizedText = HtmlText.Normalize(string.Join(' ', new[]
            {
                snapshot.Headline,
                snapshot.MetaDescription
            }.Where(static value => !string.IsNullOrWhiteSpace(value)))) ?? string.Empty;
        }

        var finalUrl = page.Url;
        var wordCount = normalizedText.Length == 0 ? 0 : normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var qualityError = CreateQualityError(normalizedText, wordCount, finalUrl);
        if (qualityError is not null)
        {
            return new FetchResponse
            {
                Url = request.Url,
                FinalUrl = finalUrl,
                Success = false,
                Title = HtmlText.Normalize(snapshot.Title),
                Text = normalizedText,
                Excerpt = normalizedText.Length <= 400 ? normalizedText : normalizedText[..400],
                Domain = Uri.TryCreate(finalUrl, UriKind.Absolute, out var qualityUri) ? qualityUri.Host : null,
                SiteName = HtmlText.Normalize(snapshot.SiteName),
                PublishedAt = DateTimeOffset.TryParse(snapshot.PublishedAt, out var qualityPublishedAt) ? qualityPublishedAt : null,
                WordCount = wordCount,
                Error = qualityError
            };
        }

        var excerpt = normalizedText.Length <= 400 ? normalizedText : normalizedText[..400];
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
            WordCount = wordCount
        };
    }

    internal static int GetPostLoadWaitTimeoutMs(int timeoutMs)
        => Math.Clamp(timeoutMs, 250, 5000);

    internal static OperationError? CreateQualityError(string text, int wordCount, string target)
    {
        if (ContainsAny(text,
            "unfortunately, bots use duckduckgo too",
            "please complete the following challenge",
            "our systems have detected unusual traffic",
            "verify you are a human"))
        {
            return new OperationError
            {
                Code = "fetch_bot_challenge",
                Message = "Fetched page appears to be a bot challenge.",
                Target = target,
                Transient = true
            };
        }

        if (ContainsAny(text,
            "join linkedin",
            "log into facebook",
            "log in to facebook",
            "sign in to continue",
            "sign up to continue",
            "create an account or sign in"))
        {
            return new OperationError
            {
                Code = "fetch_login_required",
                Message = "Fetched page appears to require login or account creation.",
                Target = target,
                Transient = false
            };
        }

        if (wordCount < 15)
        {
            return new OperationError
            {
                Code = "fetch_weak_content",
                Message = "Fetched page did not contain enough readable content.",
                Target = target,
                Transient = false
            };
        }

        return null;
    }

    private static bool ContainsAny(string text, params string[] markers)
        => markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private sealed class PageSnapshot
    {
        public string? Title { get; init; }

        public string? SiteName { get; init; }

        public string? PublishedAt { get; init; }

        public string? Text { get; init; }

        public string? MetaDescription { get; init; }

        public string? Headline { get; init; }
    }
}
