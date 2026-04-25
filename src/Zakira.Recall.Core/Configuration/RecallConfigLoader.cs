using System.Text.Json;
using Zakira.Recall.Abstractions.Config;
using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Core.Configuration;

public sealed class RecallConfigLoader(IRecallConfigLocator locator, RuntimeDefaults runtimeDefaults, IRecallConfigValidator validator) : IRecallConfigLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public async ValueTask<RecallConfig> LoadAsync(string? explicitPath = null, CancellationToken cancellationToken = default)
    {
        var path = explicitPath ?? runtimeDefaults.ConfigPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = locator.GetCandidateConfigPaths().FirstOrDefault(File.Exists);
        }

        RecallConfig config;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            config = new RecallConfig();
        }
        else
        {
            await using var stream = File.OpenRead(path);
            config = await JsonSerializer.DeserializeAsync<RecallConfig>(stream, SerializerOptions, cancellationToken) ?? new RecallConfig();
        }

        var merged = new RecallConfig
        {
            DefaultProvider = runtimeDefaults.DefaultProvider ?? config.DefaultProvider,
            DefaultProfile = runtimeDefaults.DefaultProfile ?? config.DefaultProfile,
            ProfilesRoot = runtimeDefaults.ProfilesRoot ?? config.ProfilesRoot,
            FallbackProviders = config.FallbackProviders,
            EnableProviderFallback = config.EnableProviderFallback,
            ProviderHealthCooldownSeconds = config.ProviderHealthCooldownSeconds,
            MaxConcurrentFetches = config.MaxConcurrentFetches,
            LogLevel = runtimeDefaults.LogLevel ?? config.LogLevel,
            Profiles = config.Profiles
        };

        validator.Validate(merged);
        return merged;
    }
}
