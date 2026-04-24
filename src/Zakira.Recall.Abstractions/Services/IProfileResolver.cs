using Zakira.Recall.Abstractions.Models;

namespace Zakira.Recall.Abstractions.Services;

public interface IProfileResolver
{
    ValueTask<ProfileDescriptor> ResolveAsync(string? profileName, string? providerOverride, CancellationToken cancellationToken = default);
}
