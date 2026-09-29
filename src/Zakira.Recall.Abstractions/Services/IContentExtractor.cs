using Zakira.Recall.Abstractions.Models;

namespace Zakira.Recall.Abstractions.Services;

/// <summary>
/// Extracts readable content and page metadata from a rendered HTML document.
/// </summary>
public interface IContentExtractor
{
    /// <param name="html">The serialized (rendered) HTML document.</param>
    /// <param name="url">The document URL, used for host-based defaults and resolving relative references.</param>
    ExtractedContent Extract(string html, string? url = null);
}
