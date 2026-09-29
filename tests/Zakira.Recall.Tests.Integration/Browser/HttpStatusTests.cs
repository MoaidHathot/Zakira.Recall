using Microsoft.Extensions.DependencyInjection;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Abstractions.Services;

namespace Zakira.Recall.Tests.Integration.Browser;

/// <summary>
/// The navigation response used to be discarded, so a 403/404/500 page with a few sentences was reported as
/// successfully fetched content. Runs only where a real browser is available (Windows with Microsoft Edge).
/// </summary>
public sealed class HttpStatusTests
{
    private const string NotFoundHtml = """
        <html><head><title>Page not found</title></head><body>
        <main><h1>We could not find that page</h1>
        <p>The page you are looking for may have been moved or deleted. Try searching the site or return to the home page
        to find popular recipes, guides and other content that our readers enjoy every day.</p>
        </main></body></html>
        """;

    private const string OkHtml = """
        <html><head><title>Fine page</title></head><body>
        <main><h1>Fine page</h1><p>This page exists and is served with a normal status code so it must be reported as a success
        with its status code exposed to the caller for diagnostics.</p></main></body></html>
        """;

    [Fact]
    public async Task Error_Status_Pages_Are_Reported_As_Http_Errors_With_Their_Text_Kept()
    {
        if (!BrowserTestHost.IsAvailable)
        {
            return;
        }

        using var server = LocalHtmlServer.Start(path => path switch
        {
            "missing" => (404, NotFoundHtml),
            "broken" => (503, OkHtml),
            _ => (200, OkHtml)
        });
        await using var services = BrowserTestHost.Build();
        var fetchService = services.GetRequiredService<IFetchService>();

        var ok = await fetchService.FetchAsync(new FetchRequest { Url = server.UrlFor("fine"), TimeoutSeconds = 30 });
        Assert.True(ok.Success, ok.Error?.Message);
        Assert.Equal(200, ok.StatusCode);

        var missing = await fetchService.FetchAsync(new FetchRequest { Url = server.UrlFor("missing"), TimeoutSeconds = 30 });
        Assert.False(missing.Success);
        Assert.Equal(404, missing.StatusCode);
        Assert.Equal("fetch_http_error", missing.Error!.Code);
        Assert.False(missing.Error.Transient);
        Assert.Contains("We could not find that page", missing.Text);
        Assert.True(missing.WordCount > 15);

        var broken = await fetchService.FetchAsync(new FetchRequest { Url = server.UrlFor("broken"), TimeoutSeconds = 30 });
        Assert.False(broken.Success);
        Assert.Equal(503, broken.StatusCode);
        Assert.Equal("fetch_http_error", broken.Error!.Code);
        Assert.True(broken.Error.Transient);
    }
}
