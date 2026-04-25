using Microsoft.Extensions.Logging;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Core.Services;

public sealed class FetchService(IProfileResolver profileResolver, IPageFetcher pageFetcher, ILogger<FetchService> logger) : IFetchService
{
    public async ValueTask<FetchResponse> FetchAsync(FetchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Url);

        var profile = await profileResolver.ResolveAsync(request.Profile, providerOverride: null, cancellationToken);
        try
        {
            FetchServiceLogging.FetchStarting(logger, request.Url, profile.Name);
            var response = await pageFetcher.FetchAsync(request, profile, cancellationToken);
            FetchServiceLogging.FetchSucceeded(logger, request.Url);
            return response;
        }
        catch (Exception ex)
        {
            FetchServiceLogging.FetchFailed(logger, ex, request.Url);
            return new FetchResponse
            {
                Url = request.Url,
                FinalUrl = request.Url,
                Success = false,
                Error = ServiceErrors.FromException("fetch_failed", ex.Message, ex, target: request.Url)
            };
        }
    }
}
