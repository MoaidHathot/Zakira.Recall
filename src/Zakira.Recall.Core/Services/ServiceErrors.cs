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
        => exception switch
        {
            TimeoutException => true,
            HttpRequestException httpException => httpException.StatusCode is null or >= HttpStatusCode.InternalServerError,
            TaskCanceledException => true,
            _ when exception.GetType().FullName is "Microsoft.Playwright.PlaywrightException" => true,
            _ => false
        };
}
