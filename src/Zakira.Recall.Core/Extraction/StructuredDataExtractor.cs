using System.Text.Json;
using System.Text.Json.Nodes;
using AngleSharp.Dom;
using Zakira.Recall.Abstractions.Models;

namespace Zakira.Recall.Core.Extraction;

/// <summary>
/// Reads schema.org JSON-LD (<c>&lt;script type="application/ld+json"&gt;</c>) and picks the entity that describes
/// the page's content. Handles top-level arrays, <c>@graph</c> collections, nested entities (for example a Recipe as
/// the <c>mainEntity</c> of a WebPage), multi-valued <c>@type</c>, and malformed blocks (skipped).
/// </summary>
internal sealed class StructuredDataExtractor
{
    /// <summary>Content types in priority order; anything not listed here is never the main entity.</summary>
    private static readonly string[] ContentTypePriority =
    [
        "Recipe", "HowTo", "NewsArticle", "ReportageNewsArticle", "AnalysisNewsArticle", "Article", "BlogPosting",
        "TechArticle", "ScholarlyArticle", "Report", "Product", "SoftwareApplication", "SoftwareSourceCode",
        "Event", "JobPosting", "FAQPage", "QAPage", "Course", "Movie", "Book", "PodcastEpisode", "VideoObject",
        "Dataset", "LocalBusiness", "Place"
    ];

    /// <summary>Members that carry no page content but often dominate the payload (NYT: dozens of reviews).</summary>
    private static readonly HashSet<string> PrunedMembers = new(StringComparer.Ordinal)
    {
        "@context", "review", "reviews", "comment", "comments", "potentialAction", "mainEntityOfPage", "isPartOf",
        "breadcrumb", "hasPart", "sameAs", "interactionStatistic"
    };

    /// <summary>Above this the main entity is omitted from responses; its type and metadata are still reported.</summary>
    internal const int MaxMainEntityJsonLength = 32_000;

    private const int MaxNestingDepth = 8;

    internal sealed record Result(
        IReadOnlyList<string> Types,
        string? MainEntityType,
        JsonObject? MainEntity,
        string? Headline,
        string? DatePublished,
        string? PublisherName,
        string? ImageUrl)
    {
        public static readonly Result Empty = new([], null, null, null, null, null, null);

        public StructuredData? ToModel()
        {
            if (Types.Count == 0)
            {
                return null;
            }

            JsonElement? mainEntity = null;
            if (MainEntity is not null)
            {
                var json = MainEntity.ToJsonString();
                if (json.Length <= MaxMainEntityJsonLength)
                {
                    mainEntity = JsonSerializer.Deserialize<JsonElement>(json);
                }
            }

            return new StructuredData
            {
                Types = Types,
                MainEntityType = MainEntityType,
                MainEntity = mainEntity
            };
        }
    }

    public Result Extract(IDocument document)
    {
        var roots = new List<JsonObject>();
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            JsonNode? node;
            try
            {
                node = JsonNode.Parse(script.TextContent, documentOptions: new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });
            }
            catch (JsonException)
            {
                continue;
            }

