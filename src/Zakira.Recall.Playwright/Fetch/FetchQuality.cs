using System.Net;
using Zakira.Recall.Abstractions.Models;

namespace Zakira.Recall.Playwright.Fetch;

/// <summary>
/// Decides whether a rendered page is usable content or one of the well-known failure pages
/// (bot challenges, HTTP errors, login walls, empty shells).
/// </summary>
internal static class FetchQuality
{
    internal const int MinimumWordCount = 15;

    /// <summary>
    /// Phrases from interstitials of DuckDuckGo, Google, Cloudflare (challenge and block pages), Akamai, PerimeterX,
    /// Imperva/Incapsula and Distil. Matched against title and text, case-insensitively.
    /// </summary>
    private static readonly string[] BotChallengeMarkers =
    [
        "unfortunately, bots use duckduckgo too",
        "please complete the following challenge",
        "our systems have detected unusual traffic",
        "verify you are a human",
        "verifying you are human",
        "just a moment...",
        "checking your browser before accessing",
        "checking if the site connection is secure",
        "enable javascript and cookies to continue",
        "attention required! | cloudflare",
        "sorry, you have been blocked",
        "cloudflare ray id",
        "you don't have permission to access",
        "press & hold to confirm you are a human",
        "request unsuccessful. incapsula incident id",
        "pardon our interruption"
    ];

    private static readonly string[] LoginMarkers =
    [
        "join linkedin",
        "log into facebook",
        "log in to facebook",
        "sign in to continue",
        "sign up to continue",
        "create an account or sign in"
    ];

    public static OperationError? Evaluate(string? title, string text, int wordCount, int? statusCode, string target)
    {
        if (ContainsAny(title, BotChallengeMarkers) || ContainsAny(text, BotChallengeMarkers))
        {
            return new OperationError
            {
                Code = "fetch_bot_challenge",
                Message = statusCode is null
                    ? "Fetched page appears to be a bot challenge."
                    : $"Fetched page appears to be a bot challenge (HTTP {statusCode}).",
                Target = target,
                Transient = true
            };
        }

        if (statusCode is >= 400)
        {
            // The text is still returned so callers can inspect the error page, but it is not the requested content.
            return new OperationError
            {
                Code = "fetch_http_error",
                Message = $"Server returned HTTP {statusCode}{DescribeStatus(statusCode.Value)}.",
                Target = target,
                Transient = IsTransientStatus(statusCode.Value)
            };
        }

        if (ContainsAny(text, LoginMarkers))
        {
            return new OperationError
            {
                Code = "fetch_login_required",
                Message = "Fetched page appears to require login or account creation.",
                Target = target,
                Transient = false
            };
        }

        if (wordCount < MinimumWordCount)
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

    /// <summary>Timeouts, rate limits and server-side failures are worth retrying; client errors are not.</summary>
    internal static bool IsTransientStatus(int statusCode)
        => statusCode is 408 or 425 or 429 or >= 500;

    private static string DescribeStatus(int statusCode)
        => Enum.IsDefined(typeof(HttpStatusCode), statusCode) ? $" ({(HttpStatusCode)statusCode})" : string.Empty;

    private static bool ContainsAny(string? text, string[] markers)
        => !string.IsNullOrEmpty(text) && markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
