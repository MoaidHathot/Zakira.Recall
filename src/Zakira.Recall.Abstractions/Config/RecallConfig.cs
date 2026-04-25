namespace Zakira.Recall.Abstractions.Config;

public sealed class RecallConfig
{
    public string? DefaultProvider { get; init; }

    public string? DefaultProfile { get; init; }

    public string? ProfilesRoot { get; init; }

    public IReadOnlyList<string> FallbackProviders { get; init; } = [];

    public bool? EnableProviderFallback { get; init; }

    public int? ProviderHealthCooldownSeconds { get; init; }

    public int? MaxConcurrentFetches { get; init; }

    public string? LogLevel { get; init; }

    public Dictionary<string, RecallProfileConfig> Profiles { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
