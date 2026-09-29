namespace Zakira.Recall.Abstractions.Models;

public sealed class FetchResponse
{
    public required string Url { get; init; }

    public required string FinalUrl { get; init; }

    public bool Success { get; init; }

    /// <summary>HTTP status of the main document response, when the navigation produced one.</summary>
    public int? StatusCode { get; init; }

    public string? Title { get; init; }

    public string? Text { get; init; }

    public string? Excerpt { get; init; }

    public string? Domain { get; init; }

    public string? SiteName { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public int WordCount { get; init; }

    /// <summary>
    /// A CSS-like description of the element the readable text was taken from (for example <c>main.main</c> or <c>body</c>).
    /// Useful to diagnose extraction quality.
    /// </summary>
    public string? ContentSelector { get; init; }

    /// <summary>The page's representative image (og:image, twitter:image or the main entity's image), absolute URL.</summary>
    public string? MainImage { get; init; }

    /// <summary>schema.org JSON-LD declared by the page (main entity such as Recipe/Article/Product), if any.</summary>
    public StructuredData? StructuredData { get; init; }

    public OperationError? Error { get; init; }
}
