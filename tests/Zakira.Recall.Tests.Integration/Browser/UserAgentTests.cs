using Microsoft.Extensions.DependencyInjection;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Tests.Integration.Browser;

/// <summary>
/// Headless Edge/Chromium announces itself as "HeadlessChrome/&lt;version&gt;" in the User-Agent header. Two recipe
/// sites that had returned full articles to an older build (Edge 136) started answering the current build (Edge 153)
/// with a "Just a moment..." verification page; the only request difference was that token. The fetcher now
/// presents the browser's regular user agent. Runs only where a real browser is available (Windows with Edge).
/// </summary>
public sealed class UserAgentTests
{
    private const string PageHtml = """
        <html><head><title>Header check</title></head><body>
        <main><h1>Header check</h1>
        <p>This page is served locally so the test can inspect the request headers the browser sent while fetching it.
        It has enough words for the fetcher to treat it as usable content rather than a weak page.</p>
        </main></body></html>
        """;

    [Fact]
    public async Task Headless_Fetches_Send_The_Browser_Regular_User_Agent_And_Consistent_Client_Hints()
    {
        if (!BrowserTestHost.IsAvailable)
        {
            return;
        }

        using var server = LocalHtmlServer.Start(PageHtml);
        await using var services = BrowserTestHost.Build();
        var fetchService = services.GetRequiredService<IFetchService>();

        var first = await fetchService.FetchAsync(new FetchRequest { Url = server.UrlFor("first"), TimeoutSeconds = 30 });
        Assert.True(first.Success, first.Error?.Message);
        var second = await fetchService.FetchAsync(new FetchRequest { Url = server.UrlFor("second"), TimeoutSeconds = 30 });
        Assert.True(second.Success, second.Error?.Message);

        // Every document request that reached the server (including any browser-side retry) must look like a regular Edge.
        var documents = server.Requests.Where(request => request.Headers.GetValueOrDefault("Sec-Fetch-Dest") == "document").ToArray();
        Assert.Contains(documents, request => request.Path == "first");
        Assert.Contains(documents, request => request.Path == "second");
        foreach (var request in documents)
        {
            var userAgent = request.Headers["User-Agent"];
            Assert.DoesNotContain("HeadlessChrome", userAgent);
            Assert.Contains("Chrome/", userAgent);
            Assert.Contains("Edg/", userAgent);

            // The major version in the user agent must match the sec-ch-ua client hints the browser adds itself.
            var major = userAgent.Split("Chrome/")[1].Split('.')[0];
            Assert.Contains($"\"Chromium\";v=\"{major}\"", request.Headers["sec-ch-ua"]);

            // "en-US" when the profile sets a locale, "en-US,en;q=0.9" from --lang=en-US when it does not.
            Assert.StartsWith("en-US", request.Headers["Accept-Language"]);
        }
    }
}
