using System.Globalization;

namespace Zakira.Recall.Core.Extraction;

/// <summary>
/// Parses publication timestamps as pages declare them: ISO 8601 in meta tags and JSON-LD, but also human formats
/// such as "July 8, 2024 at 2:48pm" (kingarthurbaking.com) or "2023-01-02" from a &lt;time&gt; element.
/// </summary>
internal static class DateParsing
{
    private const DateTimeStyles Styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces;

    public static DateTimeOffset? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, Styles, out var parsed))
        {
            return parsed;
        }

        // "Month d, yyyy at h:mmtt" style: the connector word defeats the invariant parser.
        var withoutConnector = trimmed.Replace(" at ", " ", StringComparison.OrdinalIgnoreCase);
        if (!ReferenceEquals(withoutConnector, trimmed)
            && DateTimeOffset.TryParse(withoutConnector, CultureInfo.InvariantCulture, Styles, out parsed))
        {
            return parsed;
        }

        return null;
    }
}
