using Microsoft.Extensions.Logging.Abstractions;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Core.Services;

namespace Zakira.Recall.Tests.Unit.Services;

public sealed class FetchServiceTests
{
    [Fact]
    public async Task Normalizes_SchemeLess_Urls_To_Https()
    {
        var pageFetcher = new CapturingPageFetcher();
        var service = new FetchService(new FakeProfileResolver(), pageFetcher, NullLogger<FetchService>.Instance);

        var response = await service.FetchAsync(new FetchRequest
        {
            Url = "example.com"
        });

        Assert.Equal("https://example.com/", pageFetcher.RequestedUrl);
        Assert.Equal("https://example.com/", response.Url);
        Assert.Equal("https://example.com/", response.FinalUrl);
    }

    [Fact]
    public async Task Leaves_Absolute_Urls_Unchanged()
    {
        var pageFetcher = new CapturingPageFetcher();
        var service = new FetchService(new FakeProfileResolver(), pageFetcher, NullLogger<FetchService>.Instance);

        await service.FetchAsync(new FetchRequest
        {
            Url = "https://example.com/path"
        });

        Assert.Equal("https://example.com/path", pageFetcher.RequestedUrl);

        await service.FetchAsync(new FetchRequest
        {
            Url = "http://example.com/path"
        });

        Assert.Equal("http://example.com/path", pageFetcher.RequestedUrl);
    }

    private sealed class CapturingPageFetcher : IPageFetcher
    {
        public string? RequestedUrl { get; private set; }

        public ValueTask<FetchResponse> FetchAsync(FetchRequest request, ProfileDescriptor profile, CancellationToken cancellationToken = default)
        {
            RequestedUrl = request.Url;
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
