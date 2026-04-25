using Zakira.Recall.Abstractions.Models;

namespace Zakira.Recall.Abstractions.Services;

public interface IProviderBootstrapper
{
    ValueTask<ProfileDescriptor> PrepareInteractiveAsync(string? profileName, string? providerOverride, CancellationToken cancellationToken = default);
}
