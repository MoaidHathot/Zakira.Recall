using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Core.Services;

public sealed class ResearchService(ISearchService searchService, IFetchService fetchService) : IResearchService
{
    public async ValueTask<ResearchResponse> ResearchAsync(ResearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Query);

        var searchResponse = await searchService.SearchAsync(new SearchRequest
        {
            Query = request.Query,
            Profile = request.Profile,
            Provider = request.Provider,
            MaxResults = request.MaxResults
        }, cancellationToken);

        var topResults = searchResponse.Results.Take(Math.Clamp(request.TopPagesToRead, 1, Math.Max(1, searchResponse.Results.Count))).ToArray();
        var sources = new List<ResearchSource>(topResults.Length);

        foreach (var result in topResults)
        {
            var fetch = await fetchService.FetchAsync(new FetchRequest
            {
                Url = result.Url,
                Profile = request.Profile
            }, cancellationToken);

            sources.Add(new ResearchSource
            {
                SearchResult = result,
                Fetch = fetch
            });
        }

        return new ResearchResponse
        {
            Query = request.Query,
            Provider = searchResponse.Provider,
            Profile = searchResponse.Profile,
            SearchResults = searchResponse.Results,
            Sources = sources
        };
    }
}
