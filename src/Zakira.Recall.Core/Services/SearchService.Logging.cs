using Microsoft.Extensions.Logging;

namespace Zakira.Recall.Core.Services;

internal static partial class SearchServiceLogging
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Searching query '{Query}' with provider '{Provider}' and profile '{Profile}'.")]
    public static partial void SearchStarting(ILogger logger, string query, string provider, string profile);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Search provider '{Provider}' succeeded with {ResultCount} results for query '{Query}'.")]
    public static partial void SearchSucceeded(ILogger logger, string provider, int resultCount, string query);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "Search provider '{Provider}' failed for query '{Query}'.")]
    public static partial void SearchFailed(ILogger logger, Exception exception, string provider, string query);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "Skipping unhealthy search provider '{Provider}'.")]
    public static partial void SearchProviderSkipped(ILogger logger, string provider);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Information, Message = "Configured fallback provider '{Provider}' did not produce results for query '{Query}'.")]
    public static partial void SearchProviderReturnedNoResults(ILogger logger, string provider, string query);
}
