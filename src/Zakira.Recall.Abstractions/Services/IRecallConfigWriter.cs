using Zakira.Recall.Abstractions.Config;

namespace Zakira.Recall.Abstractions.Services;

public interface IRecallConfigWriter
{
    ValueTask<string> SaveAsync(RecallConfig config, string? explicitPath = null, CancellationToken cancellationToken = default);
}
