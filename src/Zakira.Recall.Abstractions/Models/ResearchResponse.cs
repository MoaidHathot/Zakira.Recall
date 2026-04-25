namespace Zakira.Recall.Abstractions.Models;

public sealed class ResearchResponse
{
    public required string Query { get; init; }

    public string? Provider { get; init; }

    public required string Profile { get; init; }

    public bool Success { get; init; }

    public IReadOnlyList<SearchResult> SearchResults { get; init; } = [];

    public IReadOnlyList<ResearchSource> Sources { get; init; } = [];

    public IReadOnlyList<ResearchCitation> Citations { get; init; } = [];

    public IReadOnlyList<OperationError> Errors { get; init; } = [];
}
