namespace Zakira.Recall.Abstractions.Models;

public sealed class ResearchRequest
{
    public required string Query { get; init; }

    public string? Provider { get; init; }

    public string? Profile { get; init; }

    public int MaxResults { get; init; } = 8;

    public int TopPagesToRead { get; init; } = 3;

    public int Page { get; init; } = 1;

    public string? TimeRange { get; init; }

    public bool? SafeSearch { get; init; }

    public bool? EnableFallback { get; init; }

    public IReadOnlyList<string> FallbackProviders { get; init; } = [];

    public int? MaxConcurrentFetches { get; init; }
}
