using Zakira.Recall.Abstractions.Models;

namespace Zakira.Recall.Abstractions.Services;

public interface IResearchService
{
    ValueTask<ResearchResponse> ResearchAsync(ResearchRequest request, CancellationToken cancellationToken = default);
}
