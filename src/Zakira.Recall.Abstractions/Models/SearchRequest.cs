namespace Zakira.Recall.Abstractions.Models;

public sealed class SearchRequest
{
    public required string Query { get; init; }

    public string? Provider { get; init; }

    public string? Profile { get; init; }

    public int MaxResults { get; init; } = 10;

    public int Page { get; init; } = 1;

    public string? TimeRange { get; init; }

    public bool? SafeSearch { get; init; }
}
