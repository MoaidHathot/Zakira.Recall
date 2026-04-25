using System.Net;
using Zakira.Recall.Abstractions.Models;

namespace Zakira.Recall.Core.Services;

internal static class ServiceErrors
{
    public static OperationError FromException(string code, string message, Exception exception, string? provider = null, string? target = null)
        => new()
        {
            Code = code,
            Message = message,
            Provider = provider,
            Target = target,
            Transient = IsTransient(exception)
        };

    private static bool IsTransient(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            switch (current)
            {
                case TimeoutException:
                    return true;
                case HttpRequestException httpException when httpException.StatusCode is null or >= HttpStatusCode.InternalServerError:
                    return true;
                case TaskCanceledException:
                    return true;
            }

            if (current.GetType().FullName is "Microsoft.Playwright.PlaywrightException")
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(current.Message)
                && current.Message.Contains("Target page, context or browser has been closed", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
