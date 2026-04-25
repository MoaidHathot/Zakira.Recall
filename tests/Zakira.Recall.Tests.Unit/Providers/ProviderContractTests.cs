using Zakira.Recall.Abstractions.Services;
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
