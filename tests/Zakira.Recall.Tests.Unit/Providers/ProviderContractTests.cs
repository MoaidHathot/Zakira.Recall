using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Core.Providers;
using Zakira.Recall.Playwright.Browser;
using Zakira.Recall.Playwright.Providers;

namespace Zakira.Recall.Tests.Unit.Providers;

public sealed class ProviderContractTests
{
    [Fact]
    public void Providers_Expose_Unique_Names_And_Capabilities()
    {
        ISearchProvider[] providers =
        [
            new DuckDuckGoSearchProvider(new HttpClient(new StubHandler())),
            new DuckDuckGoBrowserSearchProvider(new StubBrowserSessionFactory()),
            new BingSearchProvider(new StubBrowserSessionFactory())
        ];

        Assert.Equal(providers.Length, providers.Select(static provider => provider.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(providers, provider => provider.Capabilities.RequiresBrowser);
        Assert.Contains(providers, provider => !provider.Capabilities.RequiresBrowser);
        Assert.Contains(providers, provider => provider.Aliases.Count > 0 || provider.SetupUrl is not null);
    }

    [Fact]
    public void Registry_Normalizes_Aliases_And_Chooses_Non_Browser_Default()
    {
        ISearchProvider[] providers =
        [
            new DuckDuckGoBrowserSearchProvider(new StubBrowserSessionFactory()),
            new BingSearchProvider(new StubBrowserSessionFactory()),
            new DuckDuckGoSearchProvider(new HttpClient(new StubHandler()))
        ];

        var registry = new SearchProviderRegistry(providers);

        Assert.Equal("duckduckgo", registry.NormalizeProviderName("ddg"));
        Assert.Equal("duckduckgo", registry.GetDefaultProviderName());
        Assert.True(registry.TryGetProvider("ddg", out var provider));
        Assert.Equal("duckduckgo", provider!.Name);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class StubBrowserSessionFactory : IBrowserSessionFactory
    {
        public ValueTask<Microsoft.Playwright.IBrowserContext> CreateContextAsync(Zakira.Recall.Abstractions.Models.ProfileDescriptor profile, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
