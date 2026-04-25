using Microsoft.Extensions.Logging;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Core.Services;

public sealed class SearchService(
    IProfileResolver profileResolver,
    ISearchProviderRegistry providerRegistry,
    IProviderHealthTracker healthTracker,
    ILogger<SearchService> logger) : ISearchService
{
    public async ValueTask<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Query);

        var profile = await profileResolver.ResolveAsync(request.Profile, request.Provider, cancellationToken);
        var providerRequest = new SearchRequest
        {
            Query = request.Query,
            Provider = profile.DefaultProvider,
            Profile = request.Profile,
            MaxResults = request.MaxResults,
            Page = request.Page,
            TimeRange = request.TimeRange,
            SafeSearch = request.SafeSearch,
            EnableFallback = request.EnableFallback,
            FallbackProviders = request.FallbackProviders
        };
        var attempts = new List<ProviderAttempt>();
        var candidates = BuildCandidateProviders(request, profile);
        var fallbackEnabled = request.EnableFallback ?? profile.EnableProviderFallback;

        foreach (var providerName in candidates)
        {
            var provider = providerRegistry.GetRequiredProvider(providerName);
            if (providerName != profile.DefaultProvider
                && (!fallbackEnabled || !healthTracker.IsHealthy(providerName, profile.ProviderHealthCooldownSeconds)))
            {
                attempts.Add(new ProviderAttempt
                {
                    Provider = providerName,
                    Skipped = true,
                    Success = false
                });
                SearchServiceLogging.SearchProviderSkipped(logger, providerName);
                continue;
            }

            try
            {
                SearchServiceLogging.SearchStarting(logger, request.Query, provider.Name, profile.Name);
                var providerSpecificRequest = new SearchRequest
                {
                    Query = providerRequest.Query,
                    Provider = provider.Name,
                    Profile = providerRequest.Profile,
                    MaxResults = providerRequest.MaxResults,
                    Page = providerRequest.Page,
                    TimeRange = providerRequest.TimeRange,
                    SafeSearch = providerRequest.SafeSearch,
                    EnableFallback = providerRequest.EnableFallback,
                    FallbackProviders = providerRequest.FallbackProviders
                };
                var results = await provider.SearchAsync(providerSpecificRequest, profile, cancellationToken);
                healthTracker.RecordSuccess(provider.Name);
                attempts.Add(new ProviderAttempt
                {
                    Provider = provider.Name,
                    Success = true,
                    ResultCount = results.Count
                });
                SearchServiceLogging.SearchSucceeded(logger, provider.Name, results.Count, request.Query);

                if (results.Count > 0 || providerName == candidates[^1])
                {
                    return new SearchResponse
                    {
                        Query = request.Query,
                        Provider = provider.Name,
                        Profile = profile.Name,
                        Success = results.Count > 0,
                        Results = results,
                        Attempts = attempts
                    };
                }

                SearchServiceLogging.SearchProviderReturnedNoResults(logger, provider.Name, request.Query);
            }
            catch (Exception ex)
            {
                healthTracker.RecordFailure(provider.Name);
                SearchServiceLogging.SearchFailed(logger, ex, provider.Name, request.Query);
                attempts.Add(new ProviderAttempt
                {
                    Provider = provider.Name,
                    Success = false,
                    Error = ServiceErrors.FromException("search_failed", ex.Message, ex, provider.Name, request.Query)
                });
            }

            if (!fallbackEnabled)
            {
                break;
            }
        }

        return new SearchResponse
        {
            Query = request.Query,
            Provider = null,
            Profile = profile.Name,
            Success = false,
            Results = [],
            Attempts = attempts,
            Error = attempts.Select(static attempt => attempt.Error).FirstOrDefault(static error => error is not null)
        };
    }

    private IReadOnlyList<string> BuildCandidateProviders(SearchRequest request, ProfileDescriptor profile)
    {
        var candidates = new List<string> { profile.DefaultProvider };
        var requestFallbacks = request.FallbackProviders.Count > 0 ? request.FallbackProviders : profile.FallbackProviders;
        foreach (var provider in requestFallbacks)
        {
            if (!candidates.Contains(provider, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(provider);
            }
        }

        return candidates;
    }
}
