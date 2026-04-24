namespace Zakira.Recall.Abstractions.Models;

public sealed class ResearchSource
{
    public required SearchResult SearchResult { get; init; }

    public required FetchResponse Fetch { get; init; }
}
