using Microsoft.Extensions.Logging;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Core.Services;

public sealed class FetchService(IProfileResolver profileResolver, IPageFetcher pageFetcher, ILogger<FetchService> logger) : IFetchService
{
    internal const int MinConcurrentFetches = 1;
    internal const int MaxConcurrentFetchesLimit = 16;

    public async ValueTask<FetchResponse[]> FetchBatchAsync(IReadOnlyList<FetchRequest> requests, int? maxConcurrentFetches = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0)
        {
            return [];
        }

        // Every browser launch is a full Edge/Chromium process; an unbounded Task.WhenAll over N urls starts N of them at once.
        var profile = await profileResolver.ResolveAsync(requests[0].Profile, providerOverride: null, cancellationToken);
        var concurrency = Math.Clamp(maxConcurrentFetches ?? profile.MaxConcurrentFetches, MinConcurrentFetches, MaxConcurrentFetchesLimit);
        using var gate = new SemaphoreSlim(concurrency, concurrency);

        var tasks = requests.Select(async request =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await FetchAsync(request, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }

    public async ValueTask<FetchResponse> FetchAsync(FetchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Url);

        var normalizedUrl = NormalizeUrl(request.Url);
        var normalizedRequest = new FetchRequest
        {
            Url = normalizedUrl,
            Profile = request.Profile,
            TimeoutSeconds = request.TimeoutSeconds
        };

        var profile = await profileResolver.ResolveAsync(request.Profile, providerOverride: null, cancellationToken);
        try
        {
            FetchServiceLogging.FetchStarting(logger, normalizedUrl, profile.Name);
            var response = await pageFetcher.FetchAsync(normalizedRequest, profile, cancellationToken);
            if (response.Success)
            {
                FetchServiceLogging.FetchSucceeded(logger, normalizedUrl);
            }
            else if (response.Error is not null)
            {
                FetchServiceLogging.FetchFailed(logger, new InvalidOperationException(response.Error.Message), normalizedUrl);
            }

            return response;
        }
        catch (Exception ex)
        {
            FetchServiceLogging.FetchFailed(logger, ex, normalizedUrl);
            return new FetchResponse
            {
                Url = normalizedUrl,
                FinalUrl = normalizedUrl,
                Success = false,
                Error = ServiceErrors.FromException("fetch_failed", ex.Message, ex, target: normalizedUrl)
            };
        }
    }

    internal static string NormalizeUrl(string url)
    {
        var trimmed = url.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return $"https:{trimmed}";
        }

        if (Uri.TryCreate($"https://{trimmed}", UriKind.Absolute, out var httpsUri))
        {
            return httpsUri.ToString();
        }

        return trimmed;
    }
}
