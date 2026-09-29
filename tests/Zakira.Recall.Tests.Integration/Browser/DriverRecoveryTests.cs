using Microsoft.Extensions.DependencyInjection;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;
using Zakira.Recall.Playwright.Browser;

namespace Zakira.Recall.Tests.Integration.Browser;

/// <summary>
/// Reproduces the production incident: a long-lived MCP host had fetched successfully, then another process ran a
/// "Stop-Process every node.exe whose command line contains playwright" sweep which killed the Playwright driver.
/// Every later fetch failed instantly with fetch_failed "Process exited" until the host was restarted.
/// Runs only where a real browser is available (Windows with Microsoft Edge).
/// </summary>
public sealed class DriverRecoveryTests
{
    private const string PageHtml = """
        <html><head><title>Local recovery page</title></head><body>
        <main><h1>Local recovery page</h1>
        <p>This page is served from the test process so the browser fetch does not depend on the network.
        It contains enough words for the fetcher to treat it as real content rather than a weak page.</p>
        </main></body></html>
        """;

    [Fact]
    public async Task Fetch_Recovers_After_The_Playwright_Driver_Process_Is_Killed()
    {
        if (!BrowserTestHost.IsAvailable)
        {
            return;
        }

        using var server = LocalHtmlServer.Start(PageHtml);
        await using var services = BrowserTestHost.Build();
        var factory = Assert.IsType<PlaywrightBrowserSessionFactory>(services.GetRequiredService<IBrowserSessionFactory>());
        var fetchService = services.GetRequiredService<IFetchService>();
        var url = server.UrlFor("page");

        var first = await fetchService.FetchAsync(new FetchRequest { Url = url, TimeoutSeconds = 30 });
        Assert.True(first.Success, first.Error?.Message);
        Assert.Contains("Local recovery page", first.Text);
        Assert.Equal(1, factory.DriverCreationCount);

        var killed = BrowserTestHost.KillPlaywrightDriverChildren();
        Assert.True(killed > 0, "Expected to find and kill the Playwright driver (node.exe run-driver) spawned by this process.");
        await Task.Delay(500); // let the transport observe the exit

        var second = await fetchService.FetchAsync(new FetchRequest { Url = url, TimeoutSeconds = 30 });
        Assert.True(second.Success, $"{second.Error?.Code}: {second.Error?.Message}");
        Assert.Contains("Local recovery page", second.Text);
        Assert.Equal(2, factory.DriverCreationCount);

        // The replacement driver is reused, not re-created per fetch.
        var third = await fetchService.FetchAsync(new FetchRequest { Url = url, TimeoutSeconds = 30 });
        Assert.True(third.Success, third.Error?.Message);
        Assert.Equal(2, factory.DriverCreationCount);
    }
}
