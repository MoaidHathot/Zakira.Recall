using Zakira.Recall.Abstractions.Config;
using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Core.Configuration;

public sealed class RecallConfigValidator : IRecallConfigValidator
{
    private readonly ISearchProviderRegistry _providerRegistry;

    public RecallConfigValidator(ISearchProviderRegistry providerRegistry)
    {
        _providerRegistry = providerRegistry;
    }

    private static readonly HashSet<string> ValidChannels = new(StringComparer.OrdinalIgnoreCase)
    {
        "chromium",
        "chrome",
        "msedge",
        "edge"
    };

    private static readonly HashSet<string> ValidLogLevels = Enum.GetNames<Microsoft.Extensions.Logging.LogLevel>()
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public void Validate(RecallConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        ValidateProvider(config.DefaultProvider, "defaultProvider");
        ValidateProviders(config.FallbackProviders, "fallbackProviders");
        ValidatePositive(config.ProviderHealthCooldownSeconds, 1, 3600, "providerHealthCooldownSeconds");
        ValidatePositive(config.MaxConcurrentFetches, 1, 16, "maxConcurrentFetches");
        ValidateLogLevel(config.LogLevel, "logLevel");

        if (!string.IsNullOrWhiteSpace(config.DefaultProfile) && !config.Profiles.ContainsKey(config.DefaultProfile))
        {
            throw new InvalidOperationException($"Configured default profile '{config.DefaultProfile}' does not exist.");
        }

        foreach (var (profileName, profile) in config.Profiles)
        {
            ValidateProfile(profileName, profile);
        }
    }

    private void ValidateProfile(string profileName, RecallProfileConfig profile)
    {
        ValidateProvider(profile.DefaultProvider, $"profiles.{profileName}.defaultProvider");
        ValidateProviders(profile.FallbackProviders, $"profiles.{profileName}.fallbackProviders");
        ValidatePositive(profile.TimeoutSeconds, 5, 300, $"profiles.{profileName}.timeoutSeconds");
        ValidatePositive(profile.ProviderHealthCooldownSeconds, 1, 3600, $"profiles.{profileName}.providerHealthCooldownSeconds");
        ValidatePositive(profile.MaxConcurrentFetches, 1, 16, $"profiles.{profileName}.maxConcurrentFetches");
        ValidateLogLevel(profile.LogLevel, $"profiles.{profileName}.logLevel");

        if (!string.IsNullOrWhiteSpace(profile.Channel) && !ValidChannels.Contains(profile.Channel))
        {
            throw new InvalidOperationException($"Unsupported browser channel '{profile.Channel}' in profiles.{profileName}.channel.");
        }
    }

    private void ValidateProvider(string? provider, string path)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return;
        }

        if (_providerRegistry.NormalizeProviderName(provider) is null)
        {
            throw new InvalidOperationException($"Unsupported provider '{provider}' in {path}.");
        }
    }

    private void ValidateProviders(IReadOnlyList<string> providers, string path)
    {
        foreach (var provider in providers)
        {
            ValidateProvider(provider, path);
        }
    }

    private static void ValidatePositive(int? value, int min, int max, string path)
    {
        if (!value.HasValue)
        {
            return;
        }

        if (value.Value < min || value.Value > max)
        {
            throw new InvalidOperationException($"Value '{value.Value}' in {path} must be between {min} and {max}.");
        }
    }

    private static void ValidateLogLevel(string? logLevel, string path)
    {
        if (string.IsNullOrWhiteSpace(logLevel))
        {
            return;
        }

        if (!ValidLogLevels.Contains(logLevel.Trim()))
        {
            throw new InvalidOperationException($"Unsupported log level '{logLevel}' in {path}.");
        }
    }
}
