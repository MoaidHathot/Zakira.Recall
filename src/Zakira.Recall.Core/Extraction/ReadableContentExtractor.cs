using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Core.Extraction;

/// <summary>
/// Readability-style extractor working on the rendered DOM serialization of a page.
/// Main content is chosen by scoring every plausible container (semantic elements, SPA roots and common CMS
/// wrappers) on its boilerplate-free word count and link density, and falling back to the whole body when
/// the best container holds only a small fraction of the page text. This avoids the classic failure of
/// taking the first <c>&lt;article&gt;</c> in DOM order, which on many sites is a related-post card or a comment.
/// </summary>
public sealed class ReadableContentExtractor : IContentExtractor
{
    /// <summary>Containers that may hold the main content, in no particular order; the best-scoring one wins.</summary>
    private const string CandidateSelector =
        "article, main, [role='main'], #__next, #root, #app, [itemprop='articleBody'], " +
        ".entry-content, .post-content, .post-body, .article-body, .article-content, .story-body, .content-body, " +
        "#content, #main-content, .main-content";

    /// <summary>Semantic containers get a small edge over generic wrappers that merely enclose them.</summary>
    private const double SemanticWeight = 1.15;

    /// <summary>Below this share of the body's words the best candidate is considered a mis-selection.</summary>
    internal const double MinimumBodyShare = 0.4;

    private static readonly HtmlParser Parser = new();

    public ExtractedContent Extract(string html, string? url = null)
    {
        ArgumentNullException.ThrowIfNull(html);

        var document = Parser.ParseDocument(html);
        var noise = new NoiseFilter();
        var root = (IElement?)document.Body ?? document.DocumentElement;

        var title = NormalizeInline(document.Title);
        var headline = FindHeadline(document, noise);
        var metaDescription = NormalizeInline(
            GetMeta(document, "meta[name='description']")
            ?? GetMeta(document, "meta[property='og:description']")
            ?? GetMeta(document, "meta[name='og:description']")
            ?? GetMeta(document, "meta[name='twitter:description']"));
        var siteName = NormalizeInline(
            GetMeta(document, "meta[property='og:site_name']")
            ?? GetMeta(document, "meta[name='og:site_name']")
            ?? GetMeta(document, "meta[name='application-name']"))
            ?? GetHost(url);
        var publishedAt = NormalizeInline(
            GetMeta(document, "meta[property='article:published_time']")
            ?? GetMeta(document, "meta[name='article:published_time']")
            ?? document.QuerySelector("time[datetime]")?.GetAttribute("datetime"));

        var body = root is null ? new TextStats(string.Empty, 0, 0) : TextBuilder.Build(root, noise, treatRootAsContent: true);
        var selection = root is null ? null : SelectMainContent(root, body, noise);
        var mainText = selection?.Stats.Text ?? body.Text;
        var contentSelector = selection is null ? (root is null ? null : Describe(root)) : Describe(selection.Element);

        var text = ComposeText(headline, metaDescription, mainText);
        return new ExtractedContent
        {
            Title = title,
            Headline = headline,
            MetaDescription = metaDescription,
            SiteName = siteName,
            PublishedAt = publishedAt,
            Text = text,
            WordCount = CountWords(text),
            BodyWordCount = body.WordCount,
            ContentSelector = contentSelector
        };
    }

    private sealed record Selection(IElement Element, TextStats Stats);

    private static Selection? SelectMainContent(IElement root, TextStats body, NoiseFilter noise)
    {
        Selection? best = null;
        var bestScore = double.NegativeInfinity;

        foreach (var candidate in root.QuerySelectorAll(CandidateSelector))
        {
            if (noise.IsInsideNoise(candidate))
            {
                continue;
            }

            var stats = TextBuilder.Build(candidate, noise, treatRootAsContent: true);
            if (stats.WordCount == 0)
            {
                continue;
            }

            var weight = IsSemanticContainer(candidate) ? SemanticWeight : 1.0;
            var score = stats.WordCount * (1.0 - Math.Min(stats.LinkDensity, 0.9)) * weight;
            if (score > bestScore)
            {
                bestScore = score;
                best = new Selection(candidate, stats);
            }
        }

        if (best is null || best.Stats.WordCount < body.WordCount * MinimumBodyShare)
        {
            // Either nothing plausible exists or the winner is a fragment (card, comment, teaser): read the whole body.
            return new Selection(root, body);
        }

        return best;
    }

