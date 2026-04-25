using Microsoft.Extensions.Logging;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Core.Services;

public sealed class FetchService(IProfileResolver profileResolver, IPageFetcher pageFetcher, ILogger<FetchService> logger) : IFetchService
{
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
            FetchServiceLogging.FetchSucceeded(logger, normalizedUrl);
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
