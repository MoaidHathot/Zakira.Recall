using AngleSharp.Dom;

namespace Zakira.Recall.Core.Extraction;

/// <summary>
/// Decides which elements are boilerplate (navigation, chrome, hidden or interactive UI, comments, ads)
/// and must be ignored when reading page text.
/// </summary>
internal sealed class NoiseFilter
{
    private static readonly HashSet<string> NoiseTags = new(StringComparer.Ordinal)
    {
        "script", "style", "noscript", "template", "svg", "math", "iframe", "canvas", "object", "embed",
        "nav", "footer", "header", "dialog",
        "button", "select", "textarea", "input", "option", "optgroup", "datalist",
        "link", "meta", "head", "title"
    };

    private static readonly HashSet<string> NoiseRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "navigation", "banner", "contentinfo", "dialog", "alertdialog", "menu", "menubar", "search", "tooltip"
    };

    private static readonly HashSet<string> ConditionalRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "complementary"
    };

    private static readonly HashSet<string> NoiseClassTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "sidebar", "side-bar", "table-of-contents", "toc", "breadcrumbs", "breadcrumb",
        "related", "related-posts", "share", "sharing", "share-buttons", "social", "socials",
        "ads", "ad", "advert", "advertisement",
        "comments", "comment-list", "commentlist", "comment-respond", "respond",
        "cookie", "cookie-banner", "cookie-consent", "popup", "modal", "newsletter",
        "skip-link", "screen-reader-text", "sr-only", "visually-hidden", "hidden"
    };

    private static readonly HashSet<string> NoiseIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "comments", "respond", "sidebar", "secondary", "footer", "header", "nav", "navigation",
        "masthead", "colophon", "cookie-banner", "cookie-consent", "breadcrumbs", "breadcrumb", "toc"
    };

    /// <summary>Plugin-generated class names embed these markers in longer tokens (e.g. wp-block-yoast-seo-table-of-contents, ez-toc-container).</summary>
    private static readonly string[] NoiseClassSubstrings =
    [
        "table-of-contents", "ez-toc", "lwptoc", "toc-container", "toc_container"
    ];

    /// <summary>
    /// Asides are dropped when they look like link lists (sidebars, related content) or are trivially short.
    /// An aside nested inside an article/main region is structurally part of the content (recipe ingredient
    /// panels, pull-outs) and only counts as noise when it is almost entirely links.
    /// </summary>
    private const double OutsideContentMaxLinkDensity = 0.25;
    private const double InsideContentMaxLinkDensity = 0.6;
    private const int ConditionalMinWords = 10;

    private readonly Dictionary<IElement, bool> _conditionalDecisions = new(ReferenceEqualityComparer.Instance);

    /// <summary>True when the element (not considering its ancestors) must be skipped.</summary>
    public bool IsNoise(IElement element)
    {
        if (IsUnconditionalNoise(element))
        {
            return true;
        }

        if (IsConditional(element))
        {
            return IsNoisyConditional(element);
        }

        return false;
    }

    /// <summary>True when the element or any of its ancestors is noise.</summary>
    public bool IsInsideNoise(IElement element)
    {
        for (var current = element; current is not null; current = current.ParentElement)
        {
            if (IsNoise(current))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsUnconditionalNoise(IElement element)
    {
        if (NoiseTags.Contains(element.LocalName))
        {
            return true;
        }

        var role = element.GetAttribute("role");
        if (role is not null && NoiseRoles.Contains(role.Trim()))
        {
            return true;
        }

        if (string.Equals(element.GetAttribute("aria-hidden"), "true", StringComparison.OrdinalIgnoreCase)
            || element.HasAttribute("hidden"))
        {
            return true;
        }

        var style = element.GetAttribute("style");
        if (style is not null && IsHiddenByInlineStyle(style))
        {
            return true;
        }

        var id = element.Id;
        if (!string.IsNullOrEmpty(id) && NoiseIds.Contains(id))
        {
            return true;
        }

        foreach (var token in element.ClassList)
        {
            if (NoiseClassTokens.Contains(token))
            {
                return true;
            }

            foreach (var marker in NoiseClassSubstrings)
            {
                if (token.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsConditional(IElement element)
    {
        if (element.LocalName == "aside")
        {
            return true;
        }

        var role = element.GetAttribute("role");
        return role is not null && ConditionalRoles.Contains(role.Trim());
    }

    private bool IsNoisyConditional(IElement element)
    {
        if (_conditionalDecisions.TryGetValue(element, out var decision))
        {
            return decision;
        }

        // Evaluate the aside's own readable text with this filter, treating the aside itself as content.
        var stats = TextBuilder.Build(element, this, treatRootAsContent: true);
        var maxLinkDensity = IsInsideContentRegion(element) ? InsideContentMaxLinkDensity : OutsideContentMaxLinkDensity;
        decision = stats.WordCount < ConditionalMinWords || stats.LinkDensity >= maxLinkDensity;
        _conditionalDecisions[element] = decision;
        return decision;
    }

    private static bool IsInsideContentRegion(IElement element)
    {
        for (var current = element.ParentElement; current is not null; current = current.ParentElement)
        {
            if (current.LocalName is "article" or "main"
                || string.Equals(current.GetAttribute("role"), "main", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsHiddenByInlineStyle(string style)
    {
        var compact = style.Replace(" ", string.Empty, StringComparison.Ordinal);
        return compact.Contains("display:none", StringComparison.OrdinalIgnoreCase)
            || compact.Contains("visibility:hidden", StringComparison.OrdinalIgnoreCase);
    }
}
