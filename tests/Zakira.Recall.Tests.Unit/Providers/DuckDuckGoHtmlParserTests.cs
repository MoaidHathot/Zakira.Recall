using Zakira.Recall.Playwright.Providers;

namespace Zakira.Recall.Tests.Unit.Providers;

public sealed class DuckDuckGoHtmlParserTests
{
    [Fact]
    public void ParseResults_Extracts_Results_From_DuckDuckGo_Html_Page()
    {
        const string html = """
        <!DOCTYPE html>
        <html>
        <body>
          <div id="links" class="results">
            <div class="result results_links results_links_deep web-result">
              <div class="links_main links_deep result__body">
                <h2 class="result__title">
                  <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Falpha&amp;rut=one">Alpha Result</a>
                </h2>
                <div class="result__extras">
                  <a class="result__url" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Falpha&amp;rut=one">example.com/alpha</a>
                </div>
                <a class="result__snippet" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Falpha&amp;rut=one"> First   alpha snippet. </a>
                <div class="clear"></div>
              </div>
            </div>
            <div class="result results_links results_links_deep web-result">
              <div class="links_main links_deep result__body">
                <h2 class="result__title">
                  <a class="result__a" href="https://example.com/beta">Beta Result</a>
                </h2>
                <a class="result__url" href="https://example.com/beta">example.com/beta</a>
                <a class="result__snippet" href="https://example.com/beta">Second snippet</a>
                <div class="clear"></div>
              </div>
            </div>
          </div>
        </body>
        </html>
        """;

        var results = DuckDuckGoHtmlParser.ParseResults(html, maxResults: 10);

        Assert.Collection(results,
            first =>
            {
                Assert.Equal("Alpha Result", first.Title);
                Assert.Equal("https://example.com/alpha", first.Url);
                Assert.Equal("example.com/alpha", first.DisplayUrl);
                Assert.Equal("First alpha snippet.", first.Snippet);
                Assert.Equal("duckduckgo", first.Provider);
                Assert.Equal(1, first.Rank);
            },
            second =>
            {
                Assert.Equal("Beta Result", second.Title);
                Assert.Equal("https://example.com/beta", second.Url);
                Assert.Equal("example.com/beta", second.DisplayUrl);
                Assert.Equal("Second snippet", second.Snippet);
                Assert.Equal(2, second.Rank);
            });
    }

    [Fact]
    public void ParseResults_Respects_MaxResults()
    {
        const string html = """
        <!DOCTYPE html>
        <html>
        <body>
          <div class="result web-result"><div><a class="result__a" href="https://example.com/1">One</a><div class="clear"></div></div></div>
          <div class="result web-result"><div><a class="result__a" href="https://example.com/2">Two</a><div class="clear"></div></div></div>
        </body>
        </html>
        """;

        var results = DuckDuckGoHtmlParser.ParseResults(html, maxResults: 1);

        Assert.Single(results);
        Assert.Equal("One", results[0].Title);
    }
}
