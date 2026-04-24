namespace Zakira.Recall.Abstractions.Models;

public sealed class SearchResponse
{
    public required string Query { get; init; }

    public required string Provider { get; init; }

    public required string Profile { get; init; }

    public IReadOnlyList<SearchResult> Results { get; init; } = [];
}
