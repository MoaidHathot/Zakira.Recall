using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Core.Services;

namespace Zakira.Recall.Tests.Unit.Services;

public sealed class ResearchServiceTests
{
    [Fact]
    public async Task Reads_Only_Top_Pages_Requested()
    {
        var searchService = new FakeSearchService();
        var fetchService = new FakeFetchService();
        var service = new ResearchService(searchService, fetchService);

        var response = await service.ResearchAsync(new ResearchRequest
        {
            Query = "mcp web search",
            MaxResults = 5,
            TopPagesToRead = 2
        });

        Assert.Equal(5, response.SearchResults.Count);
        Assert.Equal(2, response.Sources.Count);
        Assert.Equal(2, fetchService.RequestedUrls.Count);
    }

    private sealed class FakeSearchService : ISearchService
    {
        public ValueTask<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
        {
            var results = Enumerable.Range(1, request.MaxResults)
                .Select(index => new SearchResult
                {
                    Title = $"Result {index}",
                    Url = $"https://example.com/{index}",
                    Provider = "duckduckgo",
                    Rank = index,
                    Snippet = $"Snippet {index}"
                })
                .ToArray();

            return ValueTask.FromResult(new SearchResponse
            {
                Query = request.Query,
                Provider = "duckduckgo",
                Profile = request.Profile ?? "default",
                Results = results
            });
        }
    }

    private sealed class FakeFetchService : IFetchService
    {
        public List<string> RequestedUrls { get; } = [];

        public ValueTask<FetchResponse> FetchAsync(FetchRequest request, CancellationToken cancellationToken = default)
        {
            RequestedUrls.Add(request.Url);
            return ValueTask.FromResult(new FetchResponse
            {
                Url = request.Url,
                FinalUrl = request.Url,
                Title = request.Url,
                Text = "content"
            });
        }
    }
}