    private static bool IsSemanticContainer(IElement element)
        => element.LocalName is "article" or "main"
            || string.Equals(element.GetAttribute("role"), "main", StringComparison.OrdinalIgnoreCase)
            || string.Equals(element.GetAttribute("itemprop"), "articleBody", StringComparison.OrdinalIgnoreCase);

    private static string? FindHeadline(IDocument document, NoiseFilter noise)
    {
        foreach (var selector in new[] { "h1", "h2" })
        {
            foreach (var heading in document.QuerySelectorAll(selector))
            {
                if (noise.IsInsideNoise(heading))
                {
                    continue;
                }

                var text = NormalizeInline(heading.TextContent);
                if (text is not null)
                {
                    return text;
                }
            }
        }

        return null;
    }

    /// <summary>Only a headline repeated this close to the start of the main text is treated as a duplicate.</summary>
    private const int DuplicateHeadlineWindow = 300;

    /// <summary>
    /// Text order: headline, meta description, main content. A headline the main content repeats as one of its
    /// first lines is removed there so the title appears once, at the top.
    /// </summary>
    internal static string ComposeText(string? headline, string? metaDescription, string mainText)
    {
        var builder = new StringBuilder(mainText.Length + 512);
        if (headline is not null)
        {
            builder.Append(headline);
            mainText = RemoveLeadingDuplicateLine(mainText, headline);
        }

        if (metaDescription is not null && !mainText.Contains(metaDescription, StringComparison.OrdinalIgnoreCase))
        {
            if (builder.Length > 0)
            {
                builder.Append("\n\n");
            }

            builder.Append(metaDescription);
        }

        if (mainText.Length > 0)
        {
            if (builder.Length > 0)
            {
                builder.Append("\n\n");
            }

            builder.Append(mainText);
        }

        return builder.ToString();
    }

    private static string RemoveLeadingDuplicateLine(string text, string line)
    {
        var limit = Math.Min(text.Length, DuplicateHeadlineWindow);
        var position = 0;
        while (position < limit)
        {
            var end = text.IndexOf('\n', position);
            if (end < 0)
            {
                end = text.Length;
            }

            if (text.AsSpan(position, end - position).Trim().Equals(line, StringComparison.OrdinalIgnoreCase))
            {
                var after = end;
                while (after < text.Length && text[after] == '\n')
                {
                    after++;
                }

                return text.Remove(position, after - position).TrimStart('\n');
            }

            position = end + 1;
        }

        return text;
    }

    private static string? GetMeta(IDocument document, string selector)
    {
        var content = document.QuerySelector(selector)?.GetAttribute("content");
        return string.IsNullOrWhiteSpace(content) ? null : content;
    }

    private static string? GetHost(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host) ? uri.Host : null;

    internal static string Describe(IElement element)
    {
        var builder = new StringBuilder(element.LocalName);
        if (!string.IsNullOrEmpty(element.Id))
        {
            builder.Append('#').Append(element.Id);
        }

        var classes = 0;
        foreach (var token in element.ClassList)
        {
            if (classes++ == 2)
            {
                break;
            }

            builder.Append('.').Append(token);
        }

        return builder.ToString();
    }

    internal static int CountWords(string text)
    {
        var count = 0;
        var inToken = false;
        var tokenHasAlphanumeric = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (inToken && tokenHasAlphanumeric)
                {
                    count++;
                }

                inToken = false;
                tokenHasAlphanumeric = false;
                continue;
            }

            inToken = true;
            tokenHasAlphanumeric |= TextBuilder.IsWordCharacter(ch);
        }

        if (inToken && tokenHasAlphanumeric)
        {
            count++;
        }

        return count;
    }

    /// <summary>Collapses all whitespace runs to single spaces; null for blank input.</summary>
    internal static string? NormalizeInline(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(ch);
        }

        return builder.Length == 0 ? null : builder.ToString();
    }
}
