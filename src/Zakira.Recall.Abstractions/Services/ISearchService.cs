using Zakira.Recall.Abstractions.Models;

namespace Zakira.Recall.Abstractions.Services;

public interface ISearchService
{
    ValueTask<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);
}
