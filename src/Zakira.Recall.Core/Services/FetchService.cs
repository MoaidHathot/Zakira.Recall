using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Core.Services;

public sealed class FetchService(IProfileResolver profileResolver, IPageFetcher pageFetcher) : IFetchService
{
    public async ValueTask<FetchResponse> FetchAsync(FetchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Url);

        var profile = await profileResolver.ResolveAsync(request.Profile, providerOverride: null, cancellationToken);
        return await pageFetcher.FetchAsync(request, profile, cancellationToken);
    }
}
