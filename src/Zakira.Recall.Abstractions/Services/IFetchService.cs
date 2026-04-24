using Zakira.Recall.Abstractions.Models;

namespace Zakira.Recall.Abstractions.Services;

public interface IFetchService
{
    ValueTask<FetchResponse> FetchAsync(FetchRequest request, CancellationToken cancellationToken = default);
}
