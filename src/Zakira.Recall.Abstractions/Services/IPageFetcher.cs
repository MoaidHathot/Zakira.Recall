using Zakira.Recall.Abstractions.Models;

namespace Zakira.Recall.Abstractions.Services;

public interface IPageFetcher
{
    ValueTask<FetchResponse> FetchAsync(FetchRequest request, ProfileDescriptor profile, CancellationToken cancellationToken = default);
}
