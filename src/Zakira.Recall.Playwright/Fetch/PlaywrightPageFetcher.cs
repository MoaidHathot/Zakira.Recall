using Microsoft.Playwright;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Playwright.Browser;
using Zakira.Recall.Playwright.Providers;

namespace Zakira.Recall.Playwright.Fetch;

public sealed class PlaywrightPageFetcher(IBrowserSessionFactory browserSessionFactory, IContentExtractor contentExtractor) : IPageFetcher
{
    private const int ExcerptLength = 400;

    public async ValueTask<FetchResponse> FetchAsync(FetchRequest request, ProfileDescriptor profile, CancellationToken cancellationToken = default)
    {
        try
        {
            return await FetchOnceAsync(request, profile, cancellationToken);
        }
        catch (PlaywrightException ex) when (PlaywrightErrors.IsDriverProcessExited(ex))
        {
            // The shared driver process died mid-fetch. The session factory starts a new one on the next launch,
            // so a single retry recovers instead of failing every fetch until the host restarts.
            return await FetchOnceAsync(request, profile, cancellationToken);
        }
    }

    private async Task<FetchResponse> FetchOnceAsync(FetchRequest request, ProfileDescriptor profile, CancellationToken cancellationToken)
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

        // Serialize the rendered DOM (after scripts ran) and extract in-process, where the logic is testable.
        var html = await page.ContentAsync();
        var finalUrl = page.Url;
        var content = contentExtractor.Extract(html, finalUrl);

        var text = content.Text;
        var wordCount = content.WordCount;
        var qualityError = CreateQualityError(text, wordCount, finalUrl);
        return new FetchResponse
        {
            Url = request.Url,
            FinalUrl = finalUrl,
            Success = qualityError is null,
            Title = HtmlText.Normalize(content.Title),
            Text = text,
            Excerpt = CreateExcerpt(text),
            Domain = Uri.TryCreate(finalUrl, UriKind.Absolute, out var uri) ? uri.Host : null,
            SiteName = HtmlText.Normalize(content.SiteName),
            PublishedAt = DateTimeOffset.TryParse(content.PublishedAt, out var publishedAt) ? publishedAt : null,
            WordCount = wordCount,
            ContentSelector = content.ContentSelector,
            Error = qualityError
        };
    }

    internal static int GetPostLoadWaitTimeoutMs(int timeoutMs)
        => Math.Clamp(timeoutMs, 250, 5000);

    /// <summary>The excerpt is a single line so compact outputs (CLI text mode, citations) stay readable.</summary>
    internal static string? CreateExcerpt(string text)
    {
        var singleLine = HtmlText.Normalize(text);
        if (singleLine is null)
        {
            return null;
        }

        return singleLine.Length <= ExcerptLength ? singleLine : singleLine[..ExcerptLength];
    }

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
}