            CollectRoots(node, roots);
        }

        if (roots.Count == 0)
        {
            return Result.Empty;
        }

        var types = roots.Select(GetPrimaryType).Where(type => type is not null).Distinct(StringComparer.Ordinal).ToArray()!;

        var entities = new List<JsonObject>();
        foreach (var root in roots)
        {
            CollectEntities(root, entities, depth: 0);
        }

        var mainEntity = entities
            .Select(entity => (Entity: entity, Rank: GetContentRank(entity)))
            .Where(candidate => candidate.Rank >= 0)
            .OrderBy(candidate => candidate.Rank)
            .ThenByDescending(candidate => candidate.Entity.Count)
            .Select(candidate => candidate.Entity)
            .FirstOrDefault();

        var datePublished = FirstString(mainEntity, "datePublished", "dateCreated", "uploadDate")
            ?? entities.Select(entity => FirstString(entity, "datePublished", "dateCreated")).FirstOrDefault(value => value is not null);
        var publisherName = GetName(mainEntity?["publisher"])
            ?? entities.Where(entity => HasType(entity, "WebSite")).Select(entity => FirstString(entity, "name")).FirstOrDefault(value => value is not null)
            ?? entities.Where(entity => HasType(entity, "Organization")).Select(entity => FirstString(entity, "name")).FirstOrDefault(value => value is not null);
        var headline = FirstString(mainEntity, "headline", "name");
        var imageUrl = GetImageUrl(mainEntity?["image"]);

        return new Result(
            types!,
            mainEntity is null ? null : GetPrimaryType(mainEntity),
            mainEntity is null ? null : Prune(mainEntity),
            headline,
            datePublished,
            publisherName,
            imageUrl);
    }

    private static void CollectRoots(JsonNode? node, List<JsonObject> roots)
    {
        switch (node)
        {
            case JsonArray array:
                foreach (var item in array)
                {
                    CollectRoots(item, roots);
                }

                break;
            case JsonObject obj when obj["@graph"] is JsonArray graph:
                foreach (var item in graph)
                {
                    CollectRoots(item, roots);
                }

                break;
            case JsonObject obj when obj.ContainsKey("@type"):
                roots.Add(obj);
                break;
        }
    }

    private static void CollectEntities(JsonNode? node, List<JsonObject> entities, int depth)
    {
        if (depth > MaxNestingDepth)
        {
            return;
        }

        switch (node)
        {
            case JsonObject obj:
                if (obj.ContainsKey("@type"))
                {
                    entities.Add(obj);
                }

                foreach (var property in obj)
                {
                    CollectEntities(property.Value, entities, depth + 1);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    CollectEntities(item, entities, depth + 1);
                }

                break;
        }
    }

    private static IEnumerable<string> GetTypes(JsonObject entity)
    {
        switch (entity["@type"])
        {
            case JsonValue value when value.TryGetValue<string>(out var single):
                yield return single;
                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    if (item is JsonValue itemValue && itemValue.TryGetValue<string>(out var type))
                    {
                        yield return type;
                    }
                }

                break;
        }
    }

    private static string? GetPrimaryType(JsonObject entity)
        => GetTypes(entity).FirstOrDefault();

    private static bool HasType(JsonObject entity, string type)
        => GetTypes(entity).Any(candidate => string.Equals(candidate, type, StringComparison.OrdinalIgnoreCase));

    /// <summary>Lower is better; -1 for entities that are not page content.</summary>
    private static int GetContentRank(JsonObject entity)
    {
        var best = -1;
        foreach (var type in GetTypes(entity))
        {
            var rank = Array.FindIndex(ContentTypePriority, candidate => string.Equals(candidate, type, StringComparison.OrdinalIgnoreCase));
            if (rank >= 0 && (best < 0 || rank < best))
            {
                best = rank;
            }
        }

        return best;
    }

    private static JsonObject Prune(JsonObject entity)
    {
        var clone = (JsonObject)entity.DeepClone();
        foreach (var member in PrunedMembers)
        {
            clone.Remove(member);
        }

        return clone;
    }

    private static string? FirstString(JsonObject? entity, params string[] names)
    {
        if (entity is null)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (entity[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
            {
                return text.Trim();
            }
        }

        return null;
    }

    private static string? GetName(JsonNode? node)
        => node switch
        {
            JsonObject obj => FirstString(obj, "name"),
            JsonArray array => array.Select(GetName).FirstOrDefault(name => name is not null),
            JsonValue value when value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) => text.Trim(),
            _ => null
        };

    /// <summary>schema.org images are a URL string, an ImageObject, or an array of either.</summary>
    internal static string? GetImageUrl(JsonNode? node)
        => node switch
        {
            JsonValue value when value.TryGetValue<string>(out var url) && !string.IsNullOrWhiteSpace(url) => url.Trim(),
            JsonObject obj => FirstString(obj, "url", "contentUrl"),
            JsonArray array => array.Select(GetImageUrl).FirstOrDefault(url => url is not null),
            _ => null
        };
}
