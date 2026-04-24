using Zakira.Recall.Abstractions.Config;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Core.Profiles;

public sealed class ProfileBootstrapper(
    IRecallConfigLoader configLoader,
    IRecallConfigWriter configWriter,
    IProfileResolver profileResolver) : IProfileBootstrapper
{
    public async ValueTask<ProfileDescriptor> EnsureProfileAsync(string profileName, string? channel, string? provider, bool? headless, string? userDataDir, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        var config = await configLoader.LoadAsync(cancellationToken: cancellationToken);
        var profiles = new Dictionary<string, RecallProfileConfig>(config.Profiles, StringComparer.OrdinalIgnoreCase);
        profiles.TryGetValue(profileName, out var existing);

        profiles[profileName] = new RecallProfileConfig
        {
            Name = profileName,
            Channel = channel ?? existing?.Channel ?? "msedge",
            DefaultProvider = provider ?? existing?.DefaultProvider ?? config.DefaultProvider ?? "duckduckgo",
            Headless = headless ?? existing?.Headless ?? false,
            UserDataDir = userDataDir ?? existing?.UserDataDir,
            Locale = existing?.Locale,
            TimeoutSeconds = existing?.TimeoutSeconds,
            Metadata = existing?.Metadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

        var nextConfig = new RecallConfig
        {
            DefaultProvider = config.DefaultProvider,
            DefaultProfile = config.DefaultProfile ?? profileName,
            ProfilesRoot = config.ProfilesRoot,
            Profiles = profiles
        };

        await configWriter.SaveAsync(nextConfig, cancellationToken: cancellationToken);
        return await profileResolver.ResolveAsync(profileName, providerOverride: null, cancellationToken);
    }
}
