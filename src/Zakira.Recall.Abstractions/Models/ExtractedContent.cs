namespace Zakira.Recall.Abstractions.Models;

/// <summary>
/// Readable content extracted from a rendered HTML document.
/// </summary>
public sealed class ExtractedContent
{
    /// <summary>The document title (&lt;title&gt;).</summary>
    public string? Title { get; init; }

    /// <summary>The first page heading (h1, falling back to h2).</summary>
    public string? Headline { get; init; }

    /// <summary>The meta description (description, og:description or twitter:description).</summary>
    public string? MetaDescription { get; init; }

    /// <summary>The site name (og:site_name, application-name or the host name).</summary>
    public string? SiteName { get; init; }

    /// <summary>The raw publication timestamp as declared by the page, if any.</summary>
    public string? PublishedAt { get; init; }

    /// <summary>
    /// The readable text: headline, description and main content, with block elements separated by line breaks.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>Number of whitespace-separated words in <see cref="Text"/>.</summary>
    public int WordCount { get; init; }

    /// <summary>Number of readable words in the whole document body (after noise removal).</summary>
    public int BodyWordCount { get; init; }

    /// <summary>
    /// A CSS-like description of the element chosen as main content (for example <c>main.main</c>, <c>article#post-1</c> or <c>body</c>).
    /// </summary>
    public string? ContentSelector { get; init; }
}
