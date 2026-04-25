namespace Zakira.Recall.Abstractions.Models;

public sealed class SearchResponse
{
    public required string Query { get; init; }

    public string? Provider { get; init; }

    public required string Profile { get; init; }

    public bool Success { get; init; }

    public IReadOnlyList<SearchResult> Results { get; init; } = [];

    public IReadOnlyList<ProviderAttempt> Attempts { get; init; } = [];

    public OperationError? Error { get; init; }
}
