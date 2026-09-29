using System.Text.Json;

namespace Zakira.Recall.Abstractions.Models;

/// <summary>
/// schema.org data the page declares in <c>application/ld+json</c> blocks.
/// </summary>
public sealed class StructuredData
{
    /// <summary>Types of the root entities found (for example Recipe, Article, WebPage, BreadcrumbList).</summary>
    public required IReadOnlyList<string> Types { get; init; }

    /// <summary>The type of <see cref="MainEntity"/>, when a content entity was found.</summary>
    public string? MainEntityType { get; init; }

    /// <summary>
    /// The most relevant content entity (Recipe, HowTo, Article, Product, ...) as declared by the page, with bulky
    /// non-content members (reviews, comments, actions) removed. Null when the page only declares site chrome
    /// such as WebSite, WebPage or BreadcrumbList.
    /// </summary>
    public JsonElement? MainEntity { get; init; }
}
