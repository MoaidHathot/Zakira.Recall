using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Zakira.Recall.Core.Services;

namespace Zakira.Recall.Tests.Unit.Services;

public sealed class ResearchServiceTests
{
    [Fact]
    public async Task Reads_Only_Top_Pages_Requested()
    {
        var searchService = new FakeSearchService();
        var fetchService = new FakeFetchService();
        var service = new ResearchService(searchService, fetchService, new FakeProfileResolver(), NullLogger<ResearchService>.Instance);

        var response = await service.ResearchAsync(new ResearchRequest
        {
            Query = "mcp web search",
            MaxResults = 5,
            TopPagesToRead = 2
        });

        Assert.Equal(5, response.SearchResults.Count);
        Assert.Equal(2, response.Sources.Count);
        Assert.Equal(2, fetchService.RequestedUrls.Count);
        Assert.Equal(2, response.Citations.Count);
    }

    [Fact]
    public async Task Preserves_Partial_Fetch_Failures_As_Errors()
    {
        var searchService = new FakeSearchService();
        var fetchService = new FakeFetchService(failUrl: "https://example.com/2");
        var service = new ResearchService(searchService, fetchService, new FakeProfileResolver(), NullLogger<ResearchService>.Instance);

        var response = await service.ResearchAsync(new ResearchRequest
        {
            Query = "mcp web search",
            MaxResults = 3,
            TopPagesToRead = 3
        });

        Assert.Equal(3, response.Sources.Count);
        Assert.Single(response.Errors);
        Assert.Contains(response.Sources, source => !source.Fetch.Success && source.Fetch.Error is not null);
    }

    [Fact]
    public async Task Dedupes_Equivalent_Urls_Before_Fetching()
    {
        var searchService = new FakeSearchService(results:
        [
            CreateResult(1, "https://example.com/post"),
            CreateResult(2, "https://example.com/post/"),
            CreateResult(3, "https://contoso.com/post")
        ]);
        var fetchService = new FakeFetchService();
        var service = new ResearchService(searchService, fetchService, new FakeProfileResolver(), NullLogger<ResearchService>.Instance);

        var response = await service.ResearchAsync(new ResearchRequest
        {
            Query = "mcp web search",
            TopPagesToRead = 3
        });

        Assert.Equal(2, response.Sources.Count);
        Assert.Equal(2, fetchService.RequestedUrls.Count);
        Assert.Contains("https://example.com/post", fetchService.RequestedUrls);
        Assert.DoesNotContain("https://example.com/post/", fetchService.RequestedUrls);
    }

    [Fact]
    public async Task Prefers_Unique_Domains_When_Selecting_Top_Pages()
    {
        var searchService = new FakeSearchService(results:
        [
            CreateResult(1, "https://example.com/1"),
            CreateResult(2, "https://example.com/2"),
            CreateResult(3, "https://contoso.com/1")
        ]);
        var fetchService = new FakeFetchService();
        var service = new ResearchService(searchService, fetchService, new FakeProfileResolver(), NullLogger<ResearchService>.Instance);

        await service.ResearchAsync(new ResearchRequest
        {
            Query = "mcp web search",
            TopPagesToRead = 2,
            EnforceDomainDiversity = true
        });

        Assert.Equal([
            "https://example.com/1",
            "https://contoso.com/1"
        ], fetchService.RequestedUrls);
    }

    [Fact]
    public async Task Prefers_Stronger_Non_Social_Sources_And_Builds_A_Summary()
    {
        var searchService = new FakeSearchService(results:
        [
            CreateResult(1, "https://www.linkedin.com/in/example"),
            CreateResult(2, "https://example.dev"),
            CreateResult(3, "https://www.facebook.com/example")
        ]);
        var fetchService = new FakeFetchService(customResponses: new Dictionary<string, FetchResponse>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://www.linkedin.com/in/example"] = CreateFetch("https://www.linkedin.com/in/example", "Join LinkedIn to view the full profile.", 8, "www.linkedin.com"),
            ["https://example.dev"] = CreateFetch("https://example.dev", "Example Person is a principal engineer working on .NET and cloud systems. They speak at conferences and publish technical articles.", 19, "example.dev"),
            ["https://www.facebook.com/example"] = CreateFetch("https://www.facebook.com/example", "Log into Facebook to start sharing and connecting.", 8, "www.facebook.com")
        });
        var service = new ResearchService(searchService, fetchService, new FakeProfileResolver(), NullLogger<ResearchService>.Instance);

