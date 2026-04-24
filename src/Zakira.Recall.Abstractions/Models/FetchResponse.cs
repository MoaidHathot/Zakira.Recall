namespace Zakira.Recall.Abstractions.Models;

public sealed class FetchResponse
{
    public required string Url { get; init; }

    public required string FinalUrl { get; init; }

    public string? Title { get; init; }

    public string? Text { get; init; }

    public string? Excerpt { get; init; }
}
