namespace Zakira.Recall.Abstractions.Models;

public sealed class ResearchCitation
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Url { get; init; }

    public string? Domain { get; init; }

    public string? Provider { get; init; }

    public int Rank { get; init; }

    public string? Quote { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }
}
