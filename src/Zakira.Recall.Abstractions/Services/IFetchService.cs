using Zakira.Recall.Abstractions.Models;

namespace Zakira.Recall.Abstractions.Services;

public interface IFetchService
{
    ValueTask<FetchResponse> FetchAsync(FetchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches several pages with bounded concurrency (the profile's <c>MaxConcurrentFetches</c> unless overridden).
    /// Results are returned in request order; individual failures are reported per response, never thrown.
    /// </summary>
    ValueTask<FetchResponse[]> FetchBatchAsync(IReadOnlyList<FetchRequest> requests, int? maxConcurrentFetches = null, CancellationToken cancellationToken = default);
}
