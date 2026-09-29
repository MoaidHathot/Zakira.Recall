using Microsoft.Playwright;

namespace Zakira.Recall.Playwright.Browser;

/// <summary>
/// Classifies Playwright failures. The relevant types (TargetClosedException) are internal to Microsoft.Playwright,
/// so classification relies on the messages the driver transport emits.
/// </summary>
internal static class PlaywrightErrors
{
    /// <summary>Emitted by the stdio transport when the Node driver process exits; the connection is then closed for good.</summary>
    internal const string DriverProcessExitedMarker = "Process exited";

    /// <summary>Emitted when one browser, context or page was closed; the driver itself is still healthy.</summary>
    internal const string TargetClosedMarker = "Target page, context or browser has been closed";

    public static bool IsDriverProcessExited(Exception exception)
        => Any(exception, static current => current is PlaywrightException
            && current.Message.Contains(DriverProcessExitedMarker, StringComparison.OrdinalIgnoreCase));

    public static bool IsMissingBrowserExecutable(Exception exception)
        => Any(exception, static current => current is PlaywrightException
            && (current.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("is not found at", StringComparison.OrdinalIgnoreCase)));

    private static bool Any(Exception exception, Func<Exception, bool> predicate)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (predicate(current))
            {
                return true;
            }
        }

        return false;
    }
}
