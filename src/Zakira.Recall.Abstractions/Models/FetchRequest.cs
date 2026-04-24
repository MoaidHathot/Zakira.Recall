namespace Zakira.Recall.Abstractions.Models;

public sealed class FetchRequest
{
    public required string Url { get; init; }

    public string? Profile { get; init; }

    public int TimeoutSeconds { get; init; } = 30;
}
