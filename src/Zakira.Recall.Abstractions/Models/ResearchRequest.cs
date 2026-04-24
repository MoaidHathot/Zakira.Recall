namespace Zakira.Recall.Abstractions.Models;

public sealed class ResearchRequest
{
    public required string Query { get; init; }

    public string? Provider { get; init; }

    public string? Profile { get; init; }

    public int MaxResults { get; init; } = 8;

    public int TopPagesToRead { get; init; } = 3;
}
