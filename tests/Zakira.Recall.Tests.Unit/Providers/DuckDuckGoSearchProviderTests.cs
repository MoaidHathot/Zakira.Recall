using System.Net;
using System.Net.Http;
using Zakira.Recall.Abstractions.Models;
using Zakira.Recall.Playwright.Providers;

namespace Zakira.Recall.Tests.Unit.Providers;

public sealed class DuckDuckGoSearchProviderTests
{
    [Fact]
    public async Task SearchAsync_Uses_Html_Endpoint_And_Parses_Results()
    {
        Uri? requestedUri = null;
        const string responseHtml = """
        <html>
        <body>
          <div class="result results_links results_links_deep web-result">
            <div class="links_main links_deep result__body">
              <h2 class="result__title"><a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Fpost&amp;rut=abc">Example Title</a></h2>
              <a class="result__url" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Fpost&amp;rut=abc">example.com/post</a>
              <a class="result__snippet" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Fpost&amp;rut=abc">Example snippet</a>
              <div class="clear"></div>
            </div>
          </div>
        </body>
        </html>
        """;
        var handler = new DelegatingHandlerStub((request, cancellationToken) =>
        {
            requestedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseHtml)
            });
        });
        var client = new HttpClient(handler);
        var provider = new DuckDuckGoSearchProvider(client);

        var results = await provider.SearchAsync(
            new SearchRequest { Query = "Moaid Hathot", MaxResults = 5 },
            new ProfileDescriptor
            {
                Name = "default",
                UserDataDir = "ignored",
                Channel = "msedge",
                Headless = true,
                DefaultProvider = "duckduckgo",
                TimeoutSeconds = 5
            });

        Assert.NotNull(requestedUri);
        Assert.Equal("html.duckduckgo.com", requestedUri!.Host);
        Assert.Equal("/html/", requestedUri.AbsolutePath);
        Assert.Contains("q=Moaid", requestedUri.Query, StringComparison.Ordinal);
        Assert.Single(results);
        Assert.Equal("Example Title", results[0].Title);
        Assert.Equal("https://example.com/post", results[0].Url);
    }

    [Fact]
    public async Task SearchAsync_Maps_Page_TimeRange_And_SafeSearch()
    {
        Uri? requestedUri = null;
        var handler = new DelegatingHandlerStub((request, cancellationToken) =>
        {
            requestedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body></body></html>")
            });
        });
        var client = new HttpClient(handler);
        var provider = new DuckDuckGoSearchProvider(client);

        await provider.SearchAsync(
            new SearchRequest
            {
                Query = "Moaid Hathot",
                MaxResults = 5,
                Page = 3,
                TimeRange = "month",
                SafeSearch = false
            },
            new ProfileDescriptor
            {
                Name = "default",
                UserDataDir = "ignored",
                Channel = "msedge",
                Headless = true,
                DefaultProvider = "duckduckgo",
                TimeoutSeconds = 5,
                MaxConcurrentFetches = 3,
                EnableProviderFallback = true,
                ProviderHealthCooldownSeconds = 300
            });

        Assert.NotNull(requestedUri);
        Assert.Contains("df=m", requestedUri!.Query, StringComparison.Ordinal);
        Assert.Contains("kp=-1", requestedUri.Query, StringComparison.Ordinal);
        Assert.Contains("s=10", requestedUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_Rejects_Non_Ok_Status_Codes()
    {
        var handler = new DelegatingHandlerStub((request, cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.Accepted)
            {
                Content = new StringContent("<html><body></body></html>")
            }));
        var client = new HttpClient(handler);
        var provider = new DuckDuckGoSearchProvider(client);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => provider.SearchAsync(
            new SearchRequest { Query = "Moaid Hathot", MaxResults = 5 },
            new ProfileDescriptor
            {
                Name = "default",
                UserDataDir = "ignored",
                Channel = "msedge",
                Headless = true,
                DefaultProvider = "duckduckgo",
                TimeoutSeconds = 5,
                MaxConcurrentFetches = 3,
                EnableProviderFallback = true,
                ProviderHealthCooldownSeconds = 300
            }).AsTask());

        Assert.Equal(System.Net.HttpStatusCode.Accepted, exception.StatusCode);
    }

    private sealed class DelegatingHandlerStub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}
