using Microsoft.Extensions.Logging;

namespace Zakira.Recall.Core.Services;

internal static partial class ResearchServiceLogging
{
    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Researching query '{Query}' with provider '{Provider}' and profile '{Profile}'.")]
    public static partial void ResearchStarting(ILogger logger, string query, string provider, string profile);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information, Message = "Research fetch completed for '{Url}'.")]
    public static partial void ResearchFetchSucceeded(ILogger logger, string url);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Warning, Message = "Research fetch failed for '{Url}'.")]
    public static partial void ResearchFetchFailed(ILogger logger, Exception exception, string url);
}
