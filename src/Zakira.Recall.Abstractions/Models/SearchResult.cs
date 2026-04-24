namespace Zakira.Recall.Abstractions.Models;

public sealed class SearchResult
{
    public required string Title { get; init; }

    public required string Url { get; init; }

    public string? DisplayUrl { get; init; }

    public string? Snippet { get; init; }

    public required string Provider { get; init; }

    public int Rank { get; init; }
}
