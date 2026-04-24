namespace Zakira.Recall.Abstractions.Models;

public sealed class ResearchResponse
{
    public required string Query { get; init; }

    public required string Provider { get; init; }

    public required string Profile { get; init; }

    public IReadOnlyList<SearchResult> SearchResults { get; init; } = [];

    public IReadOnlyList<ResearchSource> Sources { get; init; } = [];
}
