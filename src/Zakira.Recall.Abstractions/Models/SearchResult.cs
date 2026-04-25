namespace Zakira.Recall.Abstractions.Models;

public sealed class SearchResult
{
    public required string Title { get; init; }

    public required string Url { get; init; }

    public string? CanonicalUrl { get; init; }

    public string? Host { get; init; }

    public string? DisplayUrl { get; init; }

    public string? Snippet { get; init; }

    public required string Provider { get; init; }

    public int Rank { get; init; }

    public int RawRank { get; init; }

    public int QualityScore { get; init; }

    public string? QueryVariant { get; init; }

    public IReadOnlyList<string> SourceProviders { get; init; } = [];
}
