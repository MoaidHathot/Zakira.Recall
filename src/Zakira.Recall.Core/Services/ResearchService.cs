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

        var topResults = SelectResults(searchResponse.Results, request.TopPagesToRead, request.EnforceDomainDiversity);
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
        var sources = new List<ResearchSource>(fetches.Length);
        var citations = new List<ResearchCitation>(fetches.Length);
        for (var index = 0; index < fetches.Length; index++)
        {
            var (result, fetch) = fetches[index];
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

        return new ResearchResponse
        {
            Query = request.Query,
            Provider = searchResponse.Provider,
            Profile = searchResponse.Profile,
            Success = searchResponse.Success,
            SearchResults = searchResponse.Results,
            Sources = sources,
            Citations = citations,
            Errors = errors
        };
    }

    private static SearchResult[] SelectResults(IReadOnlyList<SearchResult> results, int topPagesToRead, bool enforceDomainDiversity)
    {
        var targetCount = Math.Clamp(topPagesToRead, 1, Math.Max(1, results.Count));
        var uniqueResults = DedupeResults(results);
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
