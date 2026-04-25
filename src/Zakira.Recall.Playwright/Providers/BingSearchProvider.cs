using Microsoft.Playwright;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Playwright.Browser;

namespace Zakira.Recall.Playwright.Providers;

public sealed class BingSearchProvider(IBrowserSessionFactory browserSessionFactory) : ISearchProvider
{
    private static readonly string[] SnippetSelectors = [".b_caption p", ".b_algoSlug", ".b_lineclamp2", ".b_caption"];

    public string Name => "bing";

    public string? SetupUrl => "https://www.bing.com";

    public SearchProviderCapabilities Capabilities => new()
    {
        SupportsPagination = true,
        SupportsTimeRange = false,
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

        var itemsLocator = page.Locator("li.b_algo");
        try
        {
            await itemsLocator.First.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = profile.TimeoutSeconds * 1000 });
        }
        catch (TimeoutException)
        {
            return [];
        }

        await page.WaitForTimeoutAsync(250);
        var items = await itemsLocator.AllAsync();
        var results = new List<SearchResult>();
        var queryTokens = TokenizeQuery(request.Query);

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

            var snippet = await GetOptionalTextAsync(item, SnippetSelectors);
            var normalizedUrl = NormalizeUrl(link);
            if (!LooksRelevant(title, snippet, normalizedUrl, queryTokens))
            {
                continue;
            }

            var canonicalUrl = CanonicalizeUrl(normalizedUrl);
            var host = TryGetHost(canonicalUrl ?? normalizedUrl);
            var rank = results.Count + 1;

            results.Add(new SearchResult
            {
                Title = title,
                Url = normalizedUrl,
                CanonicalUrl = canonicalUrl,
                Host = host,
                DisplayUrl = NormalizeDisplayUrl(canonicalUrl ?? normalizedUrl),
                Snippet = snippet,
                Provider = Name,
                Rank = rank,
                RawRank = results.Count + 1,
                QualityScore = ScoreResult(title, snippet, host, queryTokens),
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
            $"count={Math.Max(1, request.MaxResults)}"
        };

        if (request.Page > 1)
        {
            parameters.Add($"first={((request.Page - 1) * Math.Max(1, request.MaxResults)) + 1}");
        }

        if (request.SafeSearch.HasValue)
        {
            parameters.Add($"adlt={(request.SafeSearch.Value ? "strict" : "off")}");
        }

        return $"https://www.bing.com/search?{string.Join("&", parameters)}";
    }

    private static string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        var target = GetQueryValue(uri.Query, "u");
        if (!string.IsNullOrWhiteSpace(target))
        {
            var decoded = Uri.UnescapeDataString(target);
            if (TryDecodeBingTarget(decoded, out var decodedTarget))
            {
                return decodedTarget;
            }
        }

        return url;
    }

    private static bool TryDecodeBingTarget(string encodedTarget, out string decodedTarget)
    {
        decodedTarget = string.Empty;

        if (Uri.TryCreate(encodedTarget, UriKind.Absolute, out var directUri))
        {
            decodedTarget = directUri.ToString();
            return true;
        }

        if (encodedTarget.Length <= 2 || encodedTarget[1] != '1')
        {
            return false;
        }

        var payload = encodedTarget[2..];
        payload = payload.Replace('-', '+').Replace('_', '/');
        var padding = payload.Length % 4;
        if (padding > 0)
        {
            payload = payload.PadRight(payload.Length + (4 - padding), '=');
        }

        try
        {
            var bytes = Convert.FromBase64String(payload);
            var candidate = System.Text.Encoding.UTF8.GetString(bytes);
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var decodedUri))
            {
                decodedTarget = decodedUri.ToString();
                return true;
            }
        }
        catch (FormatException)
        {
        }

        return false;
    }

    private static string NormalizeDisplayUrl(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host + uri.PathAndQuery : url;

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

    private static string[] TokenizeQuery(string query)
        => query.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static token => token.Length >= 3)
            .Select(static token => token.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static bool LooksRelevant(string title, string? snippet, string url, IReadOnlyList<string> queryTokens)
    {
        if (queryTokens.Count == 0)
        {
            return true;
        }

        var haystack = string.Join(' ', new[] { title, snippet ?? string.Empty, url }).ToLowerInvariant();
        return queryTokens.Any(token => haystack.Contains(token, StringComparison.Ordinal));
    }

    private static int ScoreResult(string title, string? snippet, string? host, IReadOnlyList<string> queryTokens)
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

        var haystack = string.Join(' ', new[] { title, snippet ?? string.Empty, host ?? string.Empty }).ToLowerInvariant();
        score += queryTokens.Count(token => haystack.Contains(token, StringComparison.Ordinal)) * 50;
        return score;
    }

    private static string? GetQueryValue(string query, string key)
    {
        var trimmed = query.TrimStart('?');
        if (trimmed.Length == 0)
        {
            return null;
        }

        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var normalizedKey = parts[0].TrimStart('!');
            if (parts.Length == 2 && string.Equals(normalizedKey, key, StringComparison.OrdinalIgnoreCase))
            {
                return parts[1];
            }
        }

        return null;
    }
}
