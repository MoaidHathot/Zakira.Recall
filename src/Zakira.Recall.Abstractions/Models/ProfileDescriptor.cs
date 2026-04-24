namespace Zakira.Recall.Abstractions.Models;

public sealed class ProfileDescriptor
{
    public required string Name { get; init; }

    public required string UserDataDir { get; init; }

    public required string Channel { get; init; }

    public required bool Headless { get; init; }

    public required string DefaultProvider { get; init; }

    public string? Locale { get; init; }

    public int TimeoutSeconds { get; init; } = 30;
}
