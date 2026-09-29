using Microsoft.Playwright;
using Zakira.Recall.Playwright.Browser;

namespace Zakira.Recall.Tests.Unit.Browser;

public sealed class PlaywrightErrorsTests
{
    /// <summary>Stands in for Microsoft.Playwright's internal TargetClosedException, which also derives from PlaywrightException.</summary>
    private sealed class DerivedPlaywrightException(string message, Exception? inner = null) : PlaywrightException(message, inner!);

    [Fact]
    public void Recognizes_A_Dead_Driver_From_The_Transport_Message()
    {
        Assert.True(PlaywrightErrors.IsDriverProcessExited(new DerivedPlaywrightException("Process exited")));
        Assert.True(PlaywrightErrors.IsDriverProcessExited(new DerivedPlaywrightException("Process exited", new DerivedPlaywrightException("Process exited"))));
        Assert.True(PlaywrightErrors.IsDriverProcessExited(new InvalidOperationException("wrapped", new DerivedPlaywrightException("Process exited"))));
    }

    [Fact]
    public void A_Closed_Page_Or_Browser_Is_Not_A_Dead_Driver()
    {
        // Resetting the shared driver here would kill every other in-flight fetch.
        Assert.False(PlaywrightErrors.IsDriverProcessExited(new DerivedPlaywrightException(PlaywrightErrors.TargetClosedMarker)));
        Assert.False(PlaywrightErrors.IsDriverProcessExited(new PlaywrightException("Timeout 30000ms exceeded.")));
        Assert.False(PlaywrightErrors.IsDriverProcessExited(new InvalidOperationException("Process exited")));
    }

    [Fact]
    public void Recognizes_Missing_Browser_Installations()
    {
        Assert.True(PlaywrightErrors.IsMissingBrowserExecutable(new PlaywrightException("Executable doesn't exist at C:\\ms-playwright\\chromium-1200\\chrome-win\\chrome.exe")));
        Assert.True(PlaywrightErrors.IsMissingBrowserExecutable(new PlaywrightException("Chromium distribution 'msedge' is not found at C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe")));
        Assert.False(PlaywrightErrors.IsMissingBrowserExecutable(new PlaywrightException("Process exited")));
    }
}
