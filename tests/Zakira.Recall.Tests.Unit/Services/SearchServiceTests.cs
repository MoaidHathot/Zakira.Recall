using Microsoft.Extensions.Logging.Abstractions;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Core.Services;

namespace Zakira.Recall.Tests.Unit.Services;

public sealed class SearchServiceTests
{
    [Fact]
    public async Task Falls_Back_To_Secondary_Provider_When_Primary_Fails()
    {
        var resolver = new FakeProfileResolver();
        var providers = new FakeSearchProviderRegistry(
            new ThrowingSearchProvider("duckduckgo"),
            new ReturningSearchProvider("bing"));
        var service = new SearchService(resolver, providers, new FakeProviderHealthTracker(), NullLogger<SearchService>.Instance);

        var response = await service.SearchAsync(new SearchRequest
        {
            Query = "test",
            FallbackProviders = ["bing"]
        });

        Assert.True(response.Success);
        Assert.Equal("bing", response.Provider);
        Assert.Equal(2, response.Attempts.Count);
        Assert.False(response.Attempts[0].Success);
        Assert.True(response.Attempts[1].Success);
    }

    [Fact]
    public async Task Returns_First_Provider_When_No_Fallback_Is_Enabled()
    {
        var resolver = new FakeProfileResolver();
        var providers = new FakeSearchProviderRegistry(new ReturningSearchProvider("duckduckgo"), new ReturningSearchProvider("bing"));
        var service = new SearchService(resolver, providers, new FakeProviderHealthTracker(), NullLogger<SearchService>.Instance);

        var response = await service.SearchAsync(new SearchRequest
        {
            Query = "test",
            EnableFallback = false,
            FallbackProviders = ["bing"]
        });

        Assert.True(response.Success);
        Assert.Single(response.Attempts);
        Assert.Equal("duckduckgo", response.Provider);
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
                FallbackProviders = ["bing"],
                EnableProviderFallback = true,
                ProviderHealthCooldownSeconds = 300,
                MaxConcurrentFetches = 3
            });
    }

    private sealed class FakeProviderHealthTracker : IProviderHealthTracker
    {
        public ProviderHealthSnapshot GetSnapshot(string providerName, int cooldownSeconds)
            => new()
            {
                Provider = providerName,
                IsHealthy = true,
                ConsecutiveFailures = 0
            };

        public bool IsHealthy(string providerName, int cooldownSeconds) => true;

        public void RecordFailure(string providerName)
        {
        }

        public void RecordSuccess(string providerName)
        {
        }
    }

    private sealed class FakeSearchProviderRegistry(params ISearchProvider[] providers) : ISearchProviderRegistry
    {
        private readonly Dictionary<string, ISearchProvider> _providers = providers.ToDictionary(static provider => provider.Name, StringComparer.OrdinalIgnoreCase);

        public ISearchProvider GetRequiredProvider(string providerName) => _providers[providerName];

        public IReadOnlyList<ISearchProvider> GetProviders() => _providers.Values.ToArray();

        public IReadOnlyList<string> GetProviderNames() => _providers.Keys.ToArray();
    }

    private sealed class ThrowingSearchProvider(string name) : ISearchProvider
    {
        public string Name => name;

        public SearchProviderCapabilities Capabilities => new();

        public ValueTask<IReadOnlyList<SearchResult>> SearchAsync(SearchRequest request, ProfileDescriptor profile, CancellationToken cancellationToken = default)
            => ValueTask.FromException<IReadOnlyList<SearchResult>>(new InvalidOperationException("boom"));
    }

    private sealed class ReturningSearchProvider(string name) : ISearchProvider
    {
        public string Name => name;

        public SearchProviderCapabilities Capabilities => new();

        public ValueTask<IReadOnlyList<SearchResult>> SearchAsync(SearchRequest request, ProfileDescriptor profile, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<SearchResult>>(
            [
                new SearchResult
                {
                    Title = "Result 1",
                    Url = "https://example.com/1",
                    Provider = name,
                    Rank = 1,
                    Snippet = "Snippet"
                }
            ]);
    }
}
