using Microsoft.Extensions.Logging;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Core.Services;

public sealed class ResearchService(ISearchService searchService, IFetchService fetchService, IProfileResolver profileResolver, ILogger<ResearchService> logger) : IResearchService
{
    public async ValueTask<ResearchResponse> ResearchAsync(ResearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Query);

        var profile = await profileResolver.ResolveAsync(request.Profile, request.Provider, cancellationToken);
        ResearchServiceLogging.ResearchStarting(logger, request.Query, profile.DefaultProvider, profile.Name);

        var searchResponse = await searchService.SearchAsync(new SearchRequest
        {
            Query = request.Query,
            Profile = request.Profile,
            Provider = request.Provider,
            MaxResults = request.MaxResults,
            Page = request.Page,
            TimeRange = request.TimeRange,
            SafeSearch = request.SafeSearch,
            EnableFallback = request.EnableFallback,
            FallbackProviders = request.FallbackProviders
        }, cancellationToken);

        var topResults = SelectResults(request.Query, searchResponse.Results, request.TopPagesToRead, request.EnforceDomainDiversity);
        var errors = new List<OperationError>();
        if (searchResponse.Error is not null)
        {
            errors.Add(searchResponse.Error);
        }

        var maxConcurrency = Math.Clamp(request.MaxConcurrentFetches ?? profile.MaxConcurrentFetches, 1, 16);
        using var gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var fetchTasks = topResults.Select(async result =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var fetch = await fetchService.FetchAsync(new FetchRequest
                {
                    Url = result.Url,
                    Profile = request.Profile
                }, cancellationToken);

                if (fetch.Success)
                {
                    ResearchServiceLogging.ResearchFetchSucceeded(logger, result.Url);
                }
                else if (fetch.Error is not null)
                {
                    ResearchServiceLogging.ResearchFetchFailed(logger, new InvalidOperationException(fetch.Error.Message), result.Url);
                }

                return (result, fetch);
            }
            catch (Exception ex)
            {
                ResearchServiceLogging.ResearchFetchFailed(logger, ex, result.Url);
                return (result, new FetchResponse
                {
                    Url = result.Url,
                    FinalUrl = result.Url,
                    Success = false,
                    Error = ServiceErrors.FromException("fetch_failed", ex.Message, ex, target: result.Url)
                });
            }
            finally
            {
                gate.Release();
            }
        }).ToArray();

        var fetches = await Task.WhenAll(fetchTasks);
        var selectedFetches = fetches
            .OrderByDescending(item => ScoreFetchedResult(item.Item1, item.Item2))
            .Take(Math.Clamp(request.TopPagesToRead, 1, Math.Max(1, fetches.Length)))
            .ToArray();

        var sources = new List<ResearchSource>(selectedFetches.Length);
        var citations = new List<ResearchCitation>(selectedFetches.Length);
        for (var index = 0; index < selectedFetches.Length; index++)
        {
            var (result, fetch) = selectedFetches[index];
            if (fetch.Error is not null)
            {
                errors.Add(fetch.Error);
            }

            var citationId = $"src-{index + 1}";
            citations.Add(new ResearchCitation
            {
                Id = citationId,
                Title = fetch.Title ?? result.Title,
                Url = fetch.FinalUrl,
                Domain = fetch.Domain ?? TryGetDomain(fetch.FinalUrl),
                Provider = result.Provider,
                Rank = result.Rank,
                Quote = fetch.Excerpt,
                PublishedAt = fetch.PublishedAt
            });

            sources.Add(new ResearchSource
            {
                CitationId = citationId,
                SearchResult = result,
                Fetch = fetch
            });
        }

        var strongSources = sources.Where(source => !IsWeakFetch(source.Fetch)).ToArray();
        var summary = BuildSummary(request.Query, strongSources);

        return new ResearchResponse
        {
            Query = request.Query,
            Provider = searchResponse.Provider,
            Profile = searchResponse.Profile,
            Success = strongSources.Length > 0,
            Summary = summary,
            SearchResults = searchResponse.Results,
            Sources = sources,
            Citations = citations,
            Errors = errors
        };
    }

    private static SearchResult[] SelectResults(string query, IReadOnlyList<SearchResult> results, int topPagesToRead, bool enforceDomainDiversity)
    {
        var targetCount = Math.Clamp(topPagesToRead, 1, Math.Max(1, results.Count));
        var uniqueResults = DedupeResults(results)
            .OrderByDescending(result => ScoreSearchResult(query, result))
            .ThenBy(result => result.Rank)
            .ToList();
        if (!enforceDomainDiversity)
        {
            return uniqueResults.Take(targetCount).ToArray();
        }

        var selected = new List<SearchResult>(Math.Min(targetCount, uniqueResults.Count));
        var usedDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in uniqueResults)
        {
            var domain = TryGetDomain(result.Url);
            if (!string.IsNullOrWhiteSpace(domain) && !usedDomains.Add(domain))
            {
                continue;
            }

            selected.Add(result);
            if (selected.Count == targetCount)
            {
                return selected.ToArray();
            }
        }

        foreach (var result in uniqueResults)
        {
            if (selected.Count == targetCount)
            {
                break;
            }

            if (!selected.Contains(result))
            {
                selected.Add(result);
            }
        }

        return selected.ToArray();
    }

    private static int ScoreSearchResult(string query, SearchResult result)
    {
        var score = 0;
        var domain = TryGetDomain(result.Url) ?? string.Empty;
        var title = result.Title ?? string.Empty;
        var snippet = result.Snippet ?? string.Empty;
        var haystack = string.Join(' ', new[] { title, snippet, result.Url }).ToLowerInvariant();

        foreach (var token in TokenizeQuery(query))
        {
            if (haystack.Contains(token, StringComparison.Ordinal))
            {
                score += 6;
            }
        }

        if (domain.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase)
            || domain.Contains("facebook.com", StringComparison.OrdinalIgnoreCase)
            || domain.Contains("instagram.com", StringComparison.OrdinalIgnoreCase)
            || domain.Contains("x.com", StringComparison.OrdinalIgnoreCase)
            || domain.Contains("twitter.com", StringComparison.OrdinalIgnoreCase))
        {
            score -= 8;
        }

        if (LooksLikePersonalSite(query, domain, title, snippet))
        {
            score += 14;
        }

        return score;
    }

    private static int ScoreFetchedResult(SearchResult result, FetchResponse fetch)
    {
        if (!fetch.Success)
        {
            return int.MinValue;
        }

        var score = Math.Min(fetch.WordCount, 1200);
        if (IsWeakFetch(fetch))
        {
            score -= 10_000;
        }

        var domain = fetch.Domain ?? TryGetDomain(fetch.FinalUrl) ?? string.Empty;
        if (domain.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase)
            || domain.Contains("facebook.com", StringComparison.OrdinalIgnoreCase)
            || domain.Contains("instagram.com", StringComparison.OrdinalIgnoreCase)
            || domain.Contains("x.com", StringComparison.OrdinalIgnoreCase)
            || domain.Contains("twitter.com", StringComparison.OrdinalIgnoreCase))
        {
            score -= 250;
        }

        return score;
    }

    private static bool LooksLikePersonalSite(string query, string domain, string title, string snippet)
    {
        if (domain.Length == 0)
        {
            return false;
        }

        var tokens = TokenizeQuery(query);
        var domainTokens = domain.Replace("www.", string.Empty, StringComparison.OrdinalIgnoreCase);
        return tokens.Any(token => domainTokens.Contains(token, StringComparison.OrdinalIgnoreCase))
            || title.Contains("home", StringComparison.OrdinalIgnoreCase)
            || snippet.Contains("public speaking", StringComparison.OrdinalIgnoreCase)
            || snippet.Contains("portfolio", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWeakFetch(FetchResponse fetch)
    {
        if (!fetch.Success)
        {
            return true;
        }

        var text = fetch.Text ?? string.Empty;
        if (fetch.WordCount < 15)
        {
            return true;
        }

        return text.Contains("join linkedin", StringComparison.OrdinalIgnoreCase)
            || text.Contains("log into facebook", StringComparison.OrdinalIgnoreCase)
            || text.Contains("sign up", StringComparison.OrdinalIgnoreCase)
            || text.Contains("please complete the following challenge", StringComparison.OrdinalIgnoreCase)
            || text.Contains("unfortunately, bots use duckduckgo too", StringComparison.OrdinalIgnoreCase);
    }

    private static string? BuildSummary(string query, IReadOnlyList<ResearchSource> sources)
    {
        var summaryLines = sources
            .Where(source => !IsWeakFetch(source.Fetch))
            .Take(3)
            .Select(source => BuildSourceSummaryLine(source))
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (summaryLines.Length == 0)
        {
            return null;
        }

        return string.Join(" ", summaryLines);
    }

    private static string? BuildSourceSummaryLine(ResearchSource source)
    {
        var text = source.Fetch.Text ?? source.Fetch.Excerpt ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var firstSentence = text.Split(['.', '!', '?'], 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstSentence))
        {
            return null;
        }

        return $"[{source.CitationId}] {firstSentence.Trim()}";
    }

    private static string[] TokenizeQuery(string query)
        => query.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static token => token.Length >= 3)
            .Select(static token => token.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static List<SearchResult> DedupeResults(IReadOnlyList<SearchResult> results)
    {
        var uniqueResults = new List<SearchResult>(results.Count);
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in results)
        {
            var normalizedUrl = NormalizeResultUrl(result.Url);
            if (!seenUrls.Add(normalizedUrl))
            {
                continue;
            }

            uniqueResults.Add(result);
        }

        return uniqueResults;
    }

    private static string NormalizeResultUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url.Trim();
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

        var normalized = builder.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return string.IsNullOrWhiteSpace(normalized) ? builder.Uri.GetLeftPart(UriPartial.Path) : normalized;
    }

    private static string? TryGetDomain(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;
}
