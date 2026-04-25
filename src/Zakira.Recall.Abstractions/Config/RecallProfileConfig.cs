namespace Zakira.Recall.Abstractions.Config;

public sealed class RecallProfileConfig
{
    public string? Name { get; init; }

    public string? Channel { get; init; }

    public string? DefaultProvider { get; init; }

    public bool? Headless { get; init; }

    public string? UserDataDir { get; init; }

    public string? Locale { get; init; }

    public int? TimeoutSeconds { get; init; }

    public IReadOnlyList<string> FallbackProviders { get; init; } = [];

    public bool? EnableProviderFallback { get; init; }

    public int? ProviderHealthCooldownSeconds { get; init; }

    public int? MaxConcurrentFetches { get; init; }

    public string? LogLevel { get; init; }

    public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
