using Zakira.Recall.Abstractions.Models;

namespace Zakira.Recall.Abstractions.Services;

public interface IProfileBootstrapper
{
    ValueTask<ProfileDescriptor> EnsureProfileAsync(
        string profileName,
        string? channel,
        string? provider,
        bool? headless,
        string? userDataDir,
        IReadOnlyList<string>? fallbackProviders = null,
        bool? enableFallback = null,
        int? providerHealthCooldownSeconds = null,
        int? maxConcurrentFetches = null,
        string? logLevel = null,
        CancellationToken cancellationToken = default);

    ValueTask<ProfileDescriptor> PrepareInteractiveAsync(string? profileName, string? providerOverride, CancellationToken cancellationToken = default);
}
