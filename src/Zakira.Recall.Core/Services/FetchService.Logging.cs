using Microsoft.Extensions.Logging;

namespace Zakira.Recall.Core.Services;

internal static partial class FetchServiceLogging
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Fetching URL '{Url}' with profile '{Profile}'.")]
    public static partial void FetchStarting(ILogger logger, string url, string profile);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Fetched URL '{Url}' successfully.")]
    public static partial void FetchSucceeded(ILogger logger, string url);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "Fetching URL '{Url}' failed.")]
    public static partial void FetchFailed(ILogger logger, Exception exception, string url);
}
