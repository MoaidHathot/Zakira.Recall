using Zakira.Recall.Abstractions.Config;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Core.Profiles;

public sealed class ProfileBootstrapper(
    IRecallConfigLoader configLoader,
    IRecallConfigWriter configWriter,
    IProfileResolver profileResolver) : IProfileBootstrapper
{
    public async ValueTask<ProfileDescriptor> EnsureProfileAsync(
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
        CancellationToken cancellationToken = default)
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
            FallbackProviders = fallbackProviders ?? existing?.FallbackProviders ?? config.FallbackProviders,
            EnableProviderFallback = enableFallback ?? existing?.EnableProviderFallback ?? config.EnableProviderFallback,
            ProviderHealthCooldownSeconds = providerHealthCooldownSeconds ?? existing?.ProviderHealthCooldownSeconds ?? config.ProviderHealthCooldownSeconds,
            MaxConcurrentFetches = maxConcurrentFetches ?? existing?.MaxConcurrentFetches ?? config.MaxConcurrentFetches,
            LogLevel = logLevel ?? existing?.LogLevel ?? config.LogLevel,
            Metadata = existing?.Metadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

        var nextConfig = new RecallConfig
        {
            DefaultProvider = config.DefaultProvider,
            DefaultProfile = config.DefaultProfile ?? profileName,
            ProfilesRoot = config.ProfilesRoot,
            FallbackProviders = config.FallbackProviders,
            EnableProviderFallback = config.EnableProviderFallback,
            ProviderHealthCooldownSeconds = config.ProviderHealthCooldownSeconds,
            MaxConcurrentFetches = config.MaxConcurrentFetches,
            LogLevel = config.LogLevel,
            Profiles = profiles
        };

        await configWriter.SaveAsync(nextConfig, cancellationToken: cancellationToken);
        return await profileResolver.ResolveAsync(profileName, providerOverride: null, cancellationToken);
    }

    public async ValueTask<ProfileDescriptor> PrepareInteractiveAsync(string? profileName, string? providerOverride, CancellationToken cancellationToken = default)
    {
        var profile = await profileResolver.ResolveAsync(profileName, providerOverride, cancellationToken);
        if (!profile.Headless)
        {
            Directory.CreateDirectory(profile.UserDataDir);
            return profile;
        }

        return await EnsureProfileAsync(
            profile.Name,
            profile.Channel,
            providerOverride ?? profile.DefaultProvider,
            headless: false,
            userDataDir: profile.UserDataDir,
            fallbackProviders: profile.FallbackProviders,
            enableFallback: profile.EnableProviderFallback,
            providerHealthCooldownSeconds: profile.ProviderHealthCooldownSeconds,
            maxConcurrentFetches: profile.MaxConcurrentFetches,
            logLevel: profile.LogLevel,
            cancellationToken: cancellationToken);
    }
}
