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

    [Fact]
    public async Task Batch_Fetch_Is_Bounded_By_The_Profile_Concurrency_And_Preserves_Order()
    {
        var pageFetcher = new ConcurrencyTrackingPageFetcher();
        var service = new FetchService(new FakeProfileResolver(), pageFetcher, NullLogger<FetchService>.Instance);
        var requests = Enumerable.Range(1, 10).Select(index => new FetchRequest { Url = $"https://example.com/{index}" }).ToArray();

        var responses = await service.FetchBatchAsync(requests);

        Assert.Equal(requests.Select(request => request.Url), responses.Select(response => response.Url));
        Assert.Equal(10, pageFetcher.TotalStarted);
        Assert.Equal(2, pageFetcher.MaxObservedConcurrency); // FakeProfileResolver: MaxConcurrentFetches = 2
    }

    [Fact]
    public async Task Batch_Fetch_Honours_An_Explicit_Concurrency_Override_Within_Limits()
    {
        var pageFetcher = new ConcurrencyTrackingPageFetcher();
        var service = new FetchService(new FakeProfileResolver(), pageFetcher, NullLogger<FetchService>.Instance);
        var requests = Enumerable.Range(1, 6).Select(index => new FetchRequest { Url = $"https://example.com/{index}" }).ToArray();

        await service.FetchBatchAsync(requests, maxConcurrentFetches: 4);
        Assert.Equal(4, pageFetcher.MaxObservedConcurrency);

        pageFetcher.Reset();
        await service.FetchBatchAsync(requests, maxConcurrentFetches: 0);
        Assert.Equal(1, pageFetcher.MaxObservedConcurrency);
    }

    [Fact]
    public async Task Batch_Fetch_Reports_Individual_Failures_Instead_Of_Throwing()
    {
        var pageFetcher = new ConcurrencyTrackingPageFetcher(failUrl: "https://example.com/2");
        var service = new FetchService(new FakeProfileResolver(), pageFetcher, NullLogger<FetchService>.Instance);

        var responses = await service.FetchBatchAsync(
        [
            new FetchRequest { Url = "https://example.com/1" },
            new FetchRequest { Url = "https://example.com/2" },
            new FetchRequest { Url = "https://example.com/3" }
        ]);

        Assert.True(responses[0].Success);
        Assert.False(responses[1].Success);
        Assert.Equal("fetch_failed", responses[1].Error!.Code);
        Assert.True(responses[2].Success);
        Assert.Empty(await service.FetchBatchAsync([]));
    }

    private sealed class ConcurrencyTrackingPageFetcher(string? failUrl = null) : IPageFetcher
    {
        private int _inFlight;

        public int MaxObservedConcurrency { get; private set; }

        public int TotalStarted { get; private set; }

        public void Reset()
        {
            MaxObservedConcurrency = 0;
            TotalStarted = 0;
        }

        public async ValueTask<FetchResponse> FetchAsync(FetchRequest request, ProfileDescriptor profile, CancellationToken cancellationToken = default)
        {
            var inFlight = Interlocked.Increment(ref _inFlight);
            lock (this)
            {
                TotalStarted++;
                MaxObservedConcurrency = Math.Max(MaxObservedConcurrency, inFlight);
            }

            try
            {
                await Task.Delay(20, cancellationToken);
                if (string.Equals(request.Url, failUrl, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("boom");
                }

                return new FetchResponse { Url = request.Url, FinalUrl = request.Url, Success = true, Text = "content", WordCount = 1 };
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }
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