        var response = await service.ResearchAsync(new ResearchRequest
        {
            Query = "Example Person",
            TopPagesToRead = 3
        });

        Assert.True(response.Success);
        Assert.NotNull(response.Summary);
        Assert.Contains("principal engineer", response.Summary!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("https://example.dev", response.Citations[0].Url);
    }

    [Fact]
    public async Task Filters_Weak_Fetches_From_Successful_Research()
    {
        var searchService = new FakeSearchService(results:
        [
            CreateResult(1, "https://www.linkedin.com/in/example"),
            CreateResult(2, "https://www.facebook.com/example")
        ]);
        var fetchService = new FakeFetchService(customResponses: new Dictionary<string, FetchResponse>(StringComparer.OrdinalIgnoreCase)
        {
            ["https://www.linkedin.com/in/example"] = CreateFetch("https://www.linkedin.com/in/example", "Join LinkedIn to view the full profile.", 8, "www.linkedin.com"),
            ["https://www.facebook.com/example"] = CreateFetch("https://www.facebook.com/example", "Log into Facebook to start sharing and connecting.", 8, "www.facebook.com")
        });
        var service = new ResearchService(searchService, fetchService, new FakeProfileResolver(), NullLogger<ResearchService>.Instance);

        var response = await service.ResearchAsync(new ResearchRequest
        {
            Query = "Example Person",
            TopPagesToRead = 2
        });

        Assert.False(response.Success);
        Assert.Null(response.Summary);
    }

    private static SearchResult CreateResult(int rank, string url)
        => new()
        {
            Title = $"Result {rank}",
            Url = url,
            Provider = "duckduckgo",
            Rank = rank,
            Snippet = $"Snippet {rank}"
        };

    private static FetchResponse CreateFetch(string url, string text, int wordCount, string domain)
        => new()
        {
            Url = url,
            FinalUrl = url,
            Success = true,
            Title = url,
            Text = text,
            Excerpt = text,
            Domain = domain,
            WordCount = wordCount
        };

    private sealed class FakeSearchService(IReadOnlyList<SearchResult>? results = null) : ISearchService
    {
        public ValueTask<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
        {
            var responseResults = results ?? Enumerable.Range(1, request.MaxResults)
                .Select(index => CreateResult(index, $"https://example.com/{index}"))
                .ToArray();

            return ValueTask.FromResult(new SearchResponse
            {
                Query = request.Query,
                Provider = "duckduckgo",
                Profile = request.Profile ?? "default",
                Success = true,
                Results = responseResults
            });
        }
    }

    private sealed class FakeFetchService(string? failUrl = null, IReadOnlyDictionary<string, FetchResponse>? customResponses = null) : IFetchService
    {
        public List<string> RequestedUrls { get; } = [];

        public ValueTask<FetchResponse> FetchAsync(FetchRequest request, CancellationToken cancellationToken = default)
        {
            RequestedUrls.Add(request.Url);
            if (customResponses is not null && customResponses.TryGetValue(request.Url, out var customResponse))
            {
                return ValueTask.FromResult(customResponse);
            }

            if (string.Equals(request.Url, failUrl, StringComparison.OrdinalIgnoreCase))
            {
                return ValueTask.FromResult(new FetchResponse
                {
                    Url = request.Url,
                    FinalUrl = request.Url,
                    Success = false,
                    Error = new OperationError
                    {
                        Code = "fetch_failed",
                        Message = "boom",
                        Target = request.Url,
                        Transient = true
                    }
                });
            }

            return ValueTask.FromResult(new FetchResponse
            {
                Url = request.Url,
                FinalUrl = request.Url,
                Success = true,
                Title = request.Url,
                Text = "content",
                Excerpt = "content",
                Domain = "example.com",
                WordCount = 1
            });
        }
    }

    private sealed class FakeProfileResolver : IProfileResolver
    {
        public ValueTask<ProfileDescriptor> ResolveAsync(string? profileName, string? providerOverride, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new ProfileDescriptor
            {
                Name = profileName ?? "default",
                UserDataDir = "ignored",
                Channel = "msedge",
                Headless = true,
                DefaultProvider = providerOverride ?? "duckduckgo",
                TimeoutSeconds = 30,
                MaxConcurrentFetches = 2,
                EnableProviderFallback = true,
                ProviderHealthCooldownSeconds = 300
            });
    }
}
