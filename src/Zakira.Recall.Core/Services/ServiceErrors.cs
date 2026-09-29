using System.Net;
using Zakira.Recall.Abstractions.Models;

namespace Zakira.Recall.Core.Services;

internal static class ServiceErrors
{
    private const string PlaywrightExceptionTypeName = "Microsoft.Playwright.PlaywrightException";

    /// <summary>Messages of Playwright's (internal) TargetClosedException; both are worth a retry.</summary>
    private static readonly string[] TransientMessageMarkers =
    [
        "Target page, context or browser has been closed",
        "Process exited"
    ];

    public static OperationError FromException(string code, string message, Exception exception, string? provider = null, string? target = null)
        => new()
        {
            Code = code,
            Message = message,
            Provider = provider,
            Target = target,
            Transient = IsTransient(exception)
        };

    internal static bool IsTransient(Exception exception)
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

            // Core does not reference Microsoft.Playwright; match the exception family by name, including subclasses
            // such as TargetClosedException (which the previous exact-type check missed).
            if (DerivesFrom(current.GetType(), PlaywrightExceptionTypeName))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(current.Message)
                && TransientMessageMarkers.Any(marker => current.Message.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DerivesFrom(Type type, string fullName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.FullName == fullName)
            {
                return true;
            }
        }

        return false;
    }
}
