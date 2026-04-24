using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Core.Services;

public sealed class SearchService(IProfileResolver profileResolver, ISearchProviderRegistry providerRegistry) : ISearchService
{
    public async ValueTask<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Query);

        var profile = await profileResolver.ResolveAsync(request.Profile, request.Provider, cancellationToken);
        var provider = providerRegistry.GetRequiredProvider(profile.DefaultProvider);
        var providerRequest = new SearchRequest
        {
            Query = request.Query,
            Provider = provider.Name,
            Profile = request.Profile,
            MaxResults = request.MaxResults,
            Page = request.Page,
            TimeRange = request.TimeRange,
            SafeSearch = request.SafeSearch
        };
        var results = await provider.SearchAsync(providerRequest, profile, cancellationToken);

        return new SearchResponse
        {
            Query = request.Query,
            Provider = provider.Name,
            Profile = profile.Name,
            Results = results
        };
    }
}
