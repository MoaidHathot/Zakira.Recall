using Zakira.Recall.Abstractions.Config;

namespace Zakira.Recall.Abstractions.Services;

public interface IRecallConfigLoader
{
    ValueTask<RecallConfig> LoadAsync(string? explicitPath = null, CancellationToken cancellationToken = default);
}
