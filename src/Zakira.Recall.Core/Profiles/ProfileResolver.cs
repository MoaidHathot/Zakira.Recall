using Zakira.Recall.Abstractions.Config;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Core.Infrastructure;

namespace Zakira.Recall.Core.Profiles;

public sealed class ProfileResolver(
    IRecallConfigLoader configLoader,
    ISystemEnvironment environment) : IProfileResolver
{
    public async ValueTask<ProfileDescriptor> ResolveAsync(string? profileName, string? providerOverride, CancellationToken cancellationToken = default)
    {
        var config = await configLoader.LoadAsync(cancellationToken: cancellationToken);
        var selectedProfileName = SelectProfileName(config, profileName);
        var profile = ResolveProfileConfig(config, selectedProfileName);
        var defaultProvider = NormalizeProvider(providerOverride)
            ?? NormalizeProvider(profile?.DefaultProvider)
            ?? NormalizeProvider(config.DefaultProvider)
            ?? "duckduckgo";

        var profilesRoot = ResolveProfilesRoot(config);
        var userDataDir = ResolveUserDataDir(selectedProfileName, profile, profilesRoot);

        return new ProfileDescriptor
        {
            Name = selectedProfileName,
            DefaultProvider = defaultProvider,
            Channel = NormalizeChannel(profile?.Channel),
            Headless = profile?.Headless ?? true,
            Locale = profile?.Locale,
            TimeoutSeconds = Math.Clamp(profile?.TimeoutSeconds ?? 30, 5, 300),
            UserDataDir = userDataDir
        };
    }

    private static string SelectProfileName(RecallConfig config, string? profileName)
        => string.IsNullOrWhiteSpace(profileName)
            ? config.DefaultProfile?.Trim() ?? "default"
            : profileName.Trim();

    private static RecallProfileConfig? ResolveProfileConfig(RecallConfig config, string profileName)
        => config.Profiles.TryGetValue(profileName, out var profile) ? profile : null;

    private string ResolveProfilesRoot(RecallConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.ProfilesRoot))
        {
            return Path.GetFullPath(config.ProfilesRoot);
        }

        var xdgDataHome = environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdgDataHome))
        {
            return Path.Combine(xdgDataHome, "Zakira.Recall", "profiles");
        }

        var localAppData = environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Zakira.Recall", "profiles");
    }

    private static string ResolveUserDataDir(string profileName, RecallProfileConfig? profile, string profilesRoot)
    {
        if (!string.IsNullOrWhiteSpace(profile?.UserDataDir))
        {
            return Path.GetFullPath(profile.UserDataDir);
        }

        return Path.Combine(profilesRoot, profileName);
    }

    private static string? NormalizeProvider(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return null;
        }

        return provider.Trim().ToLowerInvariant() switch
        {
            "ddg" => "duckduckgo",
            "duckduckgo" => "duckduckgo",
            "bing" => "bing",
            _ => provider.Trim().ToLowerInvariant()
        };
    }

    private static string NormalizeChannel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return "msedge";
        }

        return channel.Trim().ToLowerInvariant() switch
        {
            "chrome" => "chrome",
            "chromium" => "chromium",
            "msedge" => "msedge",
            "edge" => "msedge",
            _ => channel.Trim().ToLowerInvariant()
        };
    }
}
